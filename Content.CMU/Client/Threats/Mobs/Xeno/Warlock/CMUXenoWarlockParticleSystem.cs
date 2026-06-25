using System.Numerics;
using Content.Shared._CMU14.Threats.Mobs.Xeno.Caste.Warlock;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._CMU14.Threats.Mobs.Xeno.Xenonids.Warlock;

public sealed partial class CMUXenoWarlockParticleSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        if (!_overlay.HasOverlay<CMUXenoWarlockParticleOverlay>())
            _overlay.AddOverlay(new CMUXenoWarlockParticleOverlay());

        if (!_overlay.HasOverlay<CMUXenoPsychicCrushBlurOverlay>())
            _overlay.AddOverlay(new CMUXenoPsychicCrushBlurOverlay());
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _overlay.RemoveOverlay<CMUXenoWarlockParticleOverlay>();
        _overlay.RemoveOverlay<CMUXenoPsychicCrushBlurOverlay>();
    }
}

public sealed partial class CMUXenoWarlockParticleOverlay : Overlay
{
    [Dependency] private IEntityManager _entity = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IGameTiming _timing = default!;
    private static readonly ProtoId<ShaderPrototype> UnshadedShader = "unshaded";
    private static readonly ResPath ParticleSprite = new("/Textures/_CMU14/Effects/Xeno/warlock_particles.rsi");
    private const float PixelsPerMeter = EyeManager.PixelsPerMeter;
    private const float CullPadding = 9f;
    private const float MaxDirectedTravelPixels = 250f;
    private readonly Texture _particleTexture;
    private readonly List<EntityUid> _remove = new();
    private readonly HashSet<EntityUid> _seen = new();

    private readonly SpriteSystem _sprite;
    private readonly Dictionary<EntityUid, TimeSpan> _startedAt = new();
    private readonly TransformSystem _transform;
    private readonly ShaderInstance _unshaded;
    private readonly EntityQuery<TransformComponent> _xformQuery;

    public CMUXenoWarlockParticleOverlay()
    {
        IoCManager.InjectDependencies(this);

        _sprite = _entity.System<SpriteSystem>();
        _transform = _entity.System<TransformSystem>();
        _xformQuery = _entity.GetEntityQuery<TransformComponent>();
        _particleTexture = _sprite.Frame0(new SpriteSpecifier.Rsi(ParticleSprite, "lemon"));
        _unshaded = _prototype.Index(UnshadedShader).Instance();
    }

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    protected override void Draw(in OverlayDrawArgs args)
    {
        DrawingHandleWorld handle = args.WorldHandle;
        Box2 cullBounds = args.WorldAABB.Enlarged(CullPadding);
        TimeSpan now = _timing.CurTime;
        Vector2 textureSize = new Vector2(_particleTexture.Width, _particleTexture.Height) / PixelsPerMeter;

        _seen.Clear();
        handle.UseShader(_unshaded);

        AllEntityQueryEnumerator<CMUXenoWarlockParticleEmitterComponent, TransformComponent> query = _entity
            .AllEntityQueryEnumerator<CMUXenoWarlockParticleEmitterComponent, TransformComponent>();
        while (query.MoveNext(out EntityUid uid, out CMUXenoWarlockParticleEmitterComponent? particles,
            out TransformComponent? xform))
        {
            _seen.Add(uid);
            if (xform.MapID != args.MapId)
                continue;

            Vector2 origin = _transform.GetWorldPosition(xform, _xformQuery);
            if (!cullBounds.Contains(origin))
                continue;

            DrawEmitter(uid, particles, origin, textureSize, GetStartedAt(uid, now), now, handle);
        }

        handle.UseShader(null);

        _remove.Clear();
        foreach ((EntityUid uid, TimeSpan _) in _startedAt)
        {
            if (!_seen.Contains(uid))
                _remove.Add(uid);
        }

        foreach (EntityUid uid in _remove)
        {
            _startedAt.Remove(uid);
        }
    }

    private void DrawEmitter(EntityUid uid,
        CMUXenoWarlockParticleEmitterComponent particles,
        Vector2 origin,
        Vector2 textureSize,
        TimeSpan startedAt,
        TimeSpan now,
        DrawingHandleWorld handle)
    {
        CMUXenoWarlockParticleProfile profile = CMUXenoWarlockSystem.GetWarlockParticleProfile(particles.Effect);
        Color color = Color.FromHex(profile.Color);
        float elapsed = Math.Max(0f, (float)(now - startedAt).TotalSeconds);
        float lifespan = Math.Max(0.05f, profile.Lifespan / 10f);
        float fade = Math.Max(0.01f, profile.Fade / 10f);
        int seed = uid.GetHashCode();
        Vector2 holderOffset = CMUXenoWarlockSystem.GetWarlockParticleRenderOffset(particles.Effect) / PixelsPerMeter;
        Vector2 velocity = particles.UseMotionOverride ? particles.MotionVelocity : profile.Velocity;
        Vector2 gravity = particles.UseMotionOverride ? particles.MotionGravity : profile.Gravity;

        for (var i = 0; i < profile.Count; i++)
        {
            float phase = CMUXenoWarlockParticleOverlay.Hash01(seed, i, 0);
            float age = CMUXenoWarlockParticleOverlay.PositiveModulo(elapsed + phase * lifespan, lifespan);
            float rawAge = age * 10f;
            float alpha = CMUXenoWarlockParticleOverlay.GetAlpha(age, lifespan, fade);
            if (alpha <= 0f)
                continue;

            Vector2 initial = CMUXenoWarlockParticleOverlay.RandomRing(seed, i, profile.PositionRadius);
            Vector2 drift = CMUXenoWarlockParticleOverlay.Lerp(profile.DriftMin, profile.DriftMax,
                CMUXenoWarlockParticleOverlay.Hash01(seed, i, 4), CMUXenoWarlockParticleOverlay.Hash01(seed, i, 5));
            Vector2 motion = velocity * rawAge + drift * rawAge + gravity * (0.5f * rawAge * rawAge);
            if (particles.UseMotionOverride
                && motion.LengthSquared() > MaxDirectedTravelPixels * MaxDirectedTravelPixels)
                motion = Vector2.Normalize(motion) * MaxDirectedTravelPixels;

            Vector2 scale = CMUXenoWarlockParticleOverlay.Lerp(profile.ScaleMin, profile.ScaleMax,
                    CMUXenoWarlockParticleOverlay.Hash01(seed, i, 6), CMUXenoWarlockParticleOverlay.Hash01(seed, i, 7))
                + new Vector2(profile.Grow * rawAge);
            scale = Vector2.Max(scale, new(0.04f));

            Vector2 center = origin + holderOffset + (initial + motion) / PixelsPerMeter;
            Vector2 size = textureSize * scale;
            Box2 box = Box2.CenteredAround(center, size);
            handle.DrawTextureRect(_particleTexture, box, color.WithAlpha(alpha));
        }
    }

    private static Vector2 RandomRing(int seed, int index, Vector2 radius)
    {
        float angle = CMUXenoWarlockParticleOverlay.Hash01(seed, index, 1) * MathF.Tau;
        float length = MathHelper.Lerp(radius.X, radius.Y,
            MathF.Sqrt(CMUXenoWarlockParticleOverlay.Hash01(seed, index, 2)));
        return new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * length;
    }

    private static Vector2 Lerp(Vector2 min, Vector2 max, float x, float y) => new(MathHelper.Lerp(min.X, max.X, x),
        MathHelper.Lerp(min.Y, max.Y, y));

    private static float GetAlpha(float age, float lifespan, float fade)
    {
        float fadeStart = Math.Max(0f, lifespan - fade);
        if (age <= fadeStart)
            return 1f;

        return Math.Clamp(1f - (age - fadeStart) / Math.Max(0.01f, lifespan - fadeStart), 0f, 1f);
    }

    private static float PositiveModulo(float value, float divisor)
    {
        float result = value % divisor;
        return result < 0f ? result + divisor : result;
    }

    private static float Hash01(int seed, int index, int salt)
    {
        unchecked
        {
            var hash = (uint)seed;
            hash ^= (uint)index * 0x9E3779B9u;
            hash ^= (uint)salt * 0x85EBCA6Bu;
            hash ^= hash >> 16;
            hash *= 0x7FEB352Du;
            hash ^= hash >> 15;
            hash *= 0x846CA68Bu;
            hash ^= hash >> 16;
            return (hash & 0x00FFFFFF) / (float)0x01000000;
        }
    }

    private TimeSpan GetStartedAt(EntityUid uid, TimeSpan now)
    {
        if (_startedAt.TryGetValue(uid, out TimeSpan startedAt))
            return startedAt;

        _startedAt[uid] = now;
        return now;
    }
}
