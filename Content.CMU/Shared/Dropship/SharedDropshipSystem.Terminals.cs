namespace Content.Shared._RMC14.Dropship;

public abstract partial class SharedDropshipSystem
{
    public Entity<DropshipDestinationComponent, TransformComponent>? FindTerminalLZ(Entity<DropshipTerminalComponent> terminal)
    {
        if (!terminal.Comp.UseShipDestinations)
            return FindClosestLZ(terminal.Owner);

        var transform = Transform(terminal);
        var position = _transform.GetWorldPosition(terminal);
        Entity<DropshipDestinationComponent, TransformComponent>? closest = null;
        var distance = float.MaxValue;
        var query = EntityQueryEnumerator<DropshipDestinationComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var destination, out var destinationTransform))
        {
            if (!destination.Home ||
                destination.Destinationtype != DropshipDestinationComponent.DestinationType.Dropship ||
                HasComp<DropshipHijackDestinationComponent>(uid) ||
                !_zLevels.IsSameZNetwork(transform.MapID, destinationTransform.MapID) ||
                !string.Equals(destination.FactionController, terminal.Comp.Faction, StringComparison.OrdinalIgnoreCase))
                continue;

            var candidateDistance = (_transform.GetWorldPosition(uid) - position).LengthSquared();
            if (candidateDistance >= distance)
                continue;

            distance = candidateDistance;
            closest = (uid, destination, destinationTransform);
        }

        return closest;
    }
}
