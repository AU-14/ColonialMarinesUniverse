using System.Linq;
using Content.Shared.Interaction;
using Content.Shared.Materials;
using Content.Shared.Stacks;

namespace Content.Server.CMU14.Power;

public sealed partial class CMUSolidFuelStorageSystem : EntitySystem
{
    [Dependency] private SharedMaterialStorageSystem _materials = default!;
    [Dependency] private SharedStackSystem _stacks = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUSolidFuelStorageComponent, InteractUsingEvent>(OnInteractUsing);
    }

    private void OnInteractUsing(Entity<CMUSolidFuelStorageComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled ||
            !TryComp<MaterialStorageComponent>(ent, out var storage) ||
            !TryComp<MaterialComponent>(args.Used, out _) ||
            !TryComp<PhysicalCompositionComponent>(args.Used, out var composition) ||
            !TryComp<StackComponent>(args.Used, out var stack))
            return;

        var volume = composition.MaterialComposition.Values.Sum();
        if (volume <= 0)
            return;

        var count = stack.Count;
        if (storage.StorageLimit is { } limit)
        {
            var used = _materials.GetStoredMaterials((ent.Owner, storage), localOnly: true).Values.Sum();
            count = Math.Min(count, Math.Max(0, limit - used) / volume);
        }

        if (count <= 0)
            return;

        foreach (var (material, amount) in composition.MaterialComposition)
        {
            if (!_materials.CanChangeMaterialAmount(ent, material, amount * count, storage))
                return;
        }

        if (count == stack.Count)
        {
            args.Handled = _materials.TryInsertMaterialEntity(args.User, args.Used, ent, storage);
            return;
        }

        // Reuse normal insertion for its whitelist, feedback and material-change events.
        // The unconsumed portion stays in the user's hand.
        if (_stacks.Split((args.Used, stack), count, Transform(args.Used).Coordinates, args.User) is not { } fuel)
            return;

        args.Handled = _materials.TryInsertMaterialEntity(args.User, fuel, ent, storage);
        if (!args.Handled)
            _stacks.TryMergeStacks(fuel, args.Used, out _);
    }
}
