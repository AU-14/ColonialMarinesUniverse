using System.Numerics;
using Content.Shared.Coordinates;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;

namespace Content.Server.CMU14.Hijack;

public sealed partial class ShipHijackSystem
{
    /// <summary>Collision trail and 10-15 impacts from CM-SS13's dropship_hijack.dm.</summary>
    public bool TryApplyDropshipImpact(EntityUid dropship, EntityUid destination)
    {
        if (!TryGetShip(destination, out _) || Transform(destination).GridUid is not { } gridUid ||
            !TryComp(gridUid, out MapGridComponent? grid))
            return false;
        var target = _transform.GetMapCoordinates(destination);
        var bounds = _transform.GetWorldMatrix(gridUid).TransformBox(grid.LocalAABB);
        var edge = target.Position.Y < bounds.Center.Y ? bounds.Bottom : bounds.Top;
        var direction = MathF.Sign(target.Position.Y - edge);
        if (direction != 0)
        {
            for (var y = edge; MathF.Abs(target.Position.Y - y) >= 5; y += direction * 5)
            {
                var point = new MapCoordinates(new Vector2(target.Position.X + _random.Next(-3, 4), y), target.MapId);
                QueueCellExplosion(point, dropship, 250, 20);
                _fire.SpawnFireDiamond("RMCHijackPipeFire", _transform.ToCoordinates(point), 6,
                    zProjectionMaxFloors: 0);
            }
        }
        var count = _random.Next(10, 16);
        for (var i = 0; i < count; i++)
        {
            var point = target.Offset(new Vector2(_random.Next(-5, 16), _random.Next(-5, 26)));
            QueueCellExplosion(point, dropship, 250, 20);
        }
        return true;
    }

    // Convert a linear radial peak/falloff into integrated intensity for Robust's
    // explosion API. Damage still uses RMC's armor and structural damage handling.
    private void QueueCellExplosion(MapCoordinates point, EntityUid source, float peak, float falloff, string type = "RMC")
    {
        var total = MathF.PI * peak * peak * peak / (3 * falloff * falloff);
        _explosions.QueueExplosion(point, type, total, falloff, peak, source, canCreateVacuum: false);
    }
}
