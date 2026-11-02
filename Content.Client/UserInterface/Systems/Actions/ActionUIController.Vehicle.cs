using System.Linq;
using Content.Shared.CMU14.Blackfoot;
using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.Actions.Components;
using Content.Shared.CombatMode;
using Robust.Shared.GameObjects;

namespace Content.Client.UserInterface.Systems.Actions;

public sealed partial class ActionUIController
{
    private readonly List<EntityUid?> _vehicleActions = new();
    private bool _vehicleHotbarOverride;

    private IReadOnlyList<EntityUid?> GetActiveHotbarActions()
    {
        return _vehicleHotbarOverride ? _vehicleActions : _actions;
    }

    private List<EntityUid?> GetEditableHotbarActions()
    {
        return _vehicleHotbarOverride ? _vehicleActions : _actions;
    }

    private void RefreshVehicleHotbarOverride(bool forceUpdate = false)
    {
        if (_actionsSystem == null)
            return;

        var hasVehicleContext = HasVehicleHotbarContext();
        var shouldOverride = ShouldUseVehicleHotbarOverride();
        var changed = false;

        if (!shouldOverride && !_vehicleHotbarOverride && !forceUpdate)
            return;

        if (shouldOverride)
        {
            if (!_vehicleHotbarOverride)
            {
                _vehicleHotbarOverride = true;
                changed = true;
            }

            if (RebuildVehicleActionList())
                changed = true;
        }
        else if (_vehicleHotbarOverride)
        {
            _vehicleHotbarOverride = false;
            if (!hasVehicleContext)
                _vehicleActions.Clear();
            changed = true;
        }
        else if (!hasVehicleContext && _vehicleActions.Count > 0)
        {
            _vehicleActions.Clear();
        }

        if (changed || forceUpdate)
            _container?.SetActionData(_actionsSystem, GetActiveHotbarActions().ToArray());
    }

    private bool ShouldUseVehicleHotbarOverride()
    {
        if (_playerManager.LocalEntity is not { } user)
            return false;

        if (!EntityManager.HasComponent<VehicleWeaponsOperatorComponent>(user) &&
            !EntityManager.HasComponent<BlackfootPilotActionComponent>(user))
            return false;

        return true;
    }

    private bool HasVehicleHotbarContext()
    {
        if (_playerManager.LocalEntity is not { } user)
            return false;

        return EntityManager.HasComponent<VehicleWeaponsOperatorComponent>(user) ||
               EntityManager.HasComponent<BlackfootPilotActionComponent>(user);
    }

    // CMU14 method: poll live context, but sort and reconcile only when membership changes.
    private bool RebuildVehicleActionList()
    {
        if (_actionsSystem == null)
            return false;

        var desiredOrdered = _cmuDesiredVehicleActions;
        desiredOrdered.Clear();
        _cmuHardpointActions.Clear();
        var includeHardpointActions = true;
        var allowActionsInsideView = LocalVehicleActionsAllowedInsideView();

        AddBlackfootPilotActions(desiredOrdered);

        if (_playerManager.LocalEntity is { } localUser &&
            EntityManager.TryGetComponent<VehicleViewToggleComponent>(localUser, out var viewToggle))
        {
            includeHardpointActions = viewToggle.IsOutside || allowActionsInsideView;
        }

        if (!includeHardpointActions)
        {
            foreach (var action in _actions)
            {
                if (action is not { } actionUid ||
                    EntityManager.HasComponent<VehicleHardpointActionComponent>(actionUid))
                {
                    continue;
                }

                desiredOrdered.Add(actionUid);
            }
        }
        else if (_playerManager.LocalEntity is { } user &&
                 EntityManager.TryGetComponent<CombatModeComponent>(user, out var combat) &&
                 combat.CombatToggleActionEntity is { } combatAction &&
                 !EntityManager.HasComponent<VehicleHardpointActionComponent>(combatAction))
        {
            desiredOrdered.Add(combatAction);
        }

        var hardpointStart = desiredOrdered.Count;
        if (includeHardpointActions)
        {
            foreach (var (uid, _) in _actionsSystem.GetClientActions())
            {
                if (!EntityManager.TryGetComponent<VehicleHardpointActionComponent>(uid, out var hardpoint))
                    continue;

                _cmuHardpointActions.Add((uid, hardpoint.SortOrder, _cmuHardpointActions.Count));
                desiredOrdered.Add(uid);
            }
        }

        AddBlackfootDoorGunActions(desiredOrdered);

        EntityUid? viewToggleAction = null;
        if (_playerManager.LocalEntity is { } toggleUser &&
            EntityManager.TryGetComponent<VehicleViewToggleComponent>(toggleUser, out var toggleState) &&
            toggleState.Action is { } viewAction &&
            !EntityManager.HasComponent<VehicleHardpointActionComponent>(viewAction))
        {
            viewToggleAction = viewAction;
        }

        if (viewToggleAction == null)
        {
            foreach (var action in _actionsSystem.GetClientActions())
            {
                var uid = action.Owner;
                if (EntityManager.HasComponent<VehicleHardpointActionComponent>(uid) ||
                    !EntityManager.TryGetComponent<MetaDataComponent>(uid, out var metaData) ||
                    metaData.EntityPrototype?.ID != "ActionVehicleToggleView")
                {
                    continue;
                }

                viewToggleAction = uid;
                break;
            }
        }

        if (viewToggleAction is { } ensuredViewAction)
            desiredOrdered.Add(ensuredViewAction);

        EntityUid? vehicleLockAction = null;
        if (_playerManager.LocalEntity is { } lockUser &&
            EntityManager.TryGetComponent<VehicleLockActionComponent>(lockUser, out var lockState) &&
            lockState.Action is { } lockAction &&
            !EntityManager.HasComponent<VehicleHardpointActionComponent>(lockAction))
        {
            vehicleLockAction = lockAction;
        }

        if (vehicleLockAction == null)
        {
            foreach (var action in _actionsSystem.GetClientActions())
            {
                var uid = action.Owner;
                if (EntityManager.HasComponent<VehicleHardpointActionComponent>(uid) ||
                    !EntityManager.TryGetComponent<MetaDataComponent>(uid, out var metaData) ||
                    metaData.EntityPrototype?.ID != "ActionVehicleLock")
                {
                    continue;
                }

                vehicleLockAction = uid;
                break;
            }
        }

        if (vehicleLockAction is { } ensuredVehicleLockAction)
            desiredOrdered.Add(ensuredVehicleLockAction);

        var remaining = _cmuRemainingVehicleActions;
        remaining.Clear();
        foreach (var desired in desiredOrdered)
        {
            if (desired is { } uid)
                remaining.Add(uid);
        }

        // Manual ordering wins over SortOrder after an action has been placed.
        // Compare membership first, including duplicates/holes from hotbar editing.
        if (remaining.Count == _vehicleActions.Count)
        {
            foreach (var existing in _vehicleActions)
            {
                if (existing is { } uid)
                    remaining.Remove(uid);
            }

            if (remaining.Count == 0)
                return false;

            remaining.Clear();
            foreach (var desired in desiredOrdered)
            {
                if (desired is { } uid)
                    remaining.Add(uid);
            }
        }

        // Stable tie order matches OrderBy; only new actions need this ordering.
        _cmuHardpointActions.Sort(static (a, b) =>
        {
            var order = a.SortOrder.CompareTo(b.SortOrder);
            return order != 0 ? order : a.Index.CompareTo(b.Index);
        });
        for (var i = 0; i < _cmuHardpointActions.Count; i++)
            desiredOrdered[hardpointStart + i] = _cmuHardpointActions[i].Uid;

        var rebuilt = _cmuRebuiltVehicleActions;
        rebuilt.Clear();
        foreach (var existing in _vehicleActions)
        {
            if (existing is not { } existingUid || !remaining.Remove(existingUid))
                continue;

            rebuilt.Add(existingUid);
        }

        foreach (var desired in desiredOrdered)
        {
            if (desired is not { } desiredUid || !remaining.Remove(desiredUid))
                continue;

            rebuilt.Add(desiredUid);
        }

        _vehicleActions.Clear();
        _vehicleActions.AddRange(rebuilt);
        return true;
    }

    private bool LocalVehicleActionsAllowedInsideView()
    {
        if (_playerManager.LocalEntity is not { } user)
            return false;

        if (EntityManager.HasComponent<BlackfootPilotActionComponent>(user))
            return true;

        return EntityManager.TryGetComponent<VehicleWeaponsOperatorComponent>(user, out var operatorComp) &&
               operatorComp.AllowActionsInsideView;
    }

    private void AddBlackfootPilotActions(List<EntityUid?> desiredOrdered)
    {
        if (_playerManager.LocalEntity is not { } user ||
            !EntityManager.TryGetComponent<BlackfootPilotActionComponent>(user, out var actions))
        {
            return;
        }

        AddVehicleAction(desiredOrdered, actions.EngineToggleAction);
        AddVehicleAction(desiredOrdered, actions.TakeoffAction);
        AddVehicleAction(desiredOrdered, actions.LandAction);
        AddVehicleAction(desiredOrdered, actions.FlightModeToggleAction);
        AddVehicleAction(desiredOrdered, actions.AscendZLevelAction);
        AddVehicleAction(desiredOrdered, actions.DescendZLevelAction);
        AddVehicleAction(desiredOrdered, actions.RearDoorToggleAction);
        AddVehicleAction(desiredOrdered, actions.StowToggleAction);
    }

    private void AddBlackfootDoorGunActions(List<EntityUid?> desiredOrdered)
    {
        if (_playerManager.LocalEntity is not { } user ||
            !EntityManager.TryGetComponent<BlackfootDoorGunActionComponent>(user, out var actions))
        {
            return;
        }

        AddVehicleAction(desiredOrdered, actions.ZModeToggleAction);
    }

    private void AddVehicleAction(List<EntityUid?> desiredOrdered, EntityUid? action)
    {
        if (action is not { } actionUid ||
            !EntityManager.HasComponent<ActionComponent>(actionUid) ||
            desiredOrdered.Contains(actionUid))
        {
            return;
        }

        desiredOrdered.Add(actionUid);
    }
}
