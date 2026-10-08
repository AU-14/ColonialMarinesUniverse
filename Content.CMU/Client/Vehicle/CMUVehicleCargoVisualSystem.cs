using System.Numerics;
using Content.Client._RMC14.Vehicle;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.CMU14.Vehicle.Jeep;
using Content.Shared.Vehicle.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Timing;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client.CMU14.Vehicle;

/// <summary>
/// Draws a crate riding in a cargo bed at the bed's spot for the vehicle's facing, unrotated, between
/// the vehicle and its overlay, bouncing with the body.
/// </summary>
public sealed class CMUVehicleCargoVisualSystem : EntitySystem
{
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private const float PixelsPerMeter = 32f;

    private readonly Dictionary<EntityUid, (Vector2 Offset, int DrawDepth)> _carried = new();
    private readonly HashSet<EntityUid> _current = new();
    private readonly List<EntityUid> _released = new();

    public override void FrameUpdate(float frameTime)
    {
        _current.Clear();
        var query = EntityQueryEnumerator<CMUVehicleCargoCrateComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var crate, out var sprite))
        {
            if (crate.Vehicle is not { } vehicle || !TryComp(vehicle, out CMUVehicleCargoComponent? cargo))
                continue;

            var direction = VehicleTurretDirectionHelpers.GetRenderAlignedCardinalDir(
                _transform.GetWorldRotation(vehicle) + _eye.CurrentEye.Rotation);
            if (!cargo.CrateOffsets.TryGetValue(direction, out var pixels))
                continue;

            if (!_carried.ContainsKey(uid))
                _carried[uid] = (sprite.Offset, sprite.DrawDepth);

            var bob = CMUVehicleBob.Offset(vehicle, CompOrNull<GridVehicleMoverComponent>(vehicle), _timing.CurTime);
            _sprite.SetOffset((uid, sprite), (pixels + new Vector2(0f, bob)) / PixelsPerMeter);
            _sprite.SetDrawDepth((uid, sprite), (int) DrawDepth.Mobs);
            _current.Add(uid);
        }

        _released.Clear();
        foreach (var (uid, original) in _carried)
        {
            if (_current.Contains(uid))
                continue;

            if (TryComp(uid, out SpriteComponent? sprite))
            {
                _sprite.SetOffset((uid, sprite), original.Offset);
                _sprite.SetDrawDepth((uid, sprite), original.DrawDepth);
            }

            _released.Add(uid);
        }

        foreach (var uid in _released)
        {
            _carried.Remove(uid);
        }
    }
}
