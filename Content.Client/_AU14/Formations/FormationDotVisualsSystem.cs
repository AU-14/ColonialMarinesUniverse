using Content.Shared._AU14.Formations;
using Robust.Client.GameObjects;
using Robust.Shared.Timing;

namespace Content.Client._AU14.Formations;

/// <summary>
/// Updates the formation dot sprite each frame:
/// - Applies the leader's color tint to the dotArrow layer.
/// - Fades alpha linearly from full to zero as DeathTime approaches.
/// Skips entities where MaxLifetime is zero — those are client-side placement
/// ghost previews that manage their own color via the YAML sprite definition.
/// </summary>
public sealed partial class FormationDotVisualsSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    public override void FrameUpdate(float frameTime)
    {
        var query = EntityQueryEnumerator<FormationDotComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var dot, out var sprite))
        {
            // Skip placement-preview ghosts (MaxLifetime == 0 means not server-initialised).
            if (dot.MaxLifetime == TimeSpan.Zero) continue;

            var now = _timing.CurTime;
            var remaining = dot.DeathTime - now;
            var maxLifetime = dot.MaxLifetime.TotalSeconds;

            var alpha = (float)Math.Clamp(remaining.TotalSeconds / maxLifetime, 0, 1);
            var color = dot.DotColor.WithAlpha(alpha);
            _sprite.LayerSetColor((uid, sprite), "dotArrow", color);
        }
    }
}
