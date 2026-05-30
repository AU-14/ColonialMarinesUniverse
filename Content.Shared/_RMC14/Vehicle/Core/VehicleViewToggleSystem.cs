using System.Linq;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Network;

namespace Content.Shared._RMC14.Vehicle;

public sealed partial class VehicleViewToggleSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedEyeSystem _eye = default!;
    [Dependency] private INetManager _net = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<VehicleViewToggleComponent, VehicleToggleViewActionEvent>(OnToggleViewAction);
        SubscribeLocalEvent<VehicleViewToggleComponent, ComponentShutdown>(OnToggleViewShutdown);
    }

    public void EnableViewToggle(
        EntityUid user,
        EntityUid outsideTarget,
        EntityUid source,
        EntityUid? insideTarget,
        bool isOutside)
    {
        var toggle = EnsureComp<VehicleViewToggleComponent>(user);
        toggle.Sources.Add(source);
        toggle.Source = source;
        toggle.OutsideTarget = outsideTarget;
        toggle.InsideTarget = ResolveInsideTarget(user, outsideTarget, insideTarget);
        toggle.IsOutside = isOutside;

        EnsureSingleToggleAction(user, toggle);

        if (toggle.Action is { } actionUid && TryComp(actionUid, out ActionComponent? actionComp))
        {
            actionComp.ItemIconStyle = ItemActionIconStyle.BigAction;
            actionComp.EntIcon = null;
            _actions.SetTemporary((actionUid, actionComp), false);
            Dirty(actionUid, actionComp);
        }

        UpdateActionState(toggle);
        Dirty(user, toggle);
    }

    public void DisableViewToggle(EntityUid user, EntityUid source)
    {
        if (!TryComp(user, out VehicleViewToggleComponent? toggle))
            return;

        toggle.Sources.Remove(source);
        if (toggle.Sources.Count > 0)
        {
            foreach (var remaining in toggle.Sources)
            {
                toggle.Source = remaining;
                break;
            }

            EnsureSingleToggleAction(user, toggle);
            UpdateActionState(toggle);
            Dirty(user, toggle);
            return;
        }

        if (toggle.Action is { } action)
            RemoveAndDeleteToggleAction(action, user);

        toggle.Action = null;
        toggle.Source = null;
        toggle.OutsideTarget = null;
        toggle.InsideTarget = null;
        toggle.IsOutside = false;

        RemComp<VehicleViewToggleComponent>(user);
    }

    private void OnToggleViewShutdown(Entity<VehicleViewToggleComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Action is not { } action)
            return;

        RemoveAndDeleteToggleAction(action, ent.Owner);
    }

    private void OnToggleViewAction(Entity<VehicleViewToggleComponent> ent, ref VehicleToggleViewActionEvent args)
    {
        if (args.Handled || args.Performer != ent.Owner)
            return;

        args.Handled = true;

        if (ent.Comp.OutsideTarget == null || !TryComp(ent.Owner, out EyeComponent? eye))
            return;

        var outside = ent.Comp.OutsideTarget.Value;
        if (!Exists(outside) || TerminatingOrDeleted(outside))
            return;

        if (ent.Comp.IsOutside)
        {
            var inside = ResolveInsideTarget(ent.Owner, outside, ent.Comp.InsideTarget);
            if (inside == null)
                return;

            ent.Comp.InsideTarget = inside;
            _eye.SetTarget(ent.Owner, inside, eye);
            ent.Comp.IsOutside = false;
        }
        else
        {
            ent.Comp.InsideTarget = ResolveInsideTarget(ent.Owner, outside, eye.Target);
            _eye.SetTarget(ent.Owner, outside, eye);
            ent.Comp.IsOutside = true;
        }

        EnsureSingleToggleAction(ent.Owner, ent.Comp);
        UpdateActionState(ent.Comp);
        Dirty(ent.Owner, ent.Comp);
        RaiseLocalEvent(ent.Owner, new VehicleViewToggledEvent(ent.Comp.IsOutside));
    }

    public void RefreshOutsideViewers(EntityUid outsideTarget)
    {
        if (!Exists(outsideTarget) || TerminatingOrDeleted(outsideTarget))
            return;

        var query = EntityQueryEnumerator<VehicleViewToggleComponent, EyeComponent>();
        while (query.MoveNext(out var user, out var toggle, out var eye))
        {
            if (!toggle.IsOutside || toggle.OutsideTarget != outsideTarget)
                continue;

            _eye.SetTarget(user, outsideTarget, eye);
        }
    }

    public bool SetOutsideView(EntityUid user, EntityUid outsideTarget)
    {
        if (!TryComp(user, out VehicleViewToggleComponent? toggle) ||
            toggle.OutsideTarget != outsideTarget ||
            !TryComp(user, out EyeComponent? eye))
        {
            return false;
        }

        if (!Exists(outsideTarget) || TerminatingOrDeleted(outsideTarget))
            return false;

        toggle.InsideTarget = ResolveInsideTarget(user, outsideTarget, eye.Target);
        _eye.SetTarget(user, outsideTarget, eye);
        toggle.IsOutside = true;

        EnsureSingleToggleAction(user, toggle);
        UpdateActionState(toggle);
        Dirty(user, toggle);
        RaiseLocalEvent(user, new VehicleViewToggledEvent(true));
        return true;
    }

    public void ReplaceOutsideTarget(EntityUid oldOutsideTarget, EntityUid newOutsideTarget)
    {
        if (!Exists(newOutsideTarget) || TerminatingOrDeleted(newOutsideTarget))
            return;

        var query = EntityQueryEnumerator<VehicleViewToggleComponent, EyeComponent>();
        while (query.MoveNext(out var user, out var toggle, out var eye))
        {
            if (toggle.OutsideTarget != oldOutsideTarget)
                continue;

            toggle.OutsideTarget = newOutsideTarget;
            if (toggle.IsOutside)
                _eye.SetTarget(user, newOutsideTarget, eye);

            UpdateActionState(toggle);
            Dirty(user, toggle);
        }
    }

    private EntityUid? ResolveInsideTarget(EntityUid user, EntityUid outsideTarget, EntityUid? proposedTarget)
    {
        if (IsUsableInsideTarget(user, outsideTarget, proposedTarget))
            return proposedTarget;

        if (Transform(user).MapID != MapId.Nullspace)
            return user;

        if (!TryComp(outsideTarget, out VehicleInteriorComponent? interior))
            return null;

        if (interior.Grid != EntityUid.Invalid && Exists(interior.Grid) && !TerminatingOrDeleted(interior.Grid))
            return interior.Grid;

        if (interior.Map != EntityUid.Invalid && Exists(interior.Map) && !TerminatingOrDeleted(interior.Map))
            return interior.Map;

        return null;
    }

    private bool IsUsableInsideTarget(EntityUid user, EntityUid outsideTarget, EntityUid? proposedTarget)
    {
        if (proposedTarget is not { } target ||
            target == outsideTarget ||
            !Exists(target) ||
            TerminatingOrDeleted(target))
        {
            return false;
        }

        return target != user || Transform(user).MapID != MapId.Nullspace;
    }

    private void UpdateActionState(VehicleViewToggleComponent toggle)
    {
        if (toggle.Action == null)
            return;

        _actions.SetToggled(toggle.Action, toggle.IsOutside);
    }

    private void EnsureSingleToggleAction(EntityUid user, VehicleViewToggleComponent toggle)
    {
        bool IsToggleActionPrototype(EntityUid actionUid)
        {
            if (TerminatingOrDeleted(actionUid) ||
                !TryComp(actionUid, out MetaDataComponent? metaData))
            {
                return false;
            }

            return metaData.EntityPrototype?.ID == toggle.ActionId.ToString();
        }

        bool IsLiveToggleAction(EntityUid actionUid)
        {
            if (!IsToggleActionPrototype(actionUid))
                return false;

            if (!TryComp(actionUid, out ActionComponent? actionComp) ||
                actionComp.AttachedEntity != user)
            {
                return false;
            }

            return true;
        }

        if (TryComp(user, out ActionsContainerComponent? containerComp))
        {
            foreach (var action in containerComp.Container.ContainedEntities.ToArray())
            {
                if (!IsToggleActionPrototype(action))
                    continue;

                if (TryComp(action, out ActionComponent? actionComp) &&
                    actionComp.AttachedEntity == user)
                {
                    continue;
                }

                RemoveAndDeleteToggleAction(action, user);
            }
        }

        EntityUid? primaryAction = null;

        if (toggle.Action is { } existing &&
            IsLiveToggleAction(existing))
        {
            primaryAction = existing;
        }

        if (TryComp(user, out ActionsComponent? actionsComp))
        {
            foreach (var action in actionsComp.Actions.ToArray())
            {
                if (!IsLiveToggleAction(action))
                {
                    continue;
                }

                if (primaryAction == null)
                {
                    primaryAction = action;
                    continue;
                }

                if (action == primaryAction.Value)
                    continue;

                RemoveAndDeleteToggleAction(action, user);
            }
        }

        if (primaryAction == null)
            primaryAction = _actions.AddAction(user, toggle.ActionId);

        toggle.Action = primaryAction;

        if (toggle.Action is { } ensuredAction)
            _actions.SetEnabled(ensuredAction, true);
    }

    private void RemoveAndDeleteToggleAction(EntityUid action, EntityUid? user = null)
    {
        if (TerminatingOrDeleted(action))
            return;

        if (user is { } actionUser)
            _actions.RemoveAction(actionUser, action);
        else
            _actions.RemoveAction(action);

        // The action entity is networked; client-side queued deletion causes prediction errors.
        if (_net.IsClient)
            return;

        if (Exists(action))
            QueueDel(action);
    }
}
