using System.Linq;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.Vehicle.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Graphics.RSI;

namespace Content.Client._RMC14.Vehicle;

/// <summary>
/// Gives every freely rotating vehicle narrowed east and west sprite sectors.
/// Each side sprite appears within 22.5 degrees of its cardinal direction, for
/// a 45 degree sector in total; north and south cover every remaining angle.
/// </summary>
public sealed partial class VehicleExactCardinalDirectionSystem : EntitySystem
{
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        UpdatesAfter.Add(typeof(VehicleTurretVisualSystem));
    }

    public override void FrameUpdate(float frameTime)
    {
        var vehicles = EntityQueryEnumerator<GridVehicleMoverComponent, SpriteComponent>();
        while (vehicles.MoveNext(out var uid, out _, out var sprite))
        {
            ApplyExactDirection(uid, sprite);
        }

        var turretVisuals = EntityQueryEnumerator<VehicleTurretVisualComponent, SpriteComponent>();
        while (turretVisuals.MoveNext(out var uid, out _, out var sprite))
        {
            ApplyExactDirection(uid, sprite);
        }
    }

    private void ApplyExactDirection(EntityUid uid, SpriteComponent sprite)
    {
        var screenRotation = _transform.GetWorldRotation(uid) + _eye.CurrentEye.Rotation;
        var direction = VehicleTurretDirectionHelpers.GetRenderAlignedCardinalDir(screenRotation);

        sprite.EnableDirectionOverride = true;
        sprite.DirectionOverride = direction;

        // DirectionOverride changes the selected texture, but Robust calculates the
        // direction transform from its biased direction first. Suppress that transform
        // and apply the selected cardinal transform directly to each four-way layer.
        sprite.NoRotation = true;
        _sprite.SetGranularLayersRendering((uid, sprite), true);

        for (var i = 0; i < sprite.AllLayers.Count(); i++)
        {
            _sprite.LayerSetRenderingStrategy((uid, sprite), i, LayerRenderingStrategy.Default);

            var layer = sprite[i];
            if (layer.ActualRsi is not { } rsi ||
                !rsi.TryGetState(layer.RsiState, out var state) ||
                state.RsiDirections != RsiDirectionType.Dir4)
            {
                continue;
            }

            _sprite.LayerSetRotation((uid, sprite), i, -direction.ToAngle());
        }
    }
}
