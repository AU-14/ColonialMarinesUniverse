using System.Numerics;
using Content.Shared.CMU14.DroneOperator;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.DroneOperator;

public sealed class CMUFlamerDroneVisualizerSystem : EntitySystem
{
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private SharedPointLightSystem _lights = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private readonly Dictionary<EntityUid, (EntityUid First, EntityUid Second)> _effects = new();

    public override void Initialize()
    {
        UpdatesAfter.Add(typeof(TransformSystem));
        UpdatesAfter.Add(typeof(EyeSystem));
        SubscribeLocalEvent<CMUFlamerDroneComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnShutdown(Entity<CMUFlamerDroneComponent> ent, ref ComponentShutdown args)
    {
        ClearEffects(ent);
    }

    public override void FrameUpdate(float frameTime)
    {
        var query = EntityQueryEnumerator<CMUFlamerDroneComponent, SpriteComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var flamer, out _, out var transform))
        {
            if (!flamer.PilotLit)
            {
                ClearEffects(uid);
                continue;
            }

            if (!_effects.TryGetValue(uid, out var effects) || TerminatingOrDeleted(effects.First) || TerminatingOrDeleted(effects.Second))
            {
                ClearEffects(uid);
                effects = (SpawnAttachedTo(flamer.ClawEffect, new(uid, Vector2.Zero)),
                    SpawnAttachedTo(flamer.ClawEffect, new(uid, Vector2.Zero)));
                _effects.Add(uid, effects);
            }

            // Match the body's on-screen RSI direction, including rotated grids/cameras.
            var angle = _transform.GetWorldRotation(transform) + _eye.CurrentEye.Rotation;
            var index = (int) SpriteComponent.Layer.GetDirection(RsiDirectionType.Dir4, angle.Reduced().FlipPositive());
            var firing = flamer.FlameUntil > _timing.CurTime;
            UpdateEffect(effects.First, flamer.FirstClawOffsets[index], angle, firing);
            UpdateEffect(effects.Second, flamer.SecondClawOffsets[index], angle, firing);
        }
    }

    private void UpdateEffect(EntityUid effect, Vector2 screenOffset, Angle screenAngle, bool firing)
    {
        // These client-only children share one nozzle position for both particles and light.
        // Undo the hull and camera rotation to keep the attachment on the rendered claw tip.
        var transform = Transform(effect);
        transform.ActivelyLerping = false;
        _transform.SetLocalPositionNoLerp(effect, (-screenAngle).RotateVec(screenOffset), transform);
        _sprite.SetScale(effect, new Vector2(firing ? 0.5f : 0.3f));
        _lights.SetEnergy(effect, firing ? 2f : 0.8f);
    }

    private void ClearEffects(EntityUid drone)
    {
        if (!_effects.Remove(drone, out var effects))
            return;

        ClearEffect(effects.First);
        ClearEffect(effects.Second);
    }

    private void ClearEffect(EntityUid effect)
    {
        if (TerminatingOrDeleted(effect))
            return;

        _lights.SetEnabled(effect, false);
        _sprite.SetVisible(effect, false);
        QueueDel(effect);
    }
}
