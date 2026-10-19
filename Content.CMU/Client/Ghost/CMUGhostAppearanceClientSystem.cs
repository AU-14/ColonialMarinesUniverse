using System.Linq;
using System.Numerics;
using Content.Shared.CMU14.Ghost;
using Robust.Client.GameObjects;

namespace Content.Client.CMU14.Ghost;

/// <summary>
///     Dresses a ghost in the sprite of the body it left, once that body is known to this client. The ghost keeps its
///     own draw depth and visibility, and stands upright.
/// </summary>
public sealed class CMUGhostAppearanceClientSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    private readonly HashSet<EntityUid> _applied = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUGhostAppearanceComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnShutdown(Entity<CMUGhostAppearanceComponent> ent, ref ComponentShutdown args)
    {
        _applied.Remove(ent);
    }

    public override void FrameUpdate(float frameTime)
    {
        var query = EntityQueryEnumerator<CMUGhostAppearanceComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var ghost, out var sprite))
        {
            if (_applied.Contains(uid) ||
                ghost.Source is not { } source ||
                !TryComp(source, out SpriteComponent? sourceSprite) ||
                !sourceSprite.AllLayers.Any())
            {
                continue;
            }

            _applied.Add(uid);

            var drawDepth = sprite.DrawDepth;
            var visible = sprite.Visible;
            _sprite.CopySprite((source, sourceSprite), (uid, sprite));
            _sprite.SetDrawDepth((uid, sprite), drawDepth);
            _sprite.SetVisible((uid, sprite), visible);
            _sprite.SetRotation((uid, sprite), Angle.Zero);
            _sprite.SetOffset((uid, sprite), Vector2.Zero);
        }
    }
}
