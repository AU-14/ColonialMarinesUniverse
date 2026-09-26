using Content.Shared.Inventory;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.ForceOnForce;

public sealed partial class ForceOnForceUniformSystem : EntitySystem
{
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    public bool IsUnidentified(EntityUid user)
    {
        if (!TryComp<ForceOnForceUniformComponent>(user, out var policy))
            return false;
        if (!_inventory.TryGetSlotEntity(user, "jumpsuit", out var uniform) ||
            MetaData(uniform.Value).EntityPrototype is not { } prototype)
            return true;
        if (policy.Uniforms.Contains(prototype.ID))
            return false;
        foreach (var parent in _prototypes.EnumerateParents<EntityPrototype>(prototype.ID))
        {
            if (policy.Uniforms.Contains(parent.ID))
                return false;
        }
        return true;
    }
}
