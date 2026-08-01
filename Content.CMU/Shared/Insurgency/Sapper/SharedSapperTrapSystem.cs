using Content.Shared._CMU14.Threats.Mobs.CLF;
using Content.Shared._RMC14.Map;
using Content.Shared.DoAfter;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.StepTrigger.Systems;
using Content.Shared.Tools.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared._AU14.Insurgency.Sapper;

/// <summary>
///     Shared behavior for CLF Sapper traps: planting, disarming, and gating who can set one off. The
///     actual "what it does when tripped" is handled by the ordinary trigger components on the trap
///     prototype (for example TriggerOnStepTrigger + ExplodeOnTrigger), which fire only after this
///     system lets the step-trigger through.
///
///     Arming is scheduled once by the server subclass when deployment completes. Hiding/revealing is a purely
///     per-viewer, client-side decision in SapperTrapVisualsSystem.
/// </summary>
public abstract class SharedSapperTrapSystem : EntitySystem
{
    [Dependency] private   CollisionWakeSystem _collisionWake = default!;
    [Dependency] private   SharedContainerSystem _container = default!;
    [Dependency] private   SharedDoAfterSystem _doAfter = default!;
    [Dependency] private   SharedHandsSystem _hands = default!;
    [Dependency] private   SharedPhysicsSystem _physics = default!;
    [Dependency] private   SharedPopupSystem _popup = default!;
    [Dependency] private   SharedToolSystem _tool = default!;
    [Dependency] private   RMCMapSystem _rmcMap = default!;

    [Dependency] private   INetManager _net = default!;

    [Dependency] protected SharedTransformSystem Transforms = default!;
    [Dependency] protected IGameTiming Timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SapperTrapComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<SapperTrapComponent, SapperTrapDeployDoAfterEvent>(OnDeployDoAfter);
        SubscribeLocalEvent<SapperTrapComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<SapperTrapComponent, SapperTrapDisarmDoAfterEvent>(OnDisarmDoAfter);
        SubscribeLocalEvent<SapperTrapComponent, StepTriggerAttemptEvent>(OnStepTriggerAttempt);
    }

    // ----- planting -------------------------------------------------------

    private void OnUseInHand(Entity<SapperTrapComponent> ent, ref UseInHandEvent args)
    {
        // A trap-specific system (e.g. the tripwire's payload check) can veto the plant by handling this first.
        if (args.Handled)
            return;

        if (ent.Comp.Deployed)
            return;

        if (!CanDeployPopup(ent, args.User))
            return;

        var doAfter = new DoAfterArgs(EntityManager,
            args.User,
            ent.Comp.DeployTime,
            new SapperTrapDeployDoAfterEvent(),
            ent,
            ent)
        {
            NeedHand = true,
            BreakOnMove = true,
            BreakOnHandChange = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        // While the sapper works, show everyone a faint hue over the area the trigger will cover.
        // Server-only: the preview is an ordinary networked entity so clients just render it.
        if (_net.IsServer && ent.Comp.DeployPreviewPrototype is { } previewProto)
        {
            var mover = Transforms.GetMoverCoordinateRotation(args.User, Transform(args.User));
            var preview = Spawn(previewProto, mover.Coords);
            Transforms.SetLocalRotation(preview, mover.worldRot.GetCardinalDir().ToAngle());
            ent.Comp.DeployPreview = preview;
        }
    }

    private void OnDeployDoAfter(Entity<SapperTrapComponent> ent, ref SapperTrapDeployDoAfterEvent args)
    {
        // The preview only lives for the duration of the do-after, however it ended.
        if (_net.IsServer && ent.Comp.DeployPreview is { } preview)
        {
            QueueDel(preview);
            ent.Comp.DeployPreview = null;
        }

        if (args.Cancelled || args.Handled)
            return;

        if (!CanDeployPopup(ent, args.User))
            return;

        args.Handled = true;

        // Plant it on the user's tile, cardinally aligned, and lock it down like a mine.
        var mover = Transforms.GetMoverCoordinateRotation(args.User, Transform(args.User));
        var xform = Transform(ent);
        Transforms.SetCoordinates(ent, xform, mover.Coords, mover.worldRot.GetCardinalDir().ToAngle());
        Transforms.AnchorEntity(ent, xform);
        _physics.SetBodyType(ent, BodyType.Static);
        // Keep the planted body awake, otherwise it sleeps and never registers someone stepping on it.
        _collisionWake.SetEnabled(ent, false);

        ent.Comp.Deployed = true;
        ent.Comp.Armed = false;
        ent.Comp.ArmsAt = Timing.CurTime + TimeSpan.FromSeconds(ent.Comp.ArmingDelay);
        Dirty(ent);

        if (_net.IsServer)
            ScheduleArming(ent, ent.Comp.ArmsAt.Value);

        _popup.PopupClient(Loc.GetString("insfor-sapper-trap-deployed"), ent, args.User);
    }

    /// <summary>Server hook for scheduling the one arming transition without scanning every trap each tick.</summary>
    protected virtual void ScheduleArming(Entity<SapperTrapComponent> ent, TimeSpan armsAt)
    {
    }

    private bool CanDeployPopup(Entity<SapperTrapComponent> ent, EntityUid user)
    {
        // Only a trained sapper knows how to set these up.
        if (!HasComp<SapperComponent>(user))
        {
            _popup.PopupClient(Loc.GetString("insfor-sapper-trap-unskilled"), user, user, PopupType.SmallCaution);
            return false;
        }

        if (_container.IsEntityInContainer(user))
        {
            _popup.PopupClient(Loc.GetString("insfor-sapper-trap-deploy-container"), user, user, PopupType.SmallCaution);
            return false;
        }

        // One trap per tile, mirroring the mine deploy rule.
        var coords = Transforms.GetMoverCoordinateRotation(user, Transform(user)).Coords;
        var query = _rmcMap.GetAnchoredEntitiesEnumerator(coords);
        while (query.MoveNext(out var anchored))
        {
            if (anchored == ent.Owner)
                continue;

            if (HasComp<SapperTrapComponent>(anchored))
            {
                _popup.PopupClient(Loc.GetString("insfor-sapper-trap-deploy-occupied"), user, user, PopupType.SmallCaution);
                return false;
            }
        }

        return true;
    }

    // ----- disarming ------------------------------------------------------

    private void OnInteractUsing(Entity<SapperTrapComponent> ent, ref InteractUsingEvent args)
    {
        if (!ent.Comp.Deployed || args.Handled)
            return;

        if (!_tool.HasQuality(args.Used, ent.Comp.DisarmTool))
            return;

        args.Handled = true;

        var doAfter = new DoAfterArgs(EntityManager,
            args.User,
            ent.Comp.DisarmTime,
            new SapperTrapDisarmDoAfterEvent(),
            ent,
            ent,
            args.Used)
        {
            NeedHand = true,
            BreakOnMove = true,
            BreakOnHandChange = true,
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnDisarmDoAfter(Entity<SapperTrapComponent> ent, ref SapperTrapDisarmDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        // Pop it back into a carryable item.
        Transforms.Unanchor(ent);
        _physics.SetBodyType(ent, BodyType.Dynamic);
        _collisionWake.SetEnabled(ent, true);
        ent.Comp.Deployed = false;
        ent.Comp.Armed = false;
        ent.Comp.ArmsAt = null;
        Dirty(ent);

        if (TryComp(args.User, out HandsComponent? hands))
            _hands.TryPickupAnyHand(args.User, ent, handsComp: hands);

        _popup.PopupClient(Loc.GetString("insfor-sapper-trap-disarmed"), ent, args.User);
    }

    // ----- who can trip it ------------------------------------------------

    private void OnStepTriggerAttempt(Entity<SapperTrapComponent> ent, ref StepTriggerAttemptEvent args)
    {
        // Only an armed trap fires, and never for the cell that laid it.
        args.Continue = ent.Comp.Armed && !IsFriendly(args.Tripper);
    }

    /// <summary>
    ///     CLF members (and, by extension, every INSFOR faction member, who all carry the CLF marker) are
    ///     immune, so the cell can walk its own minefield.
    /// </summary>
    protected bool IsFriendly(EntityUid uid) => HasComp<CLFMemberComponent>(uid);
}

/// <summary>
///     DoAfter for planting a sapper trap.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class SapperTrapDeployDoAfterEvent : SimpleDoAfterEvent
{
}

/// <summary>
///     DoAfter for disarming a sapper trap with a tool.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class SapperTrapDisarmDoAfterEvent : SimpleDoAfterEvent
{
}
