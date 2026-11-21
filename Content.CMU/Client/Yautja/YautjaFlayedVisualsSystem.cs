using System.Linq;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Humanoid;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.Yautja;

// draws the cmss13 dam_human flay overlay on a flayed corpse
public sealed partial class YautjaFlayedVisualsSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    private const string LayerKey = "cmu-yautja-flay";
    private static readonly ResPath Rsi = new("/Textures/CMU14/Yautja/flayed_human.rsi");

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaFlayedComponent, AfterAutoHandleStateEvent>(OnState);
        SubscribeLocalEvent<YautjaFlayedComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnState(Entity<YautjaFlayedComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateLayer(ent);
    }

    private void OnShutdown(Entity<YautjaFlayedComponent> ent, ref ComponentShutdown args)
    {
        if (TryComp(ent, out SpriteComponent? sprite))
            _sprite.RemoveLayer((ent, sprite), LayerKey, false);
    }

    public void UpdateLayer(Entity<YautjaFlayedComponent> ent)
    {
        // severed limbs carry the comp at stage 0, only bodies get the overlay
        if (ent.Comp.Stage is < 1 or > 3 ||
            !HasComp<HumanoidProfileComponent>(ent) ||
            !TryComp(ent, out SpriteComponent? sprite))
        {
            return;
        }

        // re-added each stage because it moves: the first cuts sit under the uniform like in cmss13,
        // after the strip the uniform's gone and the overlay has to cover the hands and feet too
        _sprite.RemoveLayer((ent, sprite), LayerKey, false);

        var index = sprite.AllLayers.Count();
        if (ent.Comp.Stage == 1 && _sprite.LayerMapTryGet((ent, sprite), "jumpsuit", out var jumpsuit, false))
            index = jumpsuit;
        else if (_sprite.LayerMapTryGet((ent, sprite), HumanoidVisualLayers.Overlay, out var overlay, false))
            index = overlay;

        _sprite.AddBlankLayer((ent, sprite), index);
        _sprite.LayerMapSet((ent, sprite), LayerKey, index);
        _sprite.LayerSetSprite((ent, sprite), index, new SpriteSpecifier.Rsi(Rsi, $"human_{ent.Comp.Stage}"));
    }
}
