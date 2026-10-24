using Content.Shared.CMU14.Hygiene;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.Hygiene;

public sealed partial class CMUResidueVisualSystem : EntitySystem
{
    private static readonly (string Key, string State)[] Layers =
    {
        ("cmu-body-residue", "uniform_blood"),
        ("cmu-body-residue-hands", "hands_blood"),
        ("cmu-body-residue-feet", "feet_blood"),
        ("cmu-body-residue-face", "mask_blood"),
    };

    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUResidueComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CMUResidueComponent, AfterAutoHandleStateEvent>(OnState);
        SubscribeLocalEvent<CMUResidueComponent, ComponentRemove>(OnRemove);
    }

    private void OnStartup(Entity<CMUResidueComponent> ent, ref ComponentStartup args)
        => Refresh(ent);
    private void OnState(Entity<CMUResidueComponent> ent, ref AfterAutoHandleStateEvent args)
        => Refresh(ent);

    private void OnRemove(Entity<CMUResidueComponent> ent, ref ComponentRemove args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;
        foreach (var (key, _) in Layers)
        {
            if (_sprite.LayerMapTryGet((ent.Owner, sprite), key, out var index, false))
                _sprite.RemoveLayer((ent.Owner, sprite), index);
        }
    }

    private void Refresh(Entity<CMUResidueComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;
        var spriteEnt = new Entity<SpriteComponent?>(ent.Owner, sprite);
        var color = ent.Comp.Color;
        var bright = color is { } stain ? CMUStainColors.Brighten(stain) : default;
        foreach (var (key, state) in Layers)
        {
            if (!_sprite.LayerMapTryGet(spriteEnt, key, out var index, false))
            {
                if (ent.Comp.Color == null)
                    return;
                index = _sprite.AddRsiLayer(spriteEnt, new RSI.StateId(state),
                    new ResPath("CMU14/Effects/item_stains.rsi"));
                _sprite.LayerMapSet(spriteEnt, key, index);
                sprite.LayerSetShader(index, "CMUResidueContrast");
            }
            _sprite.LayerSetVisible(spriteEnt, index, ent.Comp.Color != null);
            if (color != null)
            {
                _sprite.LayerSetColor(spriteEnt, index, bright.WithAlpha(
                    key == "cmu-body-residue-face"
                        ? (0.18f + 0.12f * ent.Comp.Buildup) * 0.25f
                        : 0.35f + 0.25f * ent.Comp.Buildup));
            }
        }
    }
}
