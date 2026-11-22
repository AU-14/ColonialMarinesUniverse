using System.Numerics;
using Content.Shared.Buckle.Components;
using Content.Shared.CMU14.PowerLoader;
using Robust.Client.ComponentTrees;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Utility;
using Robust.Shared.Graphics.RSI;

namespace Content.Client.CMU14.PowerLoader;

/// <summary>
/// Applies a <see cref="CMUPowerLoaderComponent"/>'s piloted visuals, following the way the loader is drawn facing:
/// the pilot's sprite faces that way and is shifted into the cab, and the beacon light stays on the drawn beacon.
/// Checked every frame because the loader turns, and because the client only raises strap events for its own
/// predicted buckles, not for other players or server corrections.
/// </summary>
public sealed partial class CMUPowerLoaderVisualsSystem : EntitySystem
{
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private PointLightSystem _pointLight = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private SpriteTreeSystem _spriteTree = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    /// <summary>Pilots and how their sprite was before boarding.</summary>
    private readonly Dictionary<EntityUid, Original> _originals = new();
    private readonly HashSet<EntityUid> _piloting = new();
    private readonly List<EntityUid> _left = new();

    private readonly record struct Original(Vector2 Offset, bool EnableDirectionOverride, Direction DirectionOverride);

    public override void FrameUpdate(float frameTime)
    {
        _piloting.Clear();
        var eyeRotation = _eye.CurrentEye.Rotation;
        var query = EntityQueryEnumerator<CMUPowerLoaderComponent, StrapComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var loader, out var strap, out var xform))
        {
            if (strap.BuckledEntities.Count == 0)
                continue;

            // Picked like a four-way sprite picks its frame, so moving diagonally the pilot and the beacon
            // never face or lean one way while the loader is drawn facing another.
            var screenRotation = (_transform.GetWorldRotation(xform) + eyeRotation).Reduced().FlipPositive();
            var facing = SpriteComponent.Layer.GetDirection(RsiDirectionType.Dir4, screenRotation).Convert();

            UpdateBeacon(uid, loader, facing, screenRotation);

            var shift = loader.PilotOffset + loader.PilotDirectionOffsets.GetValueOrDefault(facing);
            foreach (var pilot in strap.BuckledEntities)
            {
                if (TryComp<SpriteComponent>(pilot, out var sprite))
                    UpdatePilot((pilot, sprite), facing, shift);
            }
        }

        foreach (var pilot in _originals.Keys)
        {
            if (!_piloting.Contains(pilot))
                _left.Add(pilot);
        }

        foreach (var pilot in _left)
        {
            if (_originals.Remove(pilot, out var original) &&
                !TerminatingOrDeleted(pilot) &&
                TryComp<SpriteComponent>(pilot, out var sprite))
            {
                sprite.EnableDirectionOverride = original.EnableDirectionOverride;
                sprite.DirectionOverride = original.DirectionOverride;
                SetOffset((pilot, sprite), original.Offset);
            }
        }

        _left.Clear();
    }

    /// <summary>
    /// Point light offsets turn with the entity, so the on-screen offset is counter-rotated by its on-screen angle.
    /// </summary>
    private void UpdateBeacon(EntityUid uid, CMUPowerLoaderComponent loader, Direction facing, Angle screenRotation)
    {
        if (loader.BeaconOffsets.Count == 0 ||
            !TryComp<PointLightComponent>(uid, out var light) ||
            !light.Enabled)
        {
            return;
        }

        var screenOffset = loader.BeaconOffsets.GetValueOrDefault(facing);
        _pointLight.SetOffset(uid, (-screenRotation).RotateVec(screenOffset), light);
    }

    /// <summary>
    /// Mob sprites don't rotate, so the shift is already on screen.
    /// </summary>
    private void UpdatePilot(Entity<SpriteComponent> pilot, Direction facing, Vector2 shift)
    {
        _piloting.Add(pilot);
        if (!_originals.TryGetValue(pilot, out var original))
        {
            original = new Original(pilot.Comp.Offset, pilot.Comp.EnableDirectionOverride, pilot.Comp.DirectionOverride);
            _originals[pilot] = original;
        }

        pilot.Comp.EnableDirectionOverride = true;
        pilot.Comp.DirectionOverride = facing;
        if (pilot.Comp.Offset != original.Offset + shift)
            SetOffset(pilot, original.Offset + shift);
    }

    private void SetOffset(Entity<SpriteComponent> sprite, Vector2 offset)
    {
        _sprite.SetOffset(sprite.AsNullable(), offset);
        _spriteTree.QueueTreeUpdate(sprite, sprite.Comp);
    }
}
