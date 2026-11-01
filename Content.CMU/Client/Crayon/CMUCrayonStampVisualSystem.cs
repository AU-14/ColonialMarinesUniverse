using System.Linq;
using Content.Shared.CMU14.Crayon;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;

namespace Content.Client.CMU14.Crayon;

public sealed class CMUCrayonStampVisualSystem : EntitySystem
{
    [Dependency] private IClyde _clyde = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    private readonly Dictionary<EntityUid, OwnedTexture> _textures = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUCrayonStampComponent, AfterAutoHandleStateEvent>(OnState);
        SubscribeLocalEvent<CMUCrayonStampComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnState(Entity<CMUCrayonStampComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (!ent.Comp.Symbol.IsValid() || !TryComp(ent, out SpriteComponent? sprite))
            return;

        Release(ent);
        var texture = CMUCrayonSymbolImage.ToTexture(_clyde, ent.Comp.Symbol);
        _textures[ent] = texture;

        if (sprite.AllLayers.Any())
            _sprite.LayerSetTexture((ent, sprite), 0, texture);
        else
            _sprite.AddTextureLayer((ent, sprite), texture);
    }

    private void OnShutdown(Entity<CMUCrayonStampComponent> ent, ref ComponentShutdown args)
    {
        if (!TerminatingOrDeleted(ent) && TryComp(ent, out SpriteComponent? sprite) && sprite.AllLayers.Any())
            _sprite.LayerSetVisible((ent, sprite), 0, false);

        Release(ent);
    }

    private void Release(EntityUid uid)
    {
        if (_textures.Remove(uid, out var texture))
            texture.Dispose();
    }
}
