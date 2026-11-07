using System.Numerics;
using Content.Shared.CMU14.Threats.Mobs.Threat;
using Robust.Client.GameObjects;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Threats.Mobs.Threat;

public sealed class CMUThreatEmergenceSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprites = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly Dictionary<EntityUid, (Vector2 Scale, Vector2 Offset, Color Color)> _original = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUThreatEmergingComponent, ComponentShutdown>(OnShutdown);
    }

    public override void FrameUpdate(float frameTime)
    {
        var query = EntityQueryEnumerator<CMUThreatEmergingComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var emerging, out var sprite))
        {
            if (!_original.TryGetValue(uid, out var initial))
            {
                initial = (sprite.Scale, sprite.Offset, sprite.Color);
                _original.Add(uid, initial);
            }

            var duration = Math.Max(0.01, emerging.Duration.TotalSeconds);
            var progress = (float) Math.Clamp(1 - (emerging.EndsAt - _timing.CurTime).TotalSeconds / duration, 0, 1);
            var eased = progress * progress * (3 - 2 * progress);
            _sprites.SetScale((uid, sprite), initial.Scale * new Vector2(1, 0.15f + 0.85f * eased));
            _sprites.SetOffset((uid, sprite), initial.Offset + new Vector2(0, -0.5f * (1 - eased)));
            _sprites.SetColor((uid, sprite), initial.Color.WithAlpha(initial.Color.A * (0.2f + 0.8f * eased)));
        }
    }

    private void OnShutdown(Entity<CMUThreatEmergingComponent> ent, ref ComponentShutdown args)
    {
        if (!_original.Remove(ent.Owner, out var initial) || !TryComp(ent, out SpriteComponent? sprite))
            return;

        _sprites.SetScale((ent.Owner, sprite), initial.Scale);
        _sprites.SetOffset((ent.Owner, sprite), initial.Offset);
        _sprites.SetColor((ent.Owner, sprite), initial.Color);
    }
}
