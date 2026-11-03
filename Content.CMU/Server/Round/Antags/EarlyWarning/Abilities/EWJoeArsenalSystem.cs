using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.CMU14.Medical.Injuries.Wounds;
using Content.Server.CMU14.Surrender;
using Content.Shared.Body.Systems;
using Content.Server.CMU14.Yautja;
using Content.Shared._RMC14.Inventory;
using Content.Shared._RMC14.Line;
using Content.Shared._RMC14.Sentry;
using Content.Shared._RMC14.Smoke;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.CMU14.Insurgency.Sapper;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.CMU14.Round.Antags.EarlyWarning.Abilities;
using Content.Shared.CMU14.Round.Antags.EarlyWarning.Melding;
using Content.Shared.CMU14.Surrender;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.Weapons.GunSwitch;
using Content.Shared.CMU14.Weapons.Safety;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Storage;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Round.Antags.EarlyWarning.Abilities;

/// <summary>
/// Phase E5: the Grand Machine's Hand (machete), Consecrate, Unwind, the heat cost of the Z-Level
/// Leap, Disrupt-II and the timed lockouts those leave behind (silenced sentries, doors held open).
/// Everything that targets the world is server-side; the Joe's own carried gear is spared the same way
/// the level-0 pulse spares it.
/// </summary>
public sealed partial class EWJoeArsenalSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private EWJoeAbilitySystem _ability = default!;
    [Dependency] private EWJoeHeatSystem _heat = default!;
    [Dependency] private CMUMedicalBodyIndexSystem _bodyIndex = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedDoorSystem _door = default!;
    [Dependency] private ExamineSystemShared _examine = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private LineSystem _line = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private CMUGunSafetySystem _safety = default!;
    [Dependency] private SentrySystem _sentry = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private CMUSurrenderSystem _surrender = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedCMUWoundsSystem _wounds = default!;
    [Dependency] private CMUWoundsSystem _cmuWounds = default!;
    [Dependency] private BloodstreamSystem _bloodstream = default!;

    private static readonly ProtoId<DamageTypePrototype> HeatDamage = "Heat";

    private readonly HashSet<Entity<MobStateComponent>> _mobs = new();
    private readonly HashSet<Entity<GunComponent>> _guns = new();
    private readonly HashSet<Entity<SentryComponent>> _sentries = new();
    private readonly HashSet<EntityUid> _pulsed = new();
    private readonly HashSet<EntityUid> _unwound = new();
    private TimeSpan _nextLockoutTick;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EWJoeComponent, EWJoeMacheteActionEvent>(OnMachete);
        SubscribeLocalEvent<EWJoeComponent, EWJoeConsecrateActionEvent>(OnConsecrate);
        SubscribeLocalEvent<EWJoeComponent, EWJoeUnwindActionEvent>(OnUnwind);
        SubscribeLocalEvent<EWJoeComponent, YautjaLeapActionEvent>(OnZLeap, before: [typeof(YautjaAbilitySystem)]);
        SubscribeLocalEvent<EWJoeComponent, YautjaLeapDoAfterEvent>(OnZLeapDoAfter, before: [typeof(YautjaAbilitySystem)]);
        SubscribeLocalEvent<EWJoeComponent, ExaminedEvent>(OnExamined);

        SubscribeLocalEvent<EWJoeMacheteComponent, MeleeHitEvent>(OnMacheteHit);

        SubscribeLocalEvent<EWJoeDisruptPulseEvent>(OnDisruptPulse);
        SubscribeLocalEvent<EWJoeRemovedEvent>(OnJoeRemoved);
        SubscribeLocalEvent<EWJoeMechanicsChangedEvent>(OnMechanicsChanged);
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<EWJoeLevelChangedEvent>(OnLevelChanged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextLockoutTick)
            return;

        _nextLockoutTick = now + TimeSpan.FromSeconds(1);

        // the sentry has no lockout of its own, so Off is re-asserted until the lockout ends
        var sentryQuery = EntityQueryEnumerator<EWJoeSentryLockoutComponent, SentryComponent>();
        while (sentryQuery.MoveNext(out var uid, out var lockout, out var sentry))
        {
            if (now >= lockout.Until)
            {
                RemCompDeferred<EWJoeSentryLockoutComponent>(uid);
                continue;
            }

            if (sentry.Mode is not SentryMode.Off and not SentryMode.Item)
                _sentry.TrySetMode((uid, sentry), SentryMode.Off);
        }

        var doorQuery = EntityQueryEnumerator<EWJoeDoorHeldOpenComponent>();
        while (doorQuery.MoveNext(out var uid, out var held))
        {
            if (now < held.Until)
                continue;

            if (held.Bolted && TryComp(uid, out DoorBoltComponent? bolt) && bolt.BoltsDown)
                _door.TrySetBoltDown((uid, bolt), false);

            RemCompDeferred<EWJoeDoorHeldOpenComponent>(uid);
        }
    }

    /// <summary>Switched off (ewjoemechanics): drawn machetes vanish, as the action that puts one away is greyed out.</summary>
    private void OnMechanicsChanged(ref EWJoeMechanicsChangedEvent args)
    {
        if (args.Enabled)
            return;

        var query = EntityQueryEnumerator<EWJoeMacheteComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            QueueDel(uid);
        }
    }

    #region Grand Machine's Hand

    private void OnMachete(Entity<EWJoeComponent> ent, ref EWJoeMacheteActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (FindMachete(ent) is { } held)
        {
            QueueDel(held);
            _popup.PopupEntity(Loc.GetString("cmu-ew-joe-machete-dismiss"), ent, ent);
            return;
        }

        // raised hands refuse anything put into them; drawing the machete ends the surrender first
        if (HasComp<CMUSurrenderComponent>(ent))
            _surrender.LowerHands(ent);

        if (!TrySummonMachete(ent, out _))
            _popup.PopupEntity(Loc.GetString("cmu-ew-joe-machete-no-hand"), ent, ent, PopupType.SmallCaution);
        else
            _popup.PopupEntity(Loc.GetString("cmu-ew-joe-machete-summon"), ent, ent);
    }

    /// <summary>Spawns the machete into a free hand; without one nothing is left on the floor.</summary>
    public bool TrySummonMachete(Entity<EWJoeComponent> joe, out EntityUid machete)
    {
        machete = Spawn(joe.Comp.MachetePrototype, Transform(joe).Coordinates);
        EnsureComp<EWJoeMacheteComponent>(machete).Joe = joe;

        if (_hands.TryPickupAnyHand(joe, machete, checkActionBlocker: false))
            return true;

        QueueDel(machete);
        machete = default;
        return false;
    }

    public EntityUid? FindMachete(EntityUid joe)
    {
        foreach (var held in _hands.EnumerateHeld(joe))
        {
            if (HasComp<EWJoeMacheteComponent>(held))
                return held;
        }

        return null;
    }

    /// <summary>
    /// A-11: every hit on an organic seeds an internal bleed on a random part; synth parts ignore it (P6).
    /// The blade is serrated: each hit also tears blood out at once (it pools as usual) and throws a spray of
    /// it on the far side of the victim.
    /// </summary>
    private void OnMacheteHit(Entity<EWJoeMacheteComponent> ent, ref MeleeHitEvent args)
    {
        // a handled hit was cancelled and deals no damage
        if (args.Handled)
            return;

        var from = _transform.GetWorldPosition(args.User);
        foreach (var target in args.HitEntities)
        {
            if (!EWJoeTargetRules.IsOrganic(EntityManager, target))
                continue;

            // a body with nothing left to lose leaves no fresh spray
            var before = _bloodstream.GetBloodLevel(target);
            if (_bloodstream.TryBleedOut(target, ent.Comp.BloodSpill) && _bloodstream.GetBloodLevel(target) < before)
            {
                _cmuWounds.SprayBloodFromHit(target, _transform.GetWorldPosition(target) - from,
                    ent.Comp.SprayRange, ent.Comp.SprayDecals);
            }

            var parts = _bodyIndex.GetBodyParts(target).ToList();
            if (parts.Count == 0)
                continue;

            var part = _random.Pick(parts);
            _wounds.SeedInternalBleed(part, ent.Comp.BleedSource, ent.Comp.BleedRate);
        }
    }

    #endregion

    #region Consecrate

    /// <summary>The Consecrate action's range (ew_joe_actions.yml), for the line-of-sight check.</summary>
    private const float ConsecrateReach = 6f;

    private void OnConsecrate(Entity<EWJoeComponent> ent, ref EWJoeConsecrateActionEvent args)
    {
        if (args.Handled)
            return;

        var target = args.Target;
        // ABIL-03: never the Joe's own thrall; ABIL-02: never through a wall (the action itself only checks range)
        if (target == ent.Owner || !TryComp(target, out MobStateComponent? mob) || !_mobState.IsAlive(target, mob)
            || HasComp<RiverMeldedComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("cmu-ew-joe-consecrate-invalid"), ent, ent, PopupType.SmallCaution);
            return;
        }

        if (!_examine.InRangeUnOccluded(ent.Owner, target, ConsecrateReach, e => HasComp<EvenSmokeComponent>(e)))
        {
            _popup.PopupEntity(Loc.GetString("cmu-ew-joe-consecrate-unseen"), ent, ent, PopupType.SmallCaution);
            return;
        }

        if (!_heat.TrySpendHeat(ent, ent.Comp.ConsecrateHeat))
            return;

        args.Handled = true;
        var hit = Consecrate(ent, target);
        _popup.PopupEntity(Loc.GetString("cmu-ew-joe-consecrate-done", ("count", hit)), ent, ent, PopupType.Medium);
    }

    /// <summary>
    /// A-12: an arc from the Joe to the target, then to the nearest unburnt living body within jump
    /// range and line of sight of the last, up to the jump count. Damage goes straight through
    /// <see cref="DamageableSystem"/> (P5: no electrocution, no insulation). Thralls are skipped.
    /// Returns how many bodies the arc reached.
    /// </summary>
    public int Consecrate(Entity<EWJoeComponent> joe, EntityUid first)
    {
        var hit = new List<EntityUid>();
        var damage = new DamageSpecifier(_proto.Index(HeatDamage), joe.Comp.ConsecrateDamage);
        var source = joe.Owner;
        var current = first;

        while (hit.Count < joe.Comp.ConsecrateJumps)
        {
            hit.Add(current);
            _line.TryCreateLine(source, current, joe.Comp.ConsecrateBeam.Id, out _);
            _damageable.TryChangeDamage(current, damage, origin: joe);
            _stun.TryParalyze(current, joe.Comp.ConsecrateParalyze, true);

            EntityUid? next = null;
            var best = float.MaxValue;
            var from = _transform.GetWorldPosition(current);
            _mobs.Clear();
            _lookup.GetEntitiesInRange(Transform(current).Coordinates, joe.Comp.ConsecrateJumpRange, _mobs, LookupFlags.Uncontained);
            foreach (var candidate in _mobs)
            {
                if (candidate.Owner == joe.Owner || hit.Contains(candidate) || !_mobState.IsAlive(candidate, candidate.Comp))
                    continue;

                if (HasComp<RiverMeldedComponent>(candidate))
                    continue;

                // the Joe's own Overclock smoke carries an occluder per tile; the arc goes through smoke
                if (!_examine.InRangeUnOccluded(current, candidate.Owner, joe.Comp.ConsecrateJumpRange, e => HasComp<EvenSmokeComponent>(e)))
                    continue;

                var distance = (_transform.GetWorldPosition(candidate) - from).LengthSquared();
                if (distance >= best)
                    continue;

                best = distance;
                next = candidate;
            }

            if (next == null)
                break;

            source = current;
            current = next.Value;
        }

        _adminLogger.Add(LogType.Action, LogImpact.Medium,
            $"Early Warning Joe {ToPrettyString(joe):joe} consecrated {hit.Count} target(s) starting with {ToPrettyString(first):target}");
        return hit.Count;
    }

    #endregion

    #region Unwind

    private void OnUnwind(Entity<EWJoeComponent> ent, ref EWJoeUnwindActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var (guns, sentries) = Unwind(ent);
        _popup.PopupEntity(Loc.GetString("cmu-ew-joe-unwind-done", ("guns", guns), ("sentries", sentries)), ent, ent);
    }

    /// <summary>
    /// A-13 (playtest 3): every firearm in range drops its magazine, has its safety switched on and
    /// jams until racked: in a person's hands, worn in any slot (back, suit storage, belt) or one
    /// container down inside a holster, and loose on the floor. Sentries are switched off and held off.
    /// The Joe's own gear and its thralls' are spared. Returns the counts.
    /// </summary>
    public (int Guns, int Sentries) Unwind(Entity<EWJoeComponent> joe)
    {
        var coordinates = Transform(joe).Coordinates;
        var range = joe.Comp.UnwindRange;
        var sentries = 0;
        _unwound.Clear();

        // the engine's contained lookup only descends from entities that already matched the query,
        // so a gun lookup never reaches a person's hands, slots or holster: walk the people instead
        _mobs.Clear();
        _lookup.GetEntitiesInRange(coordinates, range, _mobs, LookupFlags.Uncontained);
        foreach (var mob in _mobs)
        {
            // thralls are the Joe's own; Consecrate spares them and so does this
            if (mob.Owner == joe.Owner || HasComp<RiverMeldedComponent>(mob))
                continue;

            foreach (var item in _inventory.GetHandOrInventoryEntities(mob.Owner))
            {
                CollectGun(item);

                // holsters, scabbards and gun belts keep the weapon one container down
                CollectHolstered(item);

                // shoulder holsters are accessories on the uniform or armor: look one level into a worn
                // item, but never into a pack's storage (a gun in a backpack is not drawn) or its actions
                if (!TryComp(item, out ContainerManagerComponent? manager))
                    continue;

                TryComp(item, out StorageComponent? storage);
                foreach (var container in _container.GetAllContainers(item, manager))
                {
                    if (container == storage?.Container || container.ID == ActionsContainerComponent.ContainerId)
                        continue;

                    foreach (var contained in container.ContainedEntities)
                    {
                        CollectHolstered(contained);
                    }
                }
            }
        }

        // guns lying loose; not what sits in lockers and crates
        _guns.Clear();
        _lookup.GetEntitiesInRange(coordinates, range, _guns, LookupFlags.Dynamic | LookupFlags.Static | LookupFlags.Sundries);
        foreach (var gun in _guns)
        {
            if (!_container.IsEntityInContainer(gun))
                CollectGun(gun);
        }

        foreach (var gun in _unwound)
        {
            UnwindGun(joe, gun);
        }

        _sentries.Clear();
        _lookup.GetEntitiesInRange(coordinates, joe.Comp.UnwindRange, _sentries);
        foreach (var sentry in _sentries)
        {
            if (LockSentry(sentry, joe.Comp.UnwindSentryLockout))
                sentries++;
        }

        var guns = _unwound.Count;
        _adminLogger.Add(LogType.Action, LogImpact.Medium,
            $"Early Warning Joe {ToPrettyString(joe):joe} unwound {guns} gun(s) and {sentries} sentry(ies)");
        return (guns, sentries);
    }

    private void CollectGun(EntityUid uid)
    {
        if (HasComp<GunComponent>(uid) && !HasComp<SentryComponent>(uid))
            _unwound.Add(uid);
    }

    private void CollectHolstered(EntityUid uid)
    {
        if (!HasComp<CMHolsterComponent>(uid) || !TryComp(uid, out ContainerManagerComponent? manager))
            return;

        foreach (var container in _container.GetAllContainers(uid, manager))
        {
            foreach (var contained in container.ContainedEntities)
            {
                CollectGun(contained);
            }
        }
    }

    /// <summary>Magazine out, safety on (no lock: playtest 3), chamber jammed for one clean rack.</summary>
    private void UnwindGun(Entity<EWJoeComponent> joe, EntityUid gun)
    {
        _itemSlots.TryEject(gun, SharedGunSystem.MagazineSlot, null, out _);

        if (!HasComp<CMUNoSafetyComponent>(gun))
            _safety.SetSafety(gun, true, playSound: false);

        var jam = EnsureComp<GunJammedComponent>(gun);
        jam.RackTime = joe.Comp.UnwindRackTime;
        jam.RackFailChance = 0f;
        Dirty(gun, jam);
    }

    /// <summary>Switches a deployed sentry off and holds it there; a packed one is left alone.</summary>
    private bool LockSentry(Entity<SentryComponent> sentry, TimeSpan duration)
    {
        if (sentry.Comp.Mode == SentryMode.Item)
            return false;

        _sentry.TrySetMode(sentry, SentryMode.Off);
        var lockout = EnsureComp<EWJoeSentryLockoutComponent>(sentry);
        var until = _timing.CurTime + duration;
        if (until > lockout.Until)
            lockout.Until = until;
        return true;
    }

    #endregion

    #region Z-Level Leap

    /// <summary>
    /// Runs before the yautja leap system (playtest 3: the Joe leaps like a hunter, up, down or across).
    /// An overheated Joe cannot start the windup. The heat is charged only once the yautja system has
    /// really launched the leap (<c>EWJoeHeatSystem.OnZLeapLaunched</c>, ABIL-06), so a leap the map refuses, a
    /// windup broken by a hit or a downed Joe costs nothing.
    /// </summary>
    private void OnZLeap(Entity<EWJoeComponent> ent, ref YautjaLeapActionEvent args)
    {
        if (args.Handled)
            return;

        if (TryComp(ent, out EWJoeHeatComponent? heat) && heat.Overheated)
        {
            _popup.PopupEntity(Loc.GetString("cmu-ew-joe-heat-overheated"), ent, ent, PopupType.SmallCaution);
            args.Handled = true;
        }
    }

    /// <summary>An overheated core cannot land the leap: refused before the yautja system launches it.</summary>
    private void OnZLeapDoAfter(Entity<EWJoeComponent> ent, ref YautjaLeapDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        if (TryComp(ent, out EWJoeHeatComponent? heat) && heat.Overheated)
        {
            _popup.PopupEntity(Loc.GetString("cmu-ew-joe-heat-overheated"), ent, ent, PopupType.SmallCaution);
            args.Handled = true;
        }
    }

    #endregion

    #region Disrupt-II

    /// <summary>
    /// A-2: from the tier-two level the pulse also opens and holds doors, disarms planted sapper traps
    /// and silences sentries. Doors are unbolted, opened and bolted open (bolting needs power; an
    /// unpowered door simply opens); the hold is released by the update loop.
    /// </summary>
    private void OnDisruptPulse(ref EWJoeDisruptPulseEvent args)
    {
        if (!TryComp(args.Joe, out EWJoeComponent? joe) || args.Level < joe.DisruptTierTwoLevel)
            return;

        var now = _timing.CurTime;
        _pulsed.Clear();
        _lookup.GetEntitiesInRange(args.Coordinates, args.Range, _pulsed);

        foreach (var uid in _pulsed)
        {
            if (uid == args.Joe || _ability.IsCarriedBy(args.Joe, uid))
                continue;

            if (TryComp(uid, out DoorComponent? door))
            {
                if (TryComp(uid, out DoorBoltComponent? bolt))
                    HoldOpen((uid, door), bolt, now + joe.DisruptDoorHold);
                continue;
            }

            if (TryComp(uid, out SapperTrapComponent? trap))
            {
                Disarm((uid, trap));
                continue;
            }

            if (TryComp(uid, out SentryComponent? sentry))
                LockSentry((uid, sentry), joe.UnwindSentryLockout);
        }
    }

    /// <summary>
    /// Only doors with bolts (airlocks and their kin) are thrown open: firelocks hold fire and pressure,
    /// and a shutter or blast door has no auto-close to give the door back afterwards. Bolts that will
    /// not lift (no power) are respected; the door is never opened through them.
    /// </summary>
    public void HoldOpen(Entity<DoorComponent> door, DoorBoltComponent bolt, TimeSpan until)
    {
        if (HasComp<FirelockComponent>(door))
            return;

        if (bolt.BoltsDown)
        {
            _door.TrySetBoltDown((door, bolt), false);
            if (bolt.BoltsDown)
                return;
        }

        if (door.Comp.State is DoorState.Closed or DoorState.Closing or DoorState.Denying)
            _door.StartOpening(door, door.Comp);
        else if (door.Comp.State is not (DoorState.Open or DoorState.Opening))
            return;

        var held = EnsureComp<EWJoeDoorHeldOpenComponent>(door);
        if (_door.TrySetBoltDown((door, bolt), true))
            held.Bolted = true;

        if (until > held.Until)
            held.Until = until;
    }

    /// <summary>A planted trap loses its arming; the owner must pick it up and plant it again.</summary>
    public void Disarm(Entity<SapperTrapComponent> trap)
    {
        if (!trap.Comp.Deployed || !trap.Comp.Armed && trap.Comp.ArmsAt == null)
            return;

        trap.Comp.Armed = false;
        trap.Comp.ArmsAt = null;
        Dirty(trap);
    }

    #endregion

    #region Cleanup

    /// <summary>Level 3 leaves a mark on the casing (design: Evolution's disfigurement).</summary>
    private void OnExamined(Entity<EWJoeComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Level >= 3)
            args.PushMarkup(Loc.GetString("cmu-ew-joe-examine-evolved"));
    }

    private void OnJoeRemoved(ref EWJoeRemovedEvent args)
    {
        if (FindMachete(args.Joe) is { } machete)
            QueueDel(machete);
    }

    /// <summary>
    /// Playtest 4: a respawn can drop the Joe below the level that grants the machete. The summoned blade
    /// cannot be dropped, so it goes with the action rather than linger in a hand until the level returns.
    /// </summary>
    private void OnLevelChanged(ref EWJoeLevelChangedEvent args)
    {
        if (args.NewLevel >= args.OldLevel || !TryComp(args.Joe, out EWJoeComponent? joe))
            return;

        foreach (var action in joe.GrantedActions)
        {
            if (Exists(action) && MetaData(action).EntityPrototype?.ID == MacheteActionId)
                return;
        }

        if (FindMachete(args.Joe) is { } machete)
            QueueDel(machete);
    }

    private const string MacheteActionId = "CMUActionEWJoeMachete";

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || !HasComp<EWJoeComponent>(args.Target))
            return;

        if (FindMachete(args.Target) is { } machete)
            QueueDel(machete);
    }

    #endregion
}
