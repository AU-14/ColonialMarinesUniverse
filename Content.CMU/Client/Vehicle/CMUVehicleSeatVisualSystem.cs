using System.Numerics;
using Content.Client.Graphics;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.Buckle.Components;
using Content.Shared.CMU14.Vehicle.Jeep;
using Content.Shared.Jittering;
using Content.Shared.Rotation;
using Content.Shared.Vehicle.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Vehicle;

/// <summary>
/// Seats riders of an open vehicle like the fighter's cockpit crew: they face along the vehicle,
/// turn with it between cardinal headings, sit where the art draws their seat in each direction,
/// bounce with the body, and are cut off below their seat line so their legs never show through
/// the seats in front.
/// </summary>
public sealed class CMUVehicleSeatVisualSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private const float PixelsPerMeter = 32f;
    private const string ClipId = "cmu-jeep-rider-clip";
    private static readonly ProtoId<ShaderPrototype> ClipShader = "CMUJeepRiderClip";

    private readonly Dictionary<EntityUid, RiderState> _riders = new();
    private readonly HashSet<EntityUid> _seated = new();
    private readonly List<EntityUid> _released = new();

    // Riders released mid-jitter: the jitter puts back the offset it started from, which was the
    // seated one, so their own offset is restored once it ends.
    private readonly Dictionary<EntityUid, Vector2> _pendingOffsets = new();

    private sealed class RiderState(SpriteComponent sprite, Vector2 offset)
    {
        public readonly Vector2 Offset = offset;
        public readonly Angle Rotation = sprite.Rotation;
        public readonly bool NoRotation = sprite.NoRotation;
        public readonly bool EnableDirectionOverride = sprite.EnableDirectionOverride;
        public readonly Direction DirectionOverride = sprite.DirectionOverride;
        public ShaderInstance? Clip;
    }

    public override void Initialize()
    {
        UpdatesAfter.Add(typeof(CMUVehicleOverlayVisualSystem));
        SubscribeLocalEvent<CMUVehicleRiderClipComponent, BeforePostShaderRenderEvent>(OnClipRender);
    }

    public override void Shutdown()
    {
        _seated.Clear();
        Release();
        _pendingOffsets.Clear();
    }

    public override void FrameUpdate(float frameTime)
    {
        _seated.Clear();
        var eyeRotation = _eye.CurrentEye.Rotation;
        var seats = EntityQueryEnumerator<CMUVehicleSeatComponent, StrapComponent, TransformComponent>();
        while (seats.MoveNext(out _, out var seat, out var strap, out var seatXform))
        {
            if (strap.BuckledEntities.Count == 0 || seat.Vehicle is not { } vehicle || seatXform.ParentUid != vehicle)
                continue;

            var screenRotation = _transform.GetWorldRotation(vehicle) + eyeRotation;
            var direction = VehicleTurretDirectionHelpers.GetRenderAlignedCardinalDir(screenRotation);
            var leftover = screenRotation - direction.ToAngle();

            // Where the seat's transform puts the rider on screen, and where the art draws the seat.
            var placed = screenRotation.RotateVec(seatXform.LocalPosition);
            var drawn = seat.PixelOffsets.TryGetValue(direction, out var pixels)
                ? leftover.RotateVec(pixels / PixelsPerMeter)
                : placed + leftover.RotateVec(new Vector2(0f, seat.Lift / PixelsPerMeter));
            var bob = CMUVehicleBob.Offset(vehicle, CompOrNull<GridVehicleMoverComponent>(vehicle), _timing.CurTime);
            var shift = drawn - placed + leftover.RotateVec(new Vector2(0f, bob / PixelsPerMeter));

            foreach (var rider in strap.BuckledEntities)
            {
                if (!TryComp(rider, out SpriteComponent? sprite))
                    continue;

                _seated.Add(rider);
                if (!_riders.TryGetValue(rider, out var original))
                {
                    // A rider seated mid-jitter has a jittered offset; the jitter's start is their own.
                    // A rider seated again before a pending restore ran still owes that restore.
                    var offset = _pendingOffsets.TryGetValue(rider, out var pending) ? pending
                        : TryComp(rider, out JitteringComponent? jitter) ? jitter.StartOffset
                        : sprite.Offset;
                    _riders[rider] = original = new RiderState(sprite, offset);
                }

                sprite.NoRotation = true;
                sprite.EnableDirectionOverride = true;
                sprite.DirectionOverride = direction;
                _sprite.SetRotation((rider, sprite), leftover);
                _sprite.SetOffset((rider, sprite), original.Offset + shift);
                UpdateClip((rider, sprite), original, seat.Clips.TryGetValue(direction, out var clip) ? clip : (float?) null, leftover, eyeRotation);
            }
        }

        Release();
        RestorePendingOffsets();
    }

    /// <summary>
    /// Keeps the rider's seat line, in world space, for the clip shader to project into each viewport.
    /// </summary>
    private void UpdateClip(Entity<SpriteComponent> rider, RiderState original, float? clip, Angle leftover, Angle eyeRotation)
    {
        if (clip is not { } pixels)
        {
            RemoveClip(rider, original);
            return;
        }

        original.Clip ??= _prototypes.Index(ClipShader).InstanceUnique();

        // Re-add the clip if another system cleared the rider's post shaders.
        if (!_sprite.TryGetPostShader(rider.Comp, ClipId, out var entry) || entry.Shader != original.Clip)
        {
            _sprite.SetPostShader(rider.AsNullable(), new SpriteComponent.PostShaderArgs(ClipId, original.Clip)
            {
                RaiseShaderEvent = true,
                Before = ContentPostShaderIds.BeforeOutlines,
            });
        }

        // The sprite is drawn in screen space around its offset, turned by the leftover angle.
        var toWorld = -eyeRotation;
        var centre = _transform.GetWorldPosition(rider) + toWorld.RotateVec(rider.Comp.Offset);
        var line = EnsureComp<CMUVehicleRiderClipComponent>(rider);
        line.Origin = centre + toWorld.RotateVec(leftover.RotateVec(new Vector2(0f, -pixels / PixelsPerMeter)));
        line.Along = toWorld.RotateVec(leftover.RotateVec(Vector2.UnitX));
        line.Up = toWorld.RotateVec(leftover.RotateVec(Vector2.UnitY));
    }

    private void OnClipRender(Entity<CMUVehicleRiderClipComponent> ent, ref BeforePostShaderRenderEvent args)
    {
        if (args.Id != ClipId)
            return;

        // Screen UVs aren't square, so the normal is taken from the projected line, not the projected up.
        var viewport = args.Viewport;
        var origin = ScreenUv(viewport, ent.Comp.Origin);
        var along = ScreenUv(viewport, ent.Comp.Origin + ent.Comp.Along) - origin;
        var normal = new Vector2(-along.Y, along.X);
        if (Vector2.Dot(ScreenUv(viewport, ent.Comp.Origin + ent.Comp.Up) - origin, normal) < 0f)
            normal = -normal;

        args.Shader.SetParameter("line_origin", origin);
        args.Shader.SetParameter("line_normal", normal);
        args.Shader.SetParameter("clip_enabled", normal.LengthSquared() > 0f);
    }

    private static Vector2 ScreenUv(IClydeViewport viewport, Vector2 world)
    {
        var screen = viewport.WorldToLocal(world) / viewport.Size;
        return new Vector2(screen.X, 1 - screen.Y);
    }

    private void RemoveClip(Entity<SpriteComponent> rider, RiderState original)
    {
        if (original.Clip == null)
            return;

        // Only free the instance if it was still ours on the sprite; one that another system took
        // over is left to the finalizer.
        if (_sprite.TryGetPostShader(rider.Comp, ClipId, out var entry) && entry.Shader == original.Clip)
        {
            _sprite.RemovePostShader(rider.AsNullable(), ClipId);
            original.Clip.Dispose();
        }

        original.Clip = null;
        RemComp<CMUVehicleRiderClipComponent>(rider);
    }

    /// <summary>
    /// Puts back the sprite of every rider that is no longer seated.
    /// </summary>
    private void Release()
    {
        _released.Clear();
        foreach (var (rider, original) in _riders)
        {
            if (_seated.Contains(rider))
                continue;

            if (TryComp(rider, out SpriteComponent? sprite))
            {
                sprite.NoRotation = original.NoRotation;
                sprite.EnableDirectionOverride = original.EnableDirectionOverride;
                sprite.DirectionOverride = original.DirectionOverride;
                _sprite.SetRotation((rider, sprite), ReleasedRotation(rider, original));
                _sprite.SetOffset((rider, sprite), original.Offset);
                if (TryComp(rider, out JitteringComponent? jitter) && jitter.StartOffset != original.Offset)
                    _pendingOffsets[rider] = original.Offset;

                RemoveClip((rider, sprite), original);
            }
            else
            {
                original.Clip?.Dispose();
            }

            _released.Add(rider);
        }

        foreach (var rider in _released)
        {
            _riders.Remove(rider);
        }
    }

    /// <summary>
    /// The rider may have lain down or stood up while seated, so their rotation comes from their
    /// current state, not from when they sat down.
    /// </summary>
    private Angle ReleasedRotation(EntityUid rider, RiderState original)
    {
        if (!TryComp(rider, out RotationVisualsComponent? rotation))
            return original.Rotation;

        return _appearance.TryGetData(rider, RotationVisuals.RotationState, out RotationState state) &&
               state == RotationState.Horizontal
            ? rotation.HorizontalRotation
            : rotation.VerticalRotation;
    }

    private void RestorePendingOffsets()
    {
        if (_pendingOffsets.Count == 0)
            return;

        _released.Clear();
        foreach (var (rider, offset) in _pendingOffsets)
        {
            if (_seated.Contains(rider) || !TryComp(rider, out SpriteComponent? sprite))
            {
                _released.Add(rider);
                continue;
            }

            if (HasComp<JitteringComponent>(rider))
                continue;

            _sprite.SetOffset((rider, sprite), offset);
            _released.Add(rider);
        }

        foreach (var rider in _released)
        {
            _pendingOffsets.Remove(rider);
        }
    }
}

/// <summary>
/// A seated rider's seat line in world space, read by the clip shader for each viewport. Client only.
/// </summary>
[RegisterComponent]
public sealed partial class CMUVehicleRiderClipComponent : Component
{
    public Vector2 Origin;
    public Vector2 Along;
    public Vector2 Up;
}
