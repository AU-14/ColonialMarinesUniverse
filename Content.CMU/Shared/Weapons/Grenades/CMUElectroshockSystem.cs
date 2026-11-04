using System.Collections.Generic;
using System.Numerics;
using Content.Shared._RMC14.Explosion;
using Content.Shared._RMC14.Map;
using Content.Shared._RMC14.Slow;
using Content.Shared._RMC14.Synth;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Trigger;
using Content.Shared.Trigger.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Content.Shared.Toggleable;

namespace Content.Shared.CMU14.Weapons.Grenades;

public sealed partial class CMUElectroshockSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private CollisionWakeSystem _collisionWake = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private GunIFFSystem _gunIff = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private RMCMapSystem _rmcMap = default!;
    [Dependency] private RMCSlowSystem _slow = default!;
    [Dependency] private SharedStaminaSystem _stamina = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TriggerSystem _trigger = default!;

    private readonly HashSet<Entity<MobStateComponent>> _mobs = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUElectroshockComponent, TriggerEvent>(OnTrigger);
        SubscribeLocalEvent<CMUElectroshockComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<CMUElectroshockComponent, CMUElectroshockPlantDoAfterEvent>(OnPlantDoAfter);
        SubscribeLocalEvent<CMUElectroshockComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<CMUElectroshockComponent, ClaymoreDisarmDoafterEvent>(OnDisarmed, after: new[] { typeof(SharedRMCLandmineSystem) });
    }

    private void OnExamined(Entity<CMUElectroshockComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.MinePrototype != null)
            args.PushMarkup(Loc.GetString("cmu-electroshock-examine-plant"));
    }

    private void OnTrigger(Entity<CMUElectroshockComponent> ent, ref TriggerEvent args)
    {
        if (args.Key != null && !ent.Comp.TriggerKeys.Contains(args.Key))
            return;

        args.Handled = true;
        if (_net.IsClient)
            return;

        if (ent.Comp.PrimedPrototype is { } primed)
        {
            // The planted mine pops a live grenade out of the ground instead of going off instantly.
            var grenade = Spawn(primed, _transform.GetMoverCoordinates(ent));
            if (TryComp(grenade, out CMUElectroshockComponent? primedShock))
            {
                primedShock.Factions.UnionWith(ent.Comp.Factions);
                Dirty(grenade, primedShock);
            }

            _trigger.Trigger(grenade, args.User, "startTimer");
            QueueDel(ent);
            return;
        }

        Pulse(ent);
        QueueDel(ent);
    }

    private void OnDisarmed(Entity<CMUElectroshockComponent> ent, ref ClaymoreDisarmDoafterEvent args)
    {
        if (args.Cancelled || _net.IsClient || ent.Comp.DisarmedPrototype is not { } disarmed)
            return;

        var grenade = Spawn(disarmed, _transform.GetMoverCoordinates(ent));
        _hands.TryPickupAnyHand(args.User, grenade);
        QueueDel(ent);
    }

    private void Pulse(Entity<CMUElectroshockComponent> ent)
    {
        var coordinates = _transform.GetMapCoordinates(ent);
        _audio.PlayPvs(ent.Comp.ExplodeSound, _transform.GetMoverCoordinates(ent));
        if (ent.Comp.Effect is { } effect)
            Spawn(effect, coordinates);

        _mobs.Clear();
        _lookup.GetEntitiesInRange(coordinates, ent.Comp.Range, _mobs);
        foreach (var mob in _mobs)
        {
            if (_mobState.IsDead(mob))
                continue;

            if (IsFriendly(ent, mob))
                continue;

            if (!_interaction.InRangeUnobstructed(ent.Owner, mob.Owner, ent.Comp.Range + 0.5f, popup: false))
                continue;

            var delta = _transform.GetMapCoordinates(mob).Position - coordinates.Position;
            var distance = MathF.Round(MathF.Max(MathF.Abs(delta.X), MathF.Abs(delta.Y)));
            var amount = ent.Comp.Damage - distance * ent.Comp.FalloffPerTile;
            if (amount <= 0)
                continue;

            if (HasComp<XenoComponent>(mob))
            {
                Damage(ent, mob, amount);
                if (distance < ent.Comp.XenoSuperslowRange)
                    _slow.TrySuperSlowdown(mob, TimeSpan.FromSeconds(2));

                _slow.TrySlowdown(mob, TimeSpan.FromSeconds(amount / ent.Comp.XenoSlowDivisor));
            }
            else
            {
                if (HasComp<SynthComponent>(mob))
                {
                    // Massive overvoltage to an ungrounded synthetic jams its systems.
                    _stun.TryParalyze(mob, ent.Comp.SynthStunTime, true);
                    amount *= ent.Comp.SynthDamageMultiplier;
                    _popup.PopupEntity(Loc.GetString("cmu-electroshock-synth-overvolt"), mob, mob, PopupType.LargeCaution);
                }

                Damage(ent, mob, amount);
                _stamina.TakeStaminaDamage(mob, amount * ent.Comp.HumanStaminaFactor);
            }

            // Standing on top of the equivalent of a canned lightning bolt should hurt.
            if (distance < 1)
                _slow.TrySuperSlowdown(mob, TimeSpan.FromSeconds(3));
        }
    }

    private bool IsFriendly(Entity<CMUElectroshockComponent> ent, EntityUid target)
    {
        foreach (var faction in ent.Comp.Factions)
        {
            if (_gunIff.IsInFaction(target, faction))
                return true;
        }

        return false;
    }

    private void Damage(Entity<CMUElectroshockComponent> ent, EntityUid target, float amount)
    {
        var damage = new DamageSpecifier();
        damage.DamageDict[ent.Comp.DamageType] = amount;
        _damageable.TryChangeDamage(target, damage, origin: ent);
    }

    private void OnAfterInteract(Entity<CMUElectroshockComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target != null || ent.Comp.MinePrototype == null)
            return;

        // Planting only works on the tile you're standing on, like the cmss13 "plant at feet".
        var userCoords = _transform.GetMoverCoordinates(args.User);
        var clicked = _transform.ToMapCoordinates(args.ClickLocation);
        var user = _transform.ToMapCoordinates(userCoords);
        if (clicked.MapId != user.MapId ||
            MathF.Floor(clicked.Position.X) != MathF.Floor(user.Position.X) ||
            MathF.Floor(clicked.Position.Y) != MathF.Floor(user.Position.Y))
        {
            return;
        }

        args.Handled = true;
        if (!CanPlant(ent, args.User))
            return;

        _popup.PopupEntity(Loc.GetString("cmu-electroshock-plant-start", ("grenade", ent)), args.User, args.User);
        _audio.PlayPredicted(ent.Comp.PlantSound, args.User, args.User);

        var doAfter = new DoAfterArgs(EntityManager, args.User, ent.Comp.PlantDelay, new CMUElectroshockPlantDoAfterEvent(), ent, used: ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            BreakOnHandChange = true,
        };
        _doAfter.TryStartDoAfter(doAfter);
    }

    private bool CanPlant(Entity<CMUElectroshockComponent> ent, EntityUid user)
    {
        if (_container.IsEntityInContainer(user))
        {
            _popup.PopupEntity(Loc.GetString("cmu-electroshock-plant-fail-here"), user, user, PopupType.SmallCaution);
            return false;
        }

        var coords = _transform.GetMoverCoordinates(user);
        var anchored = _rmcMap.GetAnchoredEntitiesEnumerator(coords);
        while (anchored.MoveNext(out var uid))
        {
            if (HasComp<RMCLandmineComponent>(uid))
            {
                _popup.PopupEntity(Loc.GetString("cmu-electroshock-plant-fail-occupied"), user, user, PopupType.SmallCaution);
                return false;
            }

            if (TryComp(uid, out PhysicsComponent? physics) && physics.Hard && physics.BodyType == BodyType.Static)
            {
                _popup.PopupEntity(Loc.GetString("cmu-electroshock-plant-fail-here"), user, user, PopupType.SmallCaution);
                return false;
            }
        }

        return true;
    }

    private void OnPlantDoAfter(Entity<CMUElectroshockComponent> ent, ref CMUElectroshockPlantDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || ent.Comp.MinePrototype is not { } minePrototype)
            return;

        args.Handled = true;
        if (_net.IsClient || !CanPlant(ent, args.User))
            return;

        var coords = _transform.GetMoverCoordinates(args.User);
        var mine = Spawn(minePrototype, coords);
        var xform = Transform(mine);
        _transform.SetLocalPosition(mine, xform.LocalPosition + new Vector2(_random.NextFloat(-0.15f, 0.15f), _random.NextFloat(-0.15f, 0.15f)), xform);
        _transform.AnchorEntity(mine, xform);
        // Items stop colliding once anchored and asleep, which would stop the step trigger from ever firing.
        _collisionWake.SetEnabled(mine, false);
        _physics.SetBodyType(mine, BodyType.Static);

        if (TryComp(mine, out RMCLandmineComponent? landmine))
        {
            var factions = new HashSet<EntProtoId<IFFFactionComponent>>();
            var iffEvent = new GetIFFFactionEvent(SlotFlags.IDCARD | SlotFlags.BELT | SlotFlags.POCKET, factions);
            RaiseLocalEvent(args.User, ref iffEvent);
            landmine.Factions.Clear();
            landmine.Factions.UnionWith(factions);
            if (TryComp(mine, out CMUElectroshockComponent? mineShock))
            {
                mineShock.Factions.Clear();
                mineShock.Factions.UnionWith(factions);
                Dirty(mine, mineShock);
            }

            landmine.Armed = true;
            Dirty(mine, landmine);
            _appearance.SetData(mine, ToggleableVisuals.Enabled, true);
        }

        _popup.PopupEntity(Loc.GetString("cmu-electroshock-plant-finish", ("grenade", ent)), args.User, args.User);
        QueueDel(ent);
    }
}
