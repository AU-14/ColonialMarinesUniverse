using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Dialog;
using Content.Shared._RMC14.Explosion;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared.Administration.Logs;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Database;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Inventory;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.Yautja;

public sealed partial class YautjaSelfDestructSystem : EntitySystem
{
    private static readonly TimeSpan SelfDestructDialogTimeout = TimeSpan.FromSeconds(20);

    [Dependency] private ISharedAdminLogManager _adminLog = default!;
    [Dependency] private AreaSystem _area = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private DialogSystem _dialog = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedRMCExplosionSystem _rmcExplosion = default!;
    [Dependency] private SharedRMCActionsSystem _rmcActions = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public Func<Entity<YautjaBracerComponent>, EntityUid, bool>? TryStartNonTechMisuse;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaBracerComponent, YautjaSelfDestructActionEvent>(OnSelfDestructAction);
        SubscribeLocalEvent<YautjaBracerComponent, YautjaSelfDestructConfirmArmEvent>(OnSelfDestructConfirmArm);
        SubscribeLocalEvent<YautjaBracerComponent, YautjaSelfDestructConfirmCancelEvent>(OnSelfDestructConfirmCancel);
        SubscribeLocalEvent<YautjaBracerComponent, YautjaSelfDestructConfirmRemoteDeadVictimEvent>(OnSelfDestructConfirmRemoteDeadVictim);
        SubscribeLocalEvent<MobStateChangedEvent>(OnAnyMobStateChanged);
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<YautjaBracerComponent>();
        while (query.MoveNext(out var uid, out var bracer))
        {
            if (!bracer.SelfDestructArmed)
                continue;

            if (now >= bracer.SelfDestructAt)
            {
                Detonate((uid, bracer));
                continue;
            }

            if (bracer.User is not { } user || now < bracer.NextSelfDestructWarning)
                continue;

            var seconds = Math.Max(1, (int) Math.Ceiling((bracer.SelfDestructAt - now).TotalSeconds));
            _popup.PopupEntity(Loc.GetString("cmu-yautja-self-destruct-warning", ("seconds", seconds)), user, user, PopupType.LargeCaution);
            _audio.PlayPvs(bracer.SelfDestructWarningSound, user);
            bracer.NextSelfDestructWarning = now + bracer.SelfDestructWarningEvery;
            Dirty(uid, bracer);
        }
    }

    private void OnSelfDestructAction(Entity<YautjaBracerComponent> ent, ref YautjaSelfDestructActionEvent args)
    {
        if (args.Handled || _net.IsClient)
            return;

        if (!_rmcActions.TryUseAction(args))
            return;

        args.Handled = true;

        if (TryGetPulledDeadVictim(args.Performer, out var victim))
        {
            TryOpenRemoteDeadVictimSelfDestructDialog(ent, args.Performer, victim);
            return;
        }

        TryOpenSelfDestructDialog(ent, args.Performer);
    }

    private void OnSelfDestructConfirmArm(Entity<YautjaBracerComponent> ent, ref YautjaSelfDestructConfirmArmEvent args)
    {
        if (!TryGetEntity(args.User, out var user))
            return;

        TryArmSelfDestruct(ent, user.Value);
    }

    private void OnSelfDestructConfirmCancel(Entity<YautjaBracerComponent> ent, ref YautjaSelfDestructConfirmCancelEvent args)
    {
        if (!TryGetEntity(args.User, out var user))
            return;

        TryCancelSelfDestruct(ent, user.Value);
    }

    private void OnSelfDestructConfirmRemoteDeadVictim(
        Entity<YautjaBracerComponent> ent,
        ref YautjaSelfDestructConfirmRemoteDeadVictimEvent args)
    {
        if (!TryGetEntity(args.User, out var user) ||
            !TryGetEntity(args.Victim, out var victim) ||
            !TryGetEntity(args.VictimBracer, out var victimBracer))
        {
            return;
        }

        TryRemoteDetonateDeadVictimSelfDestruct(ent, user.Value, victim.Value, victimBracer.Value);
    }

    private void OnAnyMobStateChanged(MobStateChangedEvent args)
    {
        if (_net.IsClient ||
            args.NewMobState != MobState.Dead)
        {
            return;
        }

        var query = EntityQueryEnumerator<YautjaBracerComponent>();
        while (query.MoveNext(out var uid, out var bracer))
        {
            if (!bracer.AutoSelfDestructOnUserDeath ||
                bracer.User != args.Target ||
                bracer.SelfDestructArmed ||
                TerminatingOrDeleted(uid))
            {
                continue;
            }

            ArmSelfDestruct((uid, bracer), args.Target, args.Target, false, notifyYautja: false);
        }
    }

    public bool TryOpenSelfDestructDialog(Entity<YautjaBracerComponent> bracer, EntityUid user)
    {
        if (!HasComp<YautjaComponent>(user) && bracer.Comp.User == user)
            return TryStartNonTechMisuse?.Invoke(bracer, user) ?? false;

        if (bracer.Comp.SelfDestructArmed
                ? !CanCancelSelfDestruct(bracer, user)
                : !CanArmSelfDestruct(bracer, user))
            return false;

        var confirmEvent = bracer.Comp.SelfDestructArmed
            ? new YautjaSelfDestructConfirmCancelEvent(GetNetEntity(user))
            : (object) new YautjaSelfDestructConfirmArmEvent(GetNetEntity(user));
        var message = bracer.Comp.SelfDestructArmed
            ? Loc.GetString("cmu-yautja-self-destruct-cancel-confirm")
            : Loc.GetString("cmu-yautja-self-destruct-arm-confirm");
        var options = new List<DialogOption>
        {
            new(Loc.GetString("cmu-yautja-self-destruct-confirm-yes"), confirmEvent),
            new(Loc.GetString("cmu-yautja-self-destruct-confirm-no")),
        };

        _dialog.OpenOptions(
            bracer.Owner,
            user,
            Loc.GetString("cmu-yautja-self-destruct-dialog-title"),
            options,
            message);
        return true;
    }

    public bool TryOpenRemoteDeadVictimSelfDestructDialog(
        Entity<YautjaBracerComponent> bracer,
        EntityUid user,
        EntityUid victim)
    {
        if (!CanUseRemoteDeadVictimSelfDestruct(bracer, user, victim, null, out var victimBracer))
            return false;

        var confirmEvent = new YautjaSelfDestructConfirmRemoteDeadVictimEvent(
            GetNetEntity(user),
            GetNetEntity(victim),
            GetNetEntity(victimBracer.Owner));
        var message = HasComp<YautjaComponent>(victim)
            ? Loc.GetString("cmu-yautja-self-destruct-remote-confirm-yautja", ("species", SpeciesName(victim)))
            : Loc.GetString("cmu-yautja-self-destruct-remote-confirm", ("species", SpeciesName(victim)));
        var options = new List<DialogOption>
        {
            new(Loc.GetString("cmu-yautja-self-destruct-confirm-yes"), confirmEvent),
            new(Loc.GetString("cmu-yautja-self-destruct-confirm-no")),
        };

        _dialog.OpenOptions(
            bracer.Owner,
            user,
            Loc.GetString("cmu-yautja-self-destruct-dialog-title"),
            options,
            message);
        return true;
    }

    public bool TryArmSelfDestruct(Entity<YautjaBracerComponent> bracer, EntityUid user, TimeSpan? delayOverride = null)
    {
        if (!CanArmSelfDestruct(bracer, user))
            return false;

        ArmSelfDestruct(bracer, user, user, false, delayOverride);
        return true;
    }

    private void ArmSelfDestruct(
        Entity<YautjaBracerComponent> bracer,
        EntityUid user,
        EntityUid victim,
        bool remote,
        TimeSpan? delayOverride = null,
        bool notifyYautja = true)
    {
        var now = _timing.CurTime;
        var delay = delayOverride ?? TimeSpan.FromMilliseconds(_random.Next(72, 81) * 100);
        bracer.Comp.SelfDestructArmed = true;
        bracer.Comp.SelfDestructAt = now + delay;
        bracer.Comp.NextSelfDestructWarning = now;
        Dirty(bracer);

        if (notifyYautja)
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-self-destruct-armed", ("seconds", (int) delay.TotalSeconds)), victim, victim, PopupType.LargeCaution);
            BroadcastToYautja(Loc.GetString("cmu-yautja-self-destruct-broadcast-armed", ("hunter", Name(victim))));
        }

        StopSelfDestructAudio(bracer);
        bracer.Comp.SelfDestructLaughStream = _audio.PlayPvs(bracer.Comp.SelfDestructLaughSound, victim)?.Entity;
        bracer.Comp.SelfDestructArmStream = _audio.PlayPvs(bracer.Comp.SelfDestructArmSound, victim)?.Entity;
        _adminLog.Add(LogType.Action, LogImpact.High,
            $"{ToPrettyString(user):hunter} triggered their predator self-destruct sequence in {_area.GetAreaName(victim)}");

        var ev = new YautjaSelfDestructArmedEvent(bracer.Owner, user, victim, remote);
        RaiseLocalEvent(bracer.Owner, ref ev);
    }

    public bool TryRemoteDetonateDeadVictimSelfDestruct(
        Entity<YautjaBracerComponent> bracer,
        EntityUid user,
        EntityUid victim,
        EntityUid expectedVictimBracer,
        TimeSpan? delayOverride = null)
    {
        if (!CanUseRemoteDeadVictimSelfDestruct(bracer, user, victim, expectedVictimBracer, out var victimBracer))
            return false;

        var now = _timing.CurTime;
        var delay = delayOverride ?? TimeSpan.FromMilliseconds(_random.Next(72, 81) * 100);
        victimBracer.Comp.SelfDestructArmed = true;
        victimBracer.Comp.SelfDestructAt = now + delay;
        victimBracer.Comp.NextSelfDestructWarning = now;
        Dirty(victimBracer);

        StopSelfDestructAudio(victimBracer);
        victimBracer.Comp.SelfDestructArmStream = _audio.PlayPvs(victimBracer.Comp.SelfDestructArmSound, victim)?.Entity;
        _popup.PopupEntity(
            Loc.GetString("cmu-yautja-self-destruct-remote-armed", ("victim", Name(victim))),
            user,
            user,
            PopupType.LargeCaution);
        BroadcastToYautja(Loc.GetString(
            "cmu-yautja-self-destruct-broadcast-remote-armed",
            ("hunter", Name(user)),
            ("victim", Name(victim))));
        _adminLog.Add(LogType.Action, LogImpact.High,
            $"{ToPrettyString(user):hunter} triggered the predator self-destruct sequence of {ToPrettyString(victim):victim} in {_area.GetAreaName(user)}");

        var ev = new YautjaSelfDestructArmedEvent(victimBracer.Owner, user, victim, true);
        RaiseLocalEvent(victimBracer.Owner, ref ev);
        return true;
    }

    public bool TryCancelSelfDestruct(Entity<YautjaBracerComponent> bracer, EntityUid user)
    {
        if (!CanCancelSelfDestruct(bracer, user) || !bracer.Comp.SelfDestructArmed)
            return false;

        bracer.Comp.SelfDestructArmed = false;
        bracer.Comp.SelfDestructAt = TimeSpan.Zero;
        bracer.Comp.NextSelfDestructWarning = TimeSpan.Zero;
        StopSelfDestructAudio(bracer);
        Dirty(bracer);

        _popup.PopupEntity(Loc.GetString("cmu-yautja-self-destruct-cancelled"), user, user);
        BroadcastToYautja(Loc.GetString("cmu-yautja-self-destruct-broadcast-cancelled", ("hunter", Name(user))));
        _audio.PlayPvs(bracer.Comp.SelfDestructCancelSound, user);
        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(user):hunter} has deactivated their Self-Destruct.");
        return true;
    }

    private void BroadcastToYautja(string message)
    {
        if (_net.IsClient)
            return;

        var query = EntityQueryEnumerator<YautjaComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (!Deleted(uid))
                _popup.PopupEntity(message, uid, uid, PopupType.Medium);
        }
    }

    private void Detonate(Entity<YautjaBracerComponent> bracer)
    {
        if (!bracer.Comp.SelfDestructArmed)
            return;

        bracer.Comp.SelfDestructArmed = false;
        StopSelfDestructAudio(bracer);
        Dirty(bracer);

        var user = bracer.Comp.User;
        var epicenterTarget = user is { } hunter && !TerminatingOrDeleted(hunter)
            ? hunter
            : bracer.Owner;
        var epicenter = _transform.GetMapCoordinates(epicenterTarget);
        var equipment = user is { } wearer && !TerminatingOrDeleted(wearer)
            ? CollectEquipment(wearer, bracer)
            : new HashSet<EntityUid> { bracer.Owner };

        _adminLog.Add(LogType.Action, LogImpact.High,
            $"Yautja bracer self-destruct detonated from {ToPrettyString(bracer.Owner):bracer}");

        _rmcExplosion.QueueExplosion(
            epicenter,
            bracer.Comp.SelfDestructExplosion.Id,
            SelfDestructTotalIntensity(bracer.Comp),
            bracer.Comp.SelfDestructIntensitySlope,
            SelfDestructMaxIntensity(bracer.Comp),
            user ?? bracer.Owner,
            maxTileBreak: SelfDestructMaxTileBreak(
                bracer.Comp.SelfDestructExplosionType,
                bracer.Comp.SelfDestructMaxTileBreak),
            canCreateVacuum: false);

        if (user is { } victim && !TerminatingOrDeleted(victim))
        {
            if (TryComp<BodyComponent>(victim, out var body))
                _body.GibBody(victim, true, body, splatModifier: bracer.Comp.SelfDestructGibSplatModifier);
            else
                QueueDel(victim);
        }

        DestroyEquipment(equipment);
    }

    private HashSet<EntityUid> CollectEquipment(EntityUid user, Entity<YautjaBracerComponent> bracer)
    {
        var equipment = new HashSet<EntityUid>();
        foreach (var item in _inventory.GetHandOrInventoryEntities(user))
        {
            equipment.Add(item);
        }

        foreach (var tech in _lookup.GetEntitiesInRange<YautjaTechItemComponent>(
                     _transform.GetMapCoordinates(user),
                     bracer.Comp.SelfDestructEquipmentDestroyRadius))
        {
            equipment.Add(tech.Owner);
        }

        equipment.Add(bracer.Owner);
        return equipment;
    }

    private void DestroyEquipment(HashSet<EntityUid> equipment)
    {
        foreach (var item in equipment)
        {
            if (TerminatingOrDeleted(item))
                continue;

            QueueDel(item);
        }
    }

    internal static float SelfDestructTotalIntensity(YautjaBracerComponent bracer)
    {
        return bracer.SelfDestructExplosionType == YautjaSelfDestructExplosionType.Big
            ? 600
            : 800;
    }

    internal static float SelfDestructMaxIntensity(YautjaBracerComponent bracer)
    {
        return bracer.SelfDestructExplosionType == YautjaSelfDestructExplosionType.Big
            ? 50
            : 550;
    }

    internal static int SelfDestructMaxTileBreak(
        YautjaSelfDestructExplosionType type,
        int configuredSmallValue)
    {
        return type == YautjaSelfDestructExplosionType.Big ? 0 : configuredSmallValue;
    }

    private bool CanArmSelfDestruct(Entity<YautjaBracerComponent> bracer, EntityUid user)
    {
        if (!CanUseSelfDestructCommon(bracer, user))
            return false;

        if (HasComp<VictimInfectedComponent>(user) ||
            _inventory.TryGetSlotEntity(user, "mask", out var mask) &&
            HasComp<XenoParasiteComponent>(mask.Value))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-self-destruct-xeno-host"), user, user, PopupType.SmallCaution);
            return false;
        }

        return true;
    }

    private bool CanCancelSelfDestruct(Entity<YautjaBracerComponent> bracer, EntityUid user)
    {
        return CanUseSelfDestructCommon(bracer, user);
    }

    private bool CanUseRemoteDeadVictimSelfDestruct(
        Entity<YautjaBracerComponent> bracer,
        EntityUid user,
        EntityUid victim,
        EntityUid? expectedVictimBracer,
        out Entity<YautjaBracerComponent> victimBracer)
    {
        victimBracer = default;

        if (!CanUseSelfDestructCommon(bracer, user))
            return false;

        if (!TryGetPulledDeadVictim(user, out var pulled) || pulled != victim)
            return false;

        if (!_inventory.TryGetSlotEntity(victim, "gloves", out var gloves) ||
            !TryComp(gloves, out YautjaBracerComponent? foundBracer))
        {
            _popup.PopupEntity(
                Loc.GetString("cmu-yautja-self-destruct-remote-missing-bracer", ("species", SpeciesName(victim))),
                user,
                user,
                PopupType.SmallCaution);
            return false;
        }

        if (expectedVictimBracer != null && gloves.Value != expectedVictimBracer ||
            foundBracer.User != victim ||
            foundBracer.SelfDestructArmed)
        {
            return false;
        }

        victimBracer = (gloves.Value, foundBracer);
        return true;
    }

    private bool CanUseSelfDestructCommon(Entity<YautjaBracerComponent> bracer, EntityUid user)
    {
        if (!HasComp<YautjaComponent>(user) || bracer.Comp.User != user)
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-tech-denied"), user, user, PopupType.SmallCaution);
            return false;
        }

        if (IsInHuntingGrounds(user))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-self-destruct-preserve"), user, user, PopupType.SmallCaution);
            return false;
        }

        if (HasComp<YautjaYoungbloodComponent>(user))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-youngblood-self-destruct-denied"), user, user, PopupType.SmallCaution);
            return false;
        }

        if (HasComp<YautjaThrallComponent>(user))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-self-destruct-thrall-denied"), user, user, PopupType.SmallCaution);
            return false;
        }

        if (_mobState.IsCritical(user))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-self-destruct-critical"), user, user, PopupType.SmallCaution);
            return false;
        }

        if (!_mobState.IsAlive(user))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-self-destruct-dead"), user, user, PopupType.SmallCaution);
            return false;
        }

        return true;
    }

    private bool IsInHuntingGrounds(EntityUid user)
    {
        if (!TryComp(user, out TransformComponent? xform))
            return false;

        return xform.GridUid is { } grid && HasComp<YautjaHuntingGroundComponent>(grid) ||
               xform.MapUid is { } map && HasComp<YautjaHuntingGroundComponent>(map);
    }

    private bool TryGetPulledDeadVictim(EntityUid user, out EntityUid victim)
    {
        if (TryComp(user, out PullerComponent? puller) &&
            puller.Pulling is { } pulled &&
            _mobState.IsDead(pulled))
        {
            victim = pulled;
            return true;
        }

        victim = default;
        return false;
    }

    private string SpeciesName(EntityUid uid)
    {
        if (HasComp<YautjaComponent>(uid))
            return Loc.GetString("species-name-yautja");

        if (TryComp(uid, out HumanoidAppearanceComponent? humanoid) &&
            _prototype.TryIndex<SpeciesPrototype>(humanoid.Species, out var species))
        {
            return Loc.GetString(species.Name);
        }

        return Loc.GetString("humanoid-appearance-component-unknown-species");
    }

    public void StopSelfDestructAudio(Entity<YautjaBracerComponent> bracer)
    {
        bracer.Comp.SelfDestructLaughStream = _audio.Stop(bracer.Comp.SelfDestructLaughStream);
        bracer.Comp.SelfDestructArmStream = _audio.Stop(bracer.Comp.SelfDestructArmStream);
    }
}
