using System.Numerics;
using System.Linq;
using Content.Shared._CMU14.Yautja;
using Content.Shared.Clothing;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Item;
using Robust.Client.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Utility;

namespace Content.Client._CMU14.Yautja;

public sealed partial class YautjaMaskAccessoryVisualSystem : EntitySystem
{
    private const string LayerKey = "cmu-yautja-mask-accessory";
    public static readonly Vector2 OnMobOffset = new(0f, 0.5f);
    private static readonly ResPath OnMobRsi = new("_CMU14/Yautja/mask_accessories_onmob.rsi");

    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedItemSystem _item = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<YautjaMaskAccessoryHolderComponent, GetEquipmentVisualsEvent>(OnGetEquipmentVisuals, after: [typeof(ClothingSystem)]);
        SubscribeLocalEvent<YautjaMaskAccessoryHolderComponent, EntInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<YautjaMaskAccessoryHolderComponent, EntRemovedFromContainerMessage>(OnRemoved);
    }

    private void OnGetEquipmentVisuals(Entity<YautjaMaskAccessoryHolderComponent> ent, ref GetEquipmentVisualsEvent args)
    {
        if (!_container.TryGetContainer(ent, ent.Comp.ContainerId, out var container))
            return;

        var accessory = container.ContainedEntities.FirstOrDefault();
        if (!accessory.IsValid() ||
            !TryComp(accessory, out SpriteComponent? accessorySprite))
        {
            return;
        }

        var layer = accessorySprite.AllLayers.FirstOrDefault();
        var state = layer?.RsiState.Name;
        if (string.IsNullOrWhiteSpace(state) ||
            args.Layers.Any(layer => layer.Item1 == LayerKey))
        {
            return;
        }

        args.Layers.Add((LayerKey, new PrototypeLayerData
        {
            RsiPath = OnMobRsi.ToString(),
            State = $"equipped-{state}",
            Offset = OnMobOffset,
            Visible = true,
        }));
    }

    private void OnInserted(Entity<YautjaMaskAccessoryHolderComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        _item.VisualsChanged(ent);
    }

    private void OnRemoved(Entity<YautjaMaskAccessoryHolderComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        _item.VisualsChanged(ent);
    }
}
