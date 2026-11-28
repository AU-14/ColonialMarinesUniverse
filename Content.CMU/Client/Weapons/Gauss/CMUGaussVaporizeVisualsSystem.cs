using System.Numerics;
using Content.Shared.CMU14.Weapons.Gauss;
using Robust.Client.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Weapons.Gauss;

/// <summary>
///     Draws an M60 gauss gun vaporizing someone: they scorch black and shed embers and ash that drift up
///     off them, then fade out as they crumble.
/// </summary>
public sealed class CMUGaussVaporizeVisualsSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private CMUGaussVaporizingSystem _vaporizing = default!;

    private static readonly EntProtoId Ember = "CMUGaussVaporizeEmber";
    private static readonly EntProtoId Ash = "CMUGaussVaporizeAsh";

    private static readonly Color Charred = new(0.06f, 0.06f, 0.07f);

    /// <summary>How far through the effect they're fully blackened.</summary>
    private const float BlackenUntil = 0.35f;

    /// <summary>How far through the effect they start crumbling away.</summary>
    private const float FadeFrom = 0.8f;

    private const float SpawnInterval = 0.05f;

    private readonly List<(EntityUid Uid, Vector2 Velocity)> _particles = new();
    private float _spawnAccumulator;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUGaussVaporizingComponent, ComponentRemove>(OnRemove);
    }

    private void OnRemove(Entity<CMUGaussVaporizingComponent> ent, ref ComponentRemove args)
    {
        if (!TerminatingOrDeleted(ent) && TryComp(ent, out SpriteComponent? sprite))
            _sprite.SetColor((ent, sprite), Color.White);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        MoveParticles(frameTime);

        _spawnAccumulator += frameTime;
        var spawns = 0;
        while (_spawnAccumulator >= SpawnInterval)
        {
            _spawnAccumulator -= SpawnInterval;
            spawns++;
        }

        var query = EntityQueryEnumerator<CMUGaussVaporizingComponent, SpriteComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var sprite, out var xform))
        {
            var progress = _vaporizing.GetProgress(comp);
            _sprite.SetColor((uid, sprite), GetColor(progress));

            if (xform.MapID == MapId.Nullspace)
                continue;

            var origin = _transform.GetMapCoordinates(uid, xform);
            for (var i = 0; i < spawns; i++)
            {
                SpawnParticle(origin, progress);
            }
        }
    }

    private static Color GetColor(float progress)
    {
        var black = Math.Clamp(progress / BlackenUntil, 0f, 1f);
        var color = Color.InterpolateBetween(Color.White, Charred, black);

        if (progress > FadeFrom)
            color = color.WithAlpha(1f - (progress - FadeFrom) / (1f - FadeFrom));

        return color;
    }

    private void SpawnParticle(MapCoordinates origin, float progress)
    {
        // Mostly embers while they burn, mostly ash once they start to crumble.
        var ashChance = progress > FadeFrom ? 0.7f : 0.4f;
        var proto = _random.Prob(ashChance) ? Ash : Ember;

        var offset = new Vector2(_random.NextFloat(-0.3f, 0.3f), _random.NextFloat(-0.45f, 0.35f));
        var particle = Spawn(proto, origin.Offset(offset));

        // Both drift up and away off them.
        var velocity = new Vector2(_random.NextFloat(-0.15f, 0.15f), _random.NextFloat(0.35f, 0.7f));

        _particles.Add((particle, velocity));
    }

    private void MoveParticles(float frameTime)
    {
        for (var i = _particles.Count - 1; i >= 0; i--)
        {
            var (uid, velocity) = _particles[i];
            if (TerminatingOrDeleted(uid))
            {
                _particles.RemoveAt(i);
                continue;
            }

            if (velocity == Vector2.Zero)
                continue;

            var xform = Transform(uid);
            _transform.SetLocalPosition(uid, xform.LocalPosition + velocity * frameTime, xform);
        }
    }
}
