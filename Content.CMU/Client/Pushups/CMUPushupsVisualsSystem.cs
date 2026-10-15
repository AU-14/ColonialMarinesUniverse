using System.Numerics;
using Content.Shared.CMU14.Pushups;
using Robust.Client.GameObjects;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Pushups;

public sealed partial class CMUPushupsVisualsSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    private const float PushupHeight = 0.08f;
    private const float SitupHeight = 0.04f;

    private readonly Dictionary<EntityUid, Vector2> _baseOffsets = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUPushupsComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnShutdown(Entity<CMUPushupsComponent> ent, ref ComponentShutdown args)
    {
        Restore(ent);
    }

    public override void FrameUpdate(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CMUPushupsComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var pushups, out var sprite))
        {
            if (!pushups.Active)
            {
                Restore(uid);
                continue;
            }

            if (!_baseOffsets.TryGetValue(uid, out var baseOffset))
            {
                baseOffset = sprite.Offset;
                _baseOffsets[uid] = baseOffset;
            }

            var duration = pushups.RepDuration.TotalSeconds;
            var phase = duration <= 0 ? 0 : Math.Clamp((now - pushups.RepStart).TotalSeconds / duration, 0, 1);
            var height = pushups.Exercise == CMUExercise.Situps ? SitupHeight : PushupHeight;
            var lift = (float) Math.Sin(phase * Math.PI) * height;
            _sprite.SetOffset((uid, sprite), baseOffset + new Vector2(0, lift));
        }
    }

    private void Restore(EntityUid uid)
    {
        if (!_baseOffsets.Remove(uid, out var baseOffset))
            return;

        if (TryComp<SpriteComponent>(uid, out var sprite))
            _sprite.SetOffset((uid, sprite), baseOffset);
    }
}
