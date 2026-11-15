using System.Numerics;
using Robust.Client.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Utility;
using static Robust.Shared.Utility.SpriteSpecifier;

namespace Content.Client._RMC14.Xenonids.Hud;

public sealed partial class XenoHudOverlay
{
    private readonly Dictionary<int, Rsi> _hypertensionIcons = new();

    // A viewport gets its own snapshot. Never retain geometry across draws: targets can move,
    // enter containers, change maps or change sprite bounds without a HUD state update.
    private readonly Dictionary<EntityUid, HudGeometry> _hudGeometry = new();

    private Rsi GetHypertensionIcon(int level)
    {
        if (_hypertensionIcons.TryGetValue(level, out var icon))
            return icon;

        icon = new Rsi(RsiPathHypertension, $"level_{level}");
        _hypertensionIcons.Add(level, icon);
        return icon;
    }

    private bool TryGetHudGeometry(EntityUid uid, SpriteComponent sprite, TransformComponent xform,
        MapId map, Box2 viewport, out Box2 bounds, out Vector2 position)
    {
        if (!_hudGeometry.TryGetValue(uid, out var geometry))
        {
            if (xform.MapID == map && !_container.IsEntityOrParentInContainer(uid, xform: xform))
            {
                var localBounds = _sprite.GetLocalBounds((uid, sprite));
                var worldPosition = _transform.GetWorldPosition(xform, _xformQuery);
                geometry = new HudGeometry(localBounds.Translated(worldPosition).Intersects(viewport),
                    localBounds, worldPosition);
            }

            _hudGeometry.Add(uid, geometry);
        }

        bounds = geometry.Bounds;
        position = geometry.Position;
        return geometry.Visible;
    }

    private readonly record struct HudGeometry(bool Visible, Box2 Bounds, Vector2 Position);
}
