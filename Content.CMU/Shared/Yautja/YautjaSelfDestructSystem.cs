using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Explosion;
using Content.Shared.Administration.Logs;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Database;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.Yautja;

public sealed partial class YautjaSelfDestructSystem : EntitySystem
{
    [Dependency] private ISharedAdminLogManager _adminLog = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedRMCExplosionSystem _rmcExplosion = default!;
    [Dependency] private SharedRMCActionsSystem _rmcActions = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaBracerComponent, YautjaSelfDestructActionEvent>(OnSelfDestructAction);
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

        if (ent.Comp.SelfDestructArmed)
        {
            TryCancelSelfDestruct(ent, args.Performer);
            return;
        }

        TryArmSelfDestruct(ent, args.Performer);
    }

    public bool TryArmSelfDestruct(Entity<YautjaBracerComponent> bracer, EntityUid user, TimeSpan? delayOverride = null)
    {
        if (!CanUseSelfDestruct(bracer, user))
            return false;

        var now = _timing.CurTime;
        var delay = delayOverride ?? bracer.Comp.SelfDestructDelay;
        bracer.Comp.SelfDestructArmed = true;
        bracer.Comp.SelfDestructAt = now + delay;
        bracer.Comp.NextSelfDestructWarning = now;
        Dirty(bracer);

        _popup.PopupEntity(Loc.GetString("cmu-yautja-self-destruct-armed", ("seconds", (int) delay.TotalSeconds)), user, user, PopupType.LargeCaution);
        _audio.PlayPvs(bracer.Comp.SelfDestructLaughSound, user);
        _audio.PlayPvs(bracer.Comp.SelfDestructArmSound, user);
        _adminLog.Add(LogType.Action, LogImpact.High,
            $"{ToPrettyString(user):hunter} armed Yautja bracer self-destruct {ToPrettyString(bracer.Owner):bracer}");
        return true;
    }

    public bool TryCancelSelfDestruct(Entity<YautjaBracerComponent> bracer, EntityUid user)
    {
        if (!CanUseSelfDestruct(bracer, user) || !bracer.Comp.SelfDestructArmed)
            return false;

        bracer.Comp.SelfDestructArmed = false;
        bracer.Comp.SelfDestructAt = TimeSpan.Zero;
        bracer.Comp.NextSelfDestructWarning = TimeSpan.Zero;
        Dirty(bracer);

        _popup.PopupEntity(Loc.GetString("cmu-yautja-self-destruct-cancelled"), user, user);
        _audio.PlayPvs(bracer.Comp.SelfDestructCancelSound, user);
        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(user):hunter} cancelled Yautja bracer self-destruct {ToPrettyString(bracer.Owner):bracer}");
        return true;
    }

    private void Detonate(Entity<YautjaBracerComponent> bracer)
    {
        if (!bracer.Comp.SelfDestructArmed)
            return;

        bracer.Comp.SelfDestructArmed = false;
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
            bracer.Comp.SelfDestructTotalIntensity,
            bracer.Comp.SelfDestructIntensitySlope,
            bracer.Comp.SelfDestructMaxIntensity,
            user ?? bracer.Owner,
            maxTileBreak: bracer.Comp.SelfDestructMaxTileBreak,
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

    private bool CanUseSelfDestruct(Entity<YautjaBracerComponent> bracer, EntityUid user)
    {
        if (!HasComp<YautjaComponent>(user) || bracer.Comp.User != user)
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-tech-denied"), user, user, PopupType.SmallCaution);
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
}
