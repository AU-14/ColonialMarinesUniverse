using System.Numerics;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Content.Shared.Coordinates;
using Content.Shared.Throwing;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics.Systems;

namespace Content.Shared.CMU14.Threats.Mobs.Ape;

public sealed partial class ApeHurlSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private CMUSharedZLevelsSystem _zLevels = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ApeHurlComponent, ApeHurlActionEvent>(OnAction);
    }

    private void OnAction(Entity<ApeHurlComponent> ape, ref ApeHurlActionEvent args)
    {
        if (args.Handled)
            return;

        MapCoordinates origin = _transform.GetMapCoordinates(ape);
        var target = _transform.ToMapCoordinates(args.Target);
        var direction = target.Position - origin.Position;

        if (direction == Vector2.Zero)
            return;

        var length = direction.Length();
        if (length > ape.Comp.Range)
            direction *= ape.Comp.Range / length;

        args.Handled = true;

        if (_net.IsClient)
            return;

        var debris = SpawnAtPosition(ape.Comp.Debris, ape.Owner.ToCoordinates());
        _throwing.TryThrow(debris, direction, ape.Comp.ThrowSpeed, ape, compensateFriction: true);

        // Cross-level throws arc upward when the cursor is on the z-level above
        if (target.MapId != origin.MapId &&
            Transform(ape).MapUid is { } mapUid &&
            TryComp<CMUZLevelMapComponent>(mapUid, out var zMap) &&
            _zLevels.TryMapUp((mapUid, zMap), out var above) &&
            Transform(above.Value.Owner).MapID == target.MapId)
        {
            _zLevels.SetZVelocity(debris, ape.Comp.ZVelocity);
        }
    }
}
