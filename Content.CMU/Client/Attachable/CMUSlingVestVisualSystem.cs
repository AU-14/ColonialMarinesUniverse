using System.Linq;
using Content.Shared.CMU14.Attachable;
using Content.Shared.Clothing;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Item;
using Robust.Client.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.Attachable;

public sealed class CMUSlingVestVisualSystem : EntitySystem
{
    private const string LayerKey = "cmu-sling-vest-rifle";
    private static readonly string[] WornStates = ["equipped-SUITSTORAGE", "equipped-BACKPACK"];

    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedItemSystem _item = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUSlingVestComponent, GetEquipmentVisualsEvent>(OnGetEquipmentVisuals,
            after: [typeof(ClothingSystem)]);
        SubscribeLocalEvent<CMUSlingVestComponent, EntInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<CMUSlingVestComponent, EntRemovedFromContainerMessage>(OnRemoved);
    }

    private void OnGetEquipmentVisuals(
        Entity<CMUSlingVestComponent> vest,
        ref GetEquipmentVisualsEvent args)
    {
        if (!_containers.TryGetContainer(vest, CMUSlingVestComponent.ContainerId, out var container) ||
            container.ContainedEntities.Count == 0 ||
            args.Layers.Any(layer => layer.Item1 == LayerKey))
        {
            return;
        }

        var rifle = container.ContainedEntities[0];
        if (!TryComp(rifle, out SpriteComponent? sprite) ||
            _sprite.LayerGetEffectiveRsi((rifle, sprite), 0) is not { } rsi)
        {
            return;
        }

        // layer 0 is the gun's icon, drawn over the body it smeared the item sprite across the whole mob.
        // use the gun's worn-on-body state like suit storage does
        string? state = null;
        foreach (var worn in WornStates)
        {
            if (rsi.TryGetState(worn, out _))
            {
                state = worn;
                break;
            }
        }

        if (state == null)
            return;

        args.Layers.Add((LayerKey, new PrototypeLayerData
        {
            RsiPath = rsi.Path.ToString(),
            State = state,
            Visible = true,
        }));
    }

    private void OnInserted(Entity<CMUSlingVestComponent> vest, ref EntInsertedIntoContainerMessage args)
        => _item.VisualsChanged(vest);

    private void OnRemoved(Entity<CMUSlingVestComponent> vest, ref EntRemovedFromContainerMessage args)
        => _item.VisualsChanged(vest);
}
