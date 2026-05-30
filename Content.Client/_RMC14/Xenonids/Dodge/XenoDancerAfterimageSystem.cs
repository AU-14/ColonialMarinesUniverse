using System.Numerics;
using Content.Shared._RMC14.Xenonids.Dodge;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Animations;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;
using static Robust.Client.Animations.AnimationTrackProperty;

namespace Content.Client._RMC14.Xenonids.Dodge;

public sealed partial class XenoDancerAfterimageSystem : EntitySystem
{
    private const string AfterimageAnimationKey = "rmc-xeno-dancer-afterimage";
    private const float AfterimageAlpha = 200f / 255f;
    private const float AfterimageFadeSeconds = 0.3f;
    private const float AfterimageLifetimeSeconds = 0.35f;
    private const float MinMoveDistance = 0.18f;
    private static readonly TimeSpan AfterimageInterval = TimeSpan.FromSeconds(0.1);

    [Dependency] private AnimationPlayerSystem _animation = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private TransformSystem _transform = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly Dictionary<EntityUid, AfterimageState> _states = new();
    private readonly HashSet<EntityUid> _active = new();
    private readonly List<EntityUid> _remove = new();

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        _active.Clear();

        var query = EntityQueryEnumerator<XenoActiveDodgeComponent, TransformComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out _, out var xform, out var sprite))
        {
            _active.Add(uid);
            ProcessDodgeAfterimage(uid, xform, sprite);
        }

        _remove.Clear();
        foreach (var uid in _states.Keys)
        {
            if (!_active.Contains(uid))
                _remove.Add(uid);
        }

        foreach (var uid in _remove)
        {
            _states.Remove(uid);
        }
    }

    private void ProcessDodgeAfterimage(EntityUid uid, TransformComponent xform, SpriteComponent sprite)
    {
        var map = _transform.GetMapCoordinates(uid, xform);
        if (!_states.TryGetValue(uid, out var state) || state.MapId != map.MapId)
        {
            _states[uid] = new AfterimageState(xform.Coordinates, map.MapId, map.Position, _timing.CurTime + AfterimageInterval);
            return;
        }

        if (_timing.CurTime < state.NextAt ||
            (map.Position - state.LastMapPosition).Length() < MinMoveDistance)
        {
            return;
        }

        SpawnAfterimage(uid, sprite, xform, state.LastCoordinates);

        state.LastCoordinates = xform.Coordinates;
        state.LastMapPosition = map.Position;
        state.NextAt = _timing.CurTime + AfterimageInterval;
    }

    private void SpawnAfterimage(EntityUid source, SpriteComponent sourceSprite, TransformComponent sourceXform, EntityCoordinates coordinates)
    {
        if (!coordinates.IsValid(EntityManager))
            return;

        var clone = Spawn("clientsideclone", coordinates);
        var cloneSprite = Comp<SpriteComponent>(clone);
        _sprite.CopySprite((source, sourceSprite), (clone, cloneSprite));
        _sprite.SetVisible((clone, cloneSprite), true);
        _sprite.SetColor((clone, cloneSprite), sourceSprite.Color.WithAlpha(AfterimageAlpha));

        var shimmer = new Vector2(_random.NextFloat(-0.08f, 0.08f), _random.NextFloat(-0.08f, 0.08f));
        _sprite.SetOffset((clone, cloneSprite), sourceSprite.Offset + shimmer);

        _transform.SetLocalRotationNoLerp(clone, sourceXform.LocalRotation);

        var despawn = EnsureComp<TimedDespawnComponent>(clone);
        despawn.Lifetime = AfterimageLifetimeSeconds;

        var animations = Comp<AnimationPlayerComponent>(clone);
        var startColor = cloneSprite.Color;
        _animation.Play((clone, animations), new Animation
        {
            Length = TimeSpan.FromSeconds(AfterimageFadeSeconds),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Color),
                    InterpolationMode = AnimationInterpolationMode.Linear,
                    KeyFrames =
                    {
                        new KeyFrame(startColor, 0),
                        new KeyFrame(startColor.WithAlpha(0), AfterimageFadeSeconds),
                    },
                },
            },
        }, AfterimageAnimationKey);
    }

    private sealed class AfterimageState(
        EntityCoordinates lastCoordinates,
        MapId mapId,
        Vector2 lastMapPosition,
        TimeSpan nextAt)
    {
        public EntityCoordinates LastCoordinates = lastCoordinates;
        public MapId MapId = mapId;
        public Vector2 LastMapPosition = lastMapPosition;
        public TimeSpan NextAt = nextAt;
    }
}
