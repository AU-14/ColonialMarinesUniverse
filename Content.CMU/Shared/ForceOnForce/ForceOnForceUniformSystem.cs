using Content.Shared._RMC14.Marines;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;

namespace Content.Shared.CMU14.ForceOnForce;

public sealed partial class ForceOnForceUniformSystem : EntitySystem
{
    [Dependency] private InventorySystem _inventory = default!;

    public bool IsUnidentified(EntityUid target, EntityUid? viewer)
    {
        if (TryComp<MobStateComponent>(target, out var mob) && mob.CurrentState == MobState.Dead)
            return false;

        // Only FoF viewers have a uniform policy. Friendly personnel keep their normal identifiers.
        if (!TryComp<ForceOnForceUniformComponent>(viewer, out var policy) ||
            !TryComp<MarineComponent>(viewer, out var viewingMarine) ||
            !TryComp<MarineComponent>(target, out var targetMarine) ||
            string.IsNullOrEmpty(viewingMarine.Faction) ||
            string.IsNullOrEmpty(targetMarine.Faction) ||
            string.Equals(viewingMarine.Faction, targetMarine.Faction, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!_inventory.TryGetSlotEntity(target, "jumpsuit", out var uniform) ||
            MetaData(uniform.Value).EntityPrototype is not { } prototype)
            return true;
        // Uniforms of another faction can inherit a friendly uniform's components.
        // Recognize the issued/catalogued items themselves, not every descendant.
        return !policy.Uniforms.Contains(prototype.ID);
    }
}
