using Content.Shared._RMC14.Stun;
using System.Numerics;
using Content.Shared.DoAfter;
using Content.Shared._RMC14.Sprite;
using Content.Shared._RMC14.Xenonids.Evolution;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Verbs;
using Robust.Shared.Network;
using Content.Shared._RMC14.Pulling;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Pulling.Events;

namespace Content.Shared._RMC14.Xenonids.Charge.ChargerJockey;

public sealed partial class XenoChargerJockeySystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedRMCSpriteSystem _rmcSprite = default!;
    [Dependency] private RMCPullingSystem _rmcPulling = default!;

    public override void Initialize()
    {
        UpdatesAfter.Add(typeof(SharedMoverController));

        SubscribeLocalEvent<NewXenoEvolvedEvent>(OnNewXenoEvolved);
        SubscribeLocalEvent<XenoDevolvedEvent>(OnXenoDevolved);

        SubscribeLocalEvent<XenoChargerJockeyComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<XenoChargerJockeyComponent, XenoJockeyDoAfterEvent>(OnDoAfter);

        SubscribeLocalEvent<XenoChargerRidingComponent, MoveInputEvent>(OnRiderMoveInput);
        SubscribeLocalEvent<XenoChargerRidingComponent, StartPullAttemptEvent>(OnRiderStartPullAttempt);
        SubscribeLocalEvent<XenoChargerRidingComponent, PullAttemptEvent>(OnRiderPullAttempt);
        SubscribeLocalEvent<XenoChargerRidingComponent, ComponentShutdown>(OnRiderShutdown);
        SubscribeLocalEvent<XenoChargerRidingComponent, ChangeDirectionAttemptEvent>(OnRiderChangeDirectionAttempt);

        SubscribeLocalEvent<XenoChargerJockeyComponent, ComponentShutdown>(OnChargerShutdown);
        SubscribeLocalEvent<XenoChargerJockeyComponent, EntityTerminatingEvent>(OnChargerTerminating);
        SubscribeLocalEvent<XenoChargerJockeyComponent, MobStateChangedEvent>(OnChargerStateChanged);

        SubscribeLocalEvent<XenoChargerRidingComponent, StunnedEvent>(OnRiderStunned);
        SubscribeLocalEvent<XenoChargerJockeyComponent, StunnedEvent>(OnChargerStunned);
    }

    private void OnGetVerbs(Entity<XenoChargerJockeyComponent> charger, ref GetVerbsEvent<AlternativeVerb> args)
    {
        var user = args.User;
        if (!args.CanAccess || !args.CanInteract || !CanMount(user, charger))
            return;

        var verb = new AlternativeVerb
        {
            Text = Loc.GetString("rmc-xeno-jockey-verb"),
            Priority = 1,
            Act = () =>
            {
                var ev = new XenoJockeyDoAfterEvent();
                var doAfter = new DoAfterArgs(EntityManager, user, charger.Comp.MountDoAfter, ev, charger.Owner, charger.Owner)
                {
                    BreakOnMove = true,
                    BreakOnDamage = false,
                    DuplicateCondition = DuplicateConditions.SameTarget | DuplicateConditions.SameEvent,
                    NeedHand = false,
                };

                if (!_doAfter.TryStartDoAfter(doAfter))
                    return;

                var chargerName = Identity.Entity(charger.Owner, EntityManager);
                var riderName = Identity.Entity(user, EntityManager);
                var selfMessage = Loc.GetString("rmc-xeno-jockey-start-self", ("charger", chargerName));
                var othersMessage = Loc.GetString("rmc-xeno-jockey-start-others", ("rider", riderName), ("charger", chargerName));
                _popup.PopupPredicted(selfMessage, othersMessage, user, user);
            }
        };

        args.Verbs.Add(verb);
    }

    private void OnNewXenoEvolved(ref NewXenoEvolvedEvent args)
    {
        DismountAll(args.OldXeno.Owner);
    }

    private void OnXenoDevolved(ref XenoDevolvedEvent args)
    {
        DismountAll(args.OldXeno);
    }

    private bool CanMount(EntityUid rider, Entity<XenoChargerJockeyComponent> charger)
    {
        if (rider == charger.Owner)
            return false;

        if (!TryComp(rider, out RMCSizeComponent? userSize) ||
            userSize.Size is not (RMCSizes.VerySmallXeno or RMCSizes.SmallXeno))
        {
            return false;
        }

        if (HasComp<XenoChargerRidingComponent>(rider))
            return false;

        if (GetActiveRiderCount(charger.Owner, charger.Comp) >= GetMaxRiders(charger.Comp))
            return false;

        if (_mobState.IsDead(charger) || _mobState.IsDead(rider))
            return false;

        return true;
    }

    private void OnDoAfter(Entity<XenoChargerJockeyComponent> charger, ref XenoJockeyDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        var rider = args.User;

        args.Handled = true;

        if (!CanMount(rider, charger))
            return;

        Mount(rider, charger.Owner, charger.Comp);
    }

    private void Mount(EntityUid rider, EntityUid charger, XenoChargerJockeyComponent comp)
    {
        if (!_net.IsServer)
            return;

        var riderSlot = GetOpenRiderSlot(charger, comp);
        var riderLocalPosition = GetRiderLocalPosition(comp, riderSlot);

        _rmcPulling.TryStopAllPullsFromAndOn(rider);

        var riding = EnsureComp<XenoChargerRidingComponent>(rider);
        riding.Charger = charger;
        riding.LocalPosition = riderLocalPosition;
        riding.RiderSlot = riderSlot;
        riding.DrawDepth = comp.RiderDrawDepth;
        Dirty(rider, riding);

        comp.Riders.Add(rider);
        Dirty(charger, comp);

        // Parent the rider to the charger so they move together.
        _transform.SetParent(rider, charger);
        _transform.SetLocalPosition(rider, riderLocalPosition);
        _transform.SetLocalRotation(rider, Angle.Zero);
        _rmcSprite.SetRenderOrder(rider, comp.RiderRenderOrder);

        if (_net.IsServer)
        {
            var chargerName = Identity.Entity(charger, EntityManager);
            var riderName = Identity.Entity(rider, EntityManager);
            _popup.PopupEntity(Loc.GetString("rmc-xeno-jockey-mount", ("rider", riderName), ("charger", chargerName)), rider, PopupType.Small);
        }
    }

    private int GetActiveRiderCount(EntityUid charger, XenoChargerJockeyComponent comp)
    {
        var count = 0;
        foreach (var rider in comp.Riders)
        {
            if (TerminatingOrDeleted(rider) ||
                !TryComp(rider, out XenoChargerRidingComponent? riding) ||
                riding.Charger != charger)
            {
                continue;
            }

            count++;
        }

        return count;
    }

    private static int GetMaxRiders(XenoChargerJockeyComponent comp)
    {
        if (comp.RiderLocalPositions.Count == 0)
            return comp.MaxRiders;

        return Math.Min(comp.MaxRiders, comp.RiderLocalPositions.Count);
    }

    private int GetOpenRiderSlot(EntityUid charger, XenoChargerJockeyComponent comp)
    {
        if (comp.RiderLocalPositions.Count == 0)
            return -1;

        var maxRiders = GetMaxRiders(comp);
        for (var slot = 0; slot < maxRiders; slot++)
        {
            if (!IsRiderSlotOccupied(charger, comp, slot))
                return slot;
        }

        return 0;
    }

    private bool IsRiderSlotOccupied(EntityUid charger, XenoChargerJockeyComponent comp, int slot)
    {
        foreach (var rider in comp.Riders)
        {
            if (TerminatingOrDeleted(rider) ||
                !TryComp(rider, out XenoChargerRidingComponent? riding) ||
                riding.Charger != charger)
            {
                continue;
            }

            if (riding.RiderSlot == slot)
                return true;
        }

        return false;
    }

    private static Vector2 GetRiderLocalPosition(XenoChargerJockeyComponent comp, int slot)
    {
        if (slot >= 0 && slot < comp.RiderLocalPositions.Count)
            return comp.RiderLocalPositions[slot];

        return comp.RiderLocalPosition;
    }

    private void Dismount(EntityUid rider, EntityUid charger)
    {
        if (!_net.IsServer)
            return;

        if (TryComp(charger, out XenoChargerJockeyComponent? comp))
        {
            comp.Riders.Remove(rider);
            if (!TerminatingOrDeleted(charger))
                Dirty(charger, comp);
        }

        RemComp<XenoChargerRidingComponent>(rider);
        _rmcSprite.SetRenderOrder(rider, 0);

        // Unparent — drop rider at current world position.
        _transform.AttachToGridOrMap(rider);
    }

    private void DismountAll(EntityUid charger)
    {
        if (!TryComp(charger, out XenoChargerJockeyComponent? comp))
            return;

        DismountAll((charger, comp));
    }

    private void DismountAll(Entity<XenoChargerJockeyComponent> charger)
    {
        foreach (var rider in new List<EntityUid>(charger.Comp.Riders))
        {
            if (TerminatingOrDeleted(rider))
                continue;

            Dismount(rider, charger.Owner);
        }
    }

    private void OnRiderMoveInput(Entity<XenoChargerRidingComponent> rider, ref MoveInputEvent args)
    {
        // Any directional input dismounts.
        if ((args.Entity.Comp.HeldMoveButtons & MoveButtons.AnyDirection) == 0)
            return;

        Dismount(rider.Owner, rider.Comp.Charger);
    }

    private void OnRiderStartPullAttempt(Entity<XenoChargerRidingComponent> rider, ref StartPullAttemptEvent args)
    {
        if (args.Puller != rider.Owner)
            return;

        args.Cancel();
    }

    private void OnRiderPullAttempt(Entity<XenoChargerRidingComponent> rider, ref PullAttemptEvent args)
    {
        if (args.PullerUid != rider.Owner)
            return;

        args.Cancelled = true;
    }

    private void OnRiderChangeDirectionAttempt(Entity<XenoChargerRidingComponent> rider, ref ChangeDirectionAttemptEvent args)
    {
        args.Cancel();
    }

    private void OnRiderShutdown(Entity<XenoChargerRidingComponent> rider, ref ComponentShutdown args)
    {
        // Clean up charger side if rider component is removed for any reason.
        if (TryComp(rider.Comp.Charger, out XenoChargerJockeyComponent? comp))
        {
            comp.Riders.Remove(rider.Owner);
            if (!TerminatingOrDeleted(rider.Comp.Charger))
                Dirty(rider.Comp.Charger, comp);
        }

        if (_net.IsServer && !TerminatingOrDeleted(rider.Owner))
        {
            _rmcSprite.SetRenderOrder(rider.Owner, 0);
            _transform.AttachToGridOrMap(rider.Owner);
        }
    }

    private void OnChargerShutdown(Entity<XenoChargerJockeyComponent> charger, ref ComponentShutdown args)
    {
        DismountAll(charger);
    }

    private void OnChargerTerminating(Entity<XenoChargerJockeyComponent> charger, ref EntityTerminatingEvent args)
    {
        DismountAll(charger);
    }

    private void OnChargerStateChanged(Entity<XenoChargerJockeyComponent> charger, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        foreach (var rider in new List<EntityUid>(charger.Comp.Riders))
        {
            if (TerminatingOrDeleted(rider))
                continue;

            Dismount(rider, charger.Owner);
        }
    }

    private void OnRiderStunned(Entity<XenoChargerRidingComponent> rider, ref StunnedEvent args)
    {
        Dismount(rider.Owner, rider.Comp.Charger);
    }

    private void OnChargerStunned(Entity<XenoChargerJockeyComponent> charger, ref StunnedEvent args)
    {
        foreach (var rider in new List<EntityUid>(charger.Comp.Riders))
        {
            if (TerminatingOrDeleted(rider))
                continue;

            Dismount(rider, charger.Owner);
        }
    }

    public override void Update(float frameTime)
    {
        if (!_net.IsServer)
            return;

        var query = EntityQueryEnumerator<XenoChargerRidingComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var riding, out var xform))
        {
            if (xform.ParentUid != riding.Charger)
                continue;

            if (xform.LocalPosition != riding.LocalPosition)
                _transform.SetLocalPosition(uid, riding.LocalPosition, xform);

            if (xform.LocalRotation != Angle.Zero)
                _transform.SetLocalRotation(uid, Angle.Zero, xform);
        }
    }
}
