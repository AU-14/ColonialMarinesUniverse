using System.Numerics;
using Content.Shared.CMU14.WallLean;
using Robust.Client.GameObjects;
using DrawDepthTag = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client.CMU14.WallLean;

public sealed class CMUWallLeanVisualsSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private readonly Dictionary<EntityUid, (Vector2 Offset, int DrawDepth)> _base = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUWallLeaningComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnShutdown(Entity<CMUWallLeaningComponent> ent, ref ComponentShutdown args)
    {
        if (!_base.Remove(ent, out var original) || !TryComp<SpriteComponent>(ent, out var sprite))
            return;

        _sprite.SetOffset((ent, sprite), original.Offset);
        _sprite.SetDrawDepth((ent, sprite), original.DrawDepth);
    }

    public override void FrameUpdate(float frameTime)
    {
        var query = EntityQueryEnumerator<CMUWallLeaningComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var leaning, out var sprite))
        {
            if (!_base.TryGetValue(uid, out var original))
            {
                original = (sprite.Offset, sprite.DrawDepth);
                _base[uid] = original;
            }

            _sprite.SetOffset((uid, sprite), original.Offset + leaning.Offset);
            _sprite.SetDrawDepth((uid, sprite), leaning.BehindWall ? (int) DrawDepthTag.SmallMobs : original.DrawDepth);
        }
    }
}
