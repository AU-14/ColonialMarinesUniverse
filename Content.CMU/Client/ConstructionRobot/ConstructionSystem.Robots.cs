using Content.Shared.CMU14.ConstructionRobot;
using Robust.Shared.Player;

// ReSharper disable once CheckNamespace
namespace Content.Client.Construction;

public sealed partial class ConstructionGhostComponent
{
    public EntityUid? Planner;
}

public sealed partial class ConstructionSystem
{
    private EntityUid? _robotOwner;

    private void InitializeConstructionRobotTracking()
    {
        SubscribeLocalEvent<CMUConstructionRobotOwnerComponent, ComponentStartup>(OnRobotOwnerStartup);
        SubscribeLocalEvent<CMUConstructionRobotOwnerComponent, ComponentShutdown>(OnRobotOwnerShutdown);
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(_ => _robotOwner = null);
    }

    private void OnRobotOwnerStartup(Entity<CMUConstructionRobotOwnerComponent> ent, ref ComponentStartup args)
    {
        if (ent.Owner == _playerManager.LocalEntity)
            StartTrackingRobotGhosts(ent.Owner);
    }

    private void OnRobotOwnerShutdown(Entity<CMUConstructionRobotOwnerComponent> ent, ref ComponentShutdown args)
    {
        if (_robotOwner == ent.Owner)
            _robotOwner = null;
    }

    private void StartTrackingRobotGhosts(EntityUid owner)
    {
        if (_robotOwner == owner || !HasComp<CMUConstructionRobotOwnerComponent>(owner))
            return;

        _robotOwner = owner;
        foreach (var ghost in _ghosts.Values)
        {
            if (TryComp<ConstructionGhostComponent>(ghost, out var component))
                NotifyRobotGhostPlaced(ghost, component);
        }
    }

    private void NotifyRobotGhostPlaced(EntityUid ghost, ConstructionGhostComponent component)
    {
        if (_robotOwner == null || component.Planner != _robotOwner || component.Prototype is not { } prototype)
            return;

        var transform = Transform(ghost);
        RaiseNetworkEvent(new ConstructionRobotGhostPlacedMessage(
            GetNetCoordinates(transform.Coordinates), prototype.ID, transform.LocalRotation, component.GhostId));
    }

    private void NotifyRobotGhostRemoved(int ghostId)
    {
        if (_robotOwner != null)
            RaiseNetworkEvent(new ConstructionRobotGhostRemovedMessage(ghostId));
    }

    private void ClearRobotGhosts()
    {
        if (_robotOwner != null)
            RaiseNetworkEvent(new ConstructionRobotClearGhostsMessage());
    }
}
