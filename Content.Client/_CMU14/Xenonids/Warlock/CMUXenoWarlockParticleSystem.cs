using System.Numerics;
using Content.Shared._CMU14.Xenonids.Warlock;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._CMU14.Xenonids.Warlock;

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
    private static readonly ProtoId<ShaderPrototype> UnshadedShader = "unshaded";
    private static readonly ResPath ParticleSprite = new("/Textures/_CMU14/Effects/Xeno/warlock_particles.rsi");

    private const float PixelsPerMeter = EyeManager.PixelsPerMeter;
    private const float CullPadding = 9f;
    private const float MaxDirectedTravelPixels = 250f;

    [Dependency] private IEntityManager _entity = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _prototype = default!;

    private readonly SpriteSystem _sprite;
    private readonly TransformSystem _transform;
    private readonly EntityQuery<TransformComponent> _xformQuery;
    private readonly Texture _particleTexture;
    private readonly ShaderInstance _unshaded;
    private readonly Dictionary<EntityUid, TimeSpan> _startedAt = new();
    private readonly HashSet<EntityUid> _seen = new();
    private readonly List<EntityUid> _remove = new();

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public CMUXenoWarlockParticleOverlay()
    {
        IoCManager.InjectDependencies(this);

        _sprite = _entity.System<SpriteSystem>();
        _transform = _entity.System<TransformSystem>();
        _xformQuery = _entity.GetEntityQuery<TransformComponent>();
        _particleTexture = _sprite.Frame0(new SpriteSpecifier.Rsi(ParticleSprite, "lemon"));
        _unshaded = _prototype.Index(UnshadedShader).Instance();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var cullBounds = args.WorldAABB.Enlarged(CullPadding);
        var now = _timing.CurTime;
        var textureSize = new Vector2(_particleTexture.Width, _particleTexture.Height) / PixelsPerMeter;

        _seen.Clear();
        handle.UseShader(_unshaded);

        var query = _entity.AllEntityQueryEnumerator<CMUXenoWarlockParticleEmitterComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var particles, out var xform))
        {
            _seen.Add(uid);
            if (xform.MapID != args.MapId)
                continue;

            var origin = _transform.GetWorldPosition(xform, _xformQuery);
            if (!cullBounds.Contains(origin))
                continue;

            DrawEmitter(uid, particles, origin, textureSize, GetStartedAt(uid, now), now, handle);
        }

        handle.UseShader(null);

        _remove.Clear();
        foreach (var (uid, _) in _startedAt)
        {
            if (!_seen.Contains(uid))
                _remove.Add(uid);
        }

        foreach (var uid in _remove)
        {
            _startedAt.Remove(uid);
        }
    }

    private void DrawEmitter(
        EntityUid uid,
        CMUXenoWarlockParticleEmitterComponent particles,
        Vector2 origin,
        Vector2 textureSize,
        TimeSpan startedAt,
        TimeSpan now,
        DrawingHandleWorld handle)
    {
        var profile = CMUXenoWarlockSystem.GetWarlockParticleProfile(particles.Effect);
        var color = Color.FromHex(profile.Color);
        var elapsed = Math.Max(0f, (float) (now - startedAt).TotalSeconds);
        var lifespan = Math.Max(0.05f, profile.Lifespan / 10f);
        var fade = Math.Max(0.01f, profile.Fade / 10f);
        var seed = uid.GetHashCode();
        var holderOffset = CMUXenoWarlockSystem.GetWarlockParticleRenderOffset(particles.Effect) / PixelsPerMeter;
        var velocity = particles.UseMotionOverride ? particles.MotionVelocity : profile.Velocity;
        var gravity = particles.UseMotionOverride ? particles.MotionGravity : profile.Gravity;

        for (var i = 0; i < profile.Count; i++)
        {
            var phase = Hash01(seed, i, 0);
            var age = PositiveModulo(elapsed + phase * lifespan, lifespan);
            var rawAge = age * 10f;
            var alpha = GetAlpha(age, lifespan, fade);
            if (alpha <= 0f)
                continue;

            var initial = RandomRing(seed, i, profile.PositionRadius);
            var drift = Lerp(profile.DriftMin, profile.DriftMax, Hash01(seed, i, 4), Hash01(seed, i, 5));
            var motion = velocity * rawAge + drift * rawAge + gravity * (0.5f * rawAge * rawAge);
            if (particles.UseMotionOverride && motion.LengthSquared() > MaxDirectedTravelPixels * MaxDirectedTravelPixels)
                motion = Vector2.Normalize(motion) * MaxDirectedTravelPixels;

            var scale = Lerp(profile.ScaleMin, profile.ScaleMax, Hash01(seed, i, 6), Hash01(seed, i, 7)) +
                        new Vector2(profile.Grow * rawAge);
            scale = Vector2.Max(scale, new Vector2(0.04f));

            var center = origin + holderOffset + (initial + motion) / PixelsPerMeter;
            var size = textureSize * scale;
            var box = Box2.CenteredAround(center, size);
            handle.DrawTextureRect(_particleTexture, box, color.WithAlpha(alpha));
        }
    }

    private static Vector2 RandomRing(int seed, int index, Vector2 radius)
    {
        var angle = Hash01(seed, index, 1) * MathF.Tau;
        var length = MathHelper.Lerp(radius.X, radius.Y, MathF.Sqrt(Hash01(seed, index, 2)));
        return new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * length;
    }

    private static Vector2 Lerp(Vector2 min, Vector2 max, float x, float y)
    {
        return new Vector2(
            MathHelper.Lerp(min.X, max.X, x),
            MathHelper.Lerp(min.Y, max.Y, y));
    }

    private static float GetAlpha(float age, float lifespan, float fade)
    {
        var fadeStart = Math.Max(0f, lifespan - fade);
        if (age <= fadeStart)
            return 1f;

        return Math.Clamp(1f - (age - fadeStart) / Math.Max(0.01f, lifespan - fadeStart), 0f, 1f);
    }

    private static float PositiveModulo(float value, float divisor)
    {
        var result = value % divisor;
        return result < 0f ? result + divisor : result;
    }

    private static float Hash01(int seed, int index, int salt)
    {
        unchecked
        {
            var hash = (uint) seed;
            hash ^= (uint) index * 0x9E3779B9u;
            hash ^= (uint) salt * 0x85EBCA6Bu;
            hash ^= hash >> 16;
            hash *= 0x7FEB352Du;
            hash ^= hash >> 15;
            hash *= 0x846CA68Bu;
            hash ^= hash >> 16;
            return (hash & 0x00FFFFFF) / (float) 0x01000000;
        }
    }

    private TimeSpan GetStartedAt(EntityUid uid, TimeSpan now)
    {
        if (_startedAt.TryGetValue(uid, out var startedAt))
            return startedAt;

        _startedAt[uid] = now;
        return now;
    }
}
