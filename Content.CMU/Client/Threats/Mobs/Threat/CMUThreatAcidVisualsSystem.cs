using System.Numerics;
using Content.Shared.CMU14.Threats.Mobs.Threat;
using Robust.Client.GameObjects;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Threats.Mobs.Threat;

public sealed class CMUThreatAcidVisualsSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprites = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly Dictionary<EntityUid, Vector2> _projectileOffsets = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUThreatSpitProjectileComponent, ComponentShutdown>(OnProjectileShutdown);
    }

    public override void FrameUpdate(float frameTime)
    {
        var projectiles = EntityQueryEnumerator<CMUThreatSpitProjectileComponent, SpriteComponent>();
        while (projectiles.MoveNext(out var uid, out var spit, out var sprite))
        {
            if (!_projectileOffsets.TryGetValue(uid, out var offset))
            {
                offset = sprite.Offset;
                _projectileOffsets.Add(uid, offset);
            }

            var duration = Math.Max(0.01, spit.Duration.TotalSeconds);
            var progress = (float) Math.Clamp(1 - (spit.EndsAt - _timing.CurTime).TotalSeconds / duration, 0, 1);
            _sprites.SetOffset((uid, sprite), offset + new Vector2(0, MathF.Sin(progress * MathF.PI) * 0.65f));
        }

    }

    private void OnProjectileShutdown(Entity<CMUThreatSpitProjectileComponent> ent, ref ComponentShutdown args)
    {
        if (_projectileOffsets.Remove(ent, out var offset) && TryComp(ent, out SpriteComponent? sprite))
            _sprites.SetOffset((ent.Owner, sprite), offset);
    }
}
