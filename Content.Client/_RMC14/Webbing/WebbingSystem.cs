using Content.Client.Clothing;
using Content.Client.Items.Systems;
using Content.Shared._RMC14.Webbing;
using Content.Shared.Clothing;
using Content.Shared.Inventory.Events;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using static Robust.Shared.Utility.SpriteSpecifier;

namespace Content.Client._RMC14.Webbing;

public sealed partial class WebbingSystem : SharedWebbingSystem
{
    [Dependency] private ItemSystem _item = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    public event Action? PlayerWebbingUpdated;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WebbingClothingComponent, AfterAutoHandleStateEvent>(OnClothingState);
        SubscribeLocalEvent<WebbingClothingComponent, GetEquipmentVisualsEvent>(OnWebbingClothingEquipmentVisuals,
            after: [typeof(ClientClothingSystem)]);
        SubscribeLocalEvent<WebbingClothingComponent, GotEquippedEvent>(OnClothingEquipped);
        SubscribeLocalEvent<WebbingClothingComponent, GotUnequippedEvent>(OnClothingUnequipped);

        SubscribeLocalEvent<WebbingTransferComponent, ComponentRemove>(OnWebbingTransferRemove);
    }

    private void OnWebbingClothingEquipmentVisuals(Entity<WebbingClothingComponent> ent, ref GetEquipmentVisualsEvent args)
    {
        if (!TryComp(ent.Comp.Webbing, out WebbingComponent? webbing))
        {
            return;
        }


        if (webbing.PlayerSprite == null && TryComp(ent.Comp.Webbing, out SpriteComponent? webbingSprite))
        {
            webbing.PlayerSprite = new(webbingSprite.BaseRSI?.Path ?? new ResPath("_RMC14/Objects/Clothing/Webbing/webbing.rsi"), "equipped");
        }

        if (webbing.PlayerSprite is not { } sprite)
        {
            return;
        }

        if (TryComp(ent, out SpriteComponent? clothingSprite) &&
                _sprite.LayerMapTryGet((ent.Owner, clothingSprite), WebbingVisualLayers.Base, out var clothingLayer, false))
            {
                _sprite.LayerSetVisible((ent.Owner, clothingSprite), clothingLayer, true);
                _sprite.LayerSetRsi((ent.Owner, clothingSprite), clothingLayer, sprite.RsiPath);
                _sprite.LayerSetRsiState((ent.Owner, clothingSprite), clothingLayer, sprite.RsiState);
            }

        args.Layers.Add(($"enum.{nameof(WebbingVisualLayers)}.{nameof(WebbingVisualLayers.Base)}", new PrototypeLayerData
        {
            RsiPath = sprite.RsiPath.CanonPath,
            State = sprite.RsiState,
        }));
    }

    private void OnClothingState(Entity<WebbingClothingComponent> clothing, ref AfterAutoHandleStateEvent args)
    {
        if (TryComp(clothing, out SpriteComponent? clothingSprite) &&
            _sprite.LayerMapTryGet((clothing.Owner, clothingSprite), WebbingVisualLayers.Base, out var clothingLayer, false))
        {
            if (TryComp(clothing.Comp.Webbing, out WebbingComponent? webbing) &&
                webbing.PlayerSprite is { } rsi)
            {
                _sprite.LayerSetVisible((clothing.Owner, clothingSprite), clothingLayer, true);
                _sprite.LayerSetRsi((clothing.Owner, clothingSprite), clothingLayer, rsi.RsiPath);
                _sprite.LayerSetRsiState((clothing.Owner, clothingSprite), clothingLayer, rsi.RsiState);
            }
            else
            {
                _sprite.LayerSetVisible((clothing.Owner, clothingSprite), clothingLayer, false);
            }
        }

        _item.VisualsChanged(clothing);
        PlayerWebbingUpdated?.Invoke();
    }

    private void OnClothingEquipped(Entity<WebbingClothingComponent> clothing, ref GotEquippedEvent args)
    {
        if (_player.LocalEntity == args.Equipee)
            PlayerWebbingUpdated?.Invoke();
    }

    private void OnClothingUnequipped(Entity<WebbingClothingComponent> clothing, ref GotUnequippedEvent args)
    {
        if (_player.LocalEntity == args.Equipee)
            PlayerWebbingUpdated?.Invoke();
    }

    protected override void OnClothingInserted(Entity<WebbingClothingComponent> clothing, ref EntInsertedIntoContainerMessage args)
    {
        base.OnClothingInserted(clothing, ref args);

        if (_player.LocalEntity == args.Container.Owner)
            PlayerWebbingUpdated?.Invoke();
    }

    protected override void OnClothingRemoved(Entity<WebbingClothingComponent> clothing, ref EntRemovedFromContainerMessage args)
    {
        base.OnClothingRemoved(clothing, ref args);

        if (_player.LocalEntity == args.Container.Owner)
            PlayerWebbingUpdated?.Invoke();
    }

    private void OnWebbingTransferRemove(Entity<WebbingTransferComponent> ent, ref ComponentRemove args)
    {
        PlayerWebbingUpdated?.Invoke();
    }
}
