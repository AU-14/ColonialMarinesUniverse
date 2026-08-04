using System.Linq;
using Content.Client.Clothing;
using Content.Client.Items.Systems;
using Content.Shared._CMU14.Item.Stain;
using Content.Shared.Clothing;
using Content.Shared.Item;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._CMU14.Item.Stain;

/// <summary>
/// Renders clipped stains on item sprites and directional stains on equipped clothing.
/// </summary>
public sealed partial class CMUItemStainVisualizerSystem : EntitySystem
{
    private static readonly ProtoId<ShaderPrototype> ItemStainShader = "CMUItemStain";
    private static readonly ResPath ItemStainsRsi = new("_CMU14/Effects/item_stains.rsi");

    private const string ItemBloodState = "itemblood";
    private const string LayerPrefix = "cmu-item-stain";

    [Dependency] private ItemSystem _item = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUItemStainComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CMUItemStainComponent, ComponentRemove>(OnRemove);
        SubscribeLocalEvent<CMUItemStainComponent, AfterAutoHandleStateEvent>(OnAfterState);
        SubscribeLocalEvent<CMUItemStainComponent, AppearanceChangeEvent>(OnAppearanceChange);
        SubscribeLocalEvent<CMUItemStainComponent, GetEquipmentVisualsEvent>(OnGetEquipmentVisuals,
            after: [typeof(ClientClothingSystem)]);
    }

    private void OnStartup(Entity<CMUItemStainComponent> ent, ref ComponentStartup args)
    {
        Refresh(ent);
    }

    private void OnRemove(Entity<CMUItemStainComponent> ent, ref ComponentRemove args)
    {
        if (TryComp<SpriteComponent>(ent, out var sprite))
            ClearWorldLayers((ent.Owner, sprite));

        _item.VisualsChanged(ent);
    }

    private void OnAfterState(Entity<CMUItemStainComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        Refresh(ent);
    }

    private void OnAppearanceChange(Entity<CMUItemStainComponent> ent, ref AppearanceChangeEvent args)
    {
        if (args.Sprite != null)
            RebuildWorldLayers(ent, args.Sprite);
    }

    private void OnGetEquipmentVisuals(Entity<CMUItemStainComponent> ent, ref GetEquipmentVisualsEvent args)
    {
        if (ent.Comp.Color is not { } color)
            return;

        var state = GetWornState(ent.Comp, args.Slot);
        if (state == null)
            return;

        var key = $"{LayerPrefix}-equipped-{args.Slot}";
        var layer = new PrototypeLayerData
        {
            RsiPath = ItemStainsRsi.ToString(),
            State = state,
            Color = color,
            MapKeys = [key],
        };
        args.Layers.Add((key, layer));
    }

    private void Refresh(Entity<CMUItemStainComponent> ent)
    {
        _item.VisualsChanged(ent);
        if (TryComp<SpriteComponent>(ent, out var sprite))
            RebuildWorldLayers(ent, sprite);
    }

    private void RebuildWorldLayers(Entity<CMUItemStainComponent> ent, SpriteComponent sprite)
    {
        var spriteEnt = new Entity<SpriteComponent?>(ent.Owner, sprite);
        ClearWorldLayers((ent.Owner, sprite));

        if (ent.Comp.Color is not { } color || !_prototypes.TryIndex(ItemStainShader, out var shaderPrototype))
            return;

        var sourceLayers = sprite.AllLayers
            .OfType<SpriteComponent.Layer>()
            .Where(layer => layer.Visible && !layer.Blank && layer.CopyToShaderParameters == null)
            .ToArray();
        if (sourceLayers.Length == 0)
            return;

        var visuals = EnsureComp<CMUItemStainVisualsComponent>(ent);
        for (var i = 0; i < sourceLayers.Length; i++)
        {
            var source = sourceLayers[i];
            var overlayIndex = AddSourceClone(spriteEnt, source);
            if (overlayIndex < 0)
                continue;

            var overlayKey = $"{LayerPrefix}-overlay-{i}";
            _sprite.LayerMapSet(spriteEnt, overlayKey, overlayIndex);
            visuals.LayerKeys.Add(overlayKey);

            var shader = shaderPrototype.InstanceUnique();
            shader.SetParameter("stainColor", color.RGBA);
            sprite.LayerSetShader(overlayIndex, shader, ItemStainShader.Id);

            // Shader-input layers must precede their target so their texture and UVs are populated before it draws.
            var maskIndex = _sprite.AddRsiLayer(
                spriteEnt,
                new RSI.StateId(ItemBloodState),
                ItemStainsRsi,
                overlayIndex);
            if (maskIndex < 0 || !_sprite.TryGetLayer(spriteEnt, maskIndex, out var maskLayer, false))
                continue;

            var maskKey = $"{LayerPrefix}-mask-{i}";
            _sprite.LayerMapSet(spriteEnt, maskKey, maskIndex);
            visuals.LayerKeys.Add(maskKey);
            maskLayer.CopyToShaderParameters = new SpriteComponent.CopyToShaderParameters(overlayKey)
            {
                ParameterTexture = "stainMask",
                ParameterUV = "stainMaskUV",
            };
        }
    }

    private int AddSourceClone(Entity<SpriteComponent?> sprite, SpriteComponent.Layer source)
    {
        int index;
        if (source.State.IsValid && source.ActualRsi != null)
            index = _sprite.AddRsiLayer(sprite, source.State, source.ActualRsi);
        else if (source.Texture != null)
            index = _sprite.AddTextureLayer(sprite, source.Texture);
        else
            return -1;

        _sprite.LayerSetScale(sprite, index, source.Scale);
        _sprite.LayerSetRotation(sprite, index, source.Rotation);
        _sprite.LayerSetOffset(sprite, index, source.Offset);
        _sprite.LayerSetDirOffset(sprite, index, source.DirOffset);
        _sprite.LayerSetRenderingStrategy(sprite, index, source.RenderingStrategy);
        _sprite.LayerSetAutoAnimated(sprite, index, source.AutoAnimated);
        _sprite.LayerSetAnimationTime(sprite, index, source.AnimationTime);

        if (_sprite.TryGetLayer(sprite, index, out var clone, false))
        {
            clone.Cycle = source.Cycle;
            clone.Loop = source.Loop;
        }

        return index;
    }

    private void ClearWorldLayers(Entity<SpriteComponent> sprite)
    {
        if (!TryComp<CMUItemStainVisualsComponent>(sprite, out var visuals))
            return;

        var indices = new List<int>();
        foreach (var key in visuals.LayerKeys)
        {
            if (_sprite.LayerMapTryGet(sprite.AsNullable(), key, out var index, false))
                indices.Add(index);
        }

        indices.Sort(static (a, b) => b.CompareTo(a));
        foreach (var index in indices.Distinct())
            _sprite.RemoveLayer(sprite.AsNullable(), index, false);

        visuals.LayerKeys.Clear();
    }

    private static string? GetWornState(CMUItemStainComponent component, string slot)
    {
        if (component.WornStates.TryGetValue(slot, out var state))
            return state;

        return slot switch
        {
            CMUItemStainSystem.JumpsuitSlot => "uniform_blood",
            CMUItemStainSystem.GlovesSlot => "hands_blood",
            CMUItemStainSystem.ShoesSlot => "feet_blood",
            CMUItemStainSystem.MaskSlot => "mask_blood",
            CMUItemStainSystem.HeadSlot => "helmet_blood",
            CMUItemStainSystem.OuterClothingSlot => "suit_blood",
            _ => null,
        };
    }
}
