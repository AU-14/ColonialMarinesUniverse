using Content.Shared.Body.Systems;
using Content.Shared.CMU14.GasMask;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared.Inventory;
using Robust.Shared.Random;

namespace Content.Shared.CMU14.Xenomorphs.Pathogen.Mycotoxin;

/// <summary>
/// Single owner of the spore protection rule. Spore clouds and sporecaster stripping ask it;
/// the gas path reuses its wound check. The stage order (wound, internals, filter, gear)
/// lives here so the paths cannot give contradicting verdicts again.
/// </summary>
public sealed partial class CMUPathogenSporeProtectionSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly SharedCMUWoundsSystem _wounds = default!;
    [Dependency] private readonly SharedGasMaskSystem _gasMask = default!;

    public bool IsProtected(EntityUid target)
        => IsProtected(target, out _, out _);

    /// <summary>
    /// Single ordered spore rule: open wounds leak first, breathing internals block, a
    /// working spore filter blocks (returned so the caller can drain it), then worn
    /// protection: full blocks, two partials block, a single partial rolls its chance.
    /// </summary>
    public bool IsProtected(EntityUid target, out EntityUid? filterItem, out GasMaskFilterComponent? filter)
    {
        filterItem = null;
        filter = null;

        if (HasOpenWound(target))
            return false;

        if (_gasMask.IsBreathingInternals(target))
            return true;

        if (_gasMask.TryGetWornSporeFilter(target, out var worn))
        {
            filterItem = worn.Owner;
            filter = worn.Comp;
            return true;
        }

        MycotoxinProtectionComponent? single = null;
        var count = 0;

        foreach (var slot in new[] { "mask", "head" })
        {
            if (!_inventory.TryGetSlotEntity(target, slot, out var item))
                continue;

            if (!TryComp(item, out MycotoxinProtectionComponent? protection))
                continue;

            if (protection.FullProtection)
                return true;

            count++;
            single ??= protection;
        }

        if (count >= 2)
            return true;

        if (count == 1)
            return _random.Prob(single!.PartialBlockChance);

        return false;
    }

    public bool HasOpenWound(EntityUid target)
    {
        foreach (var (partUid, _) in _body.GetBodyChildren(target))
        {
            if (_wounds.HasOpenWound(partUid))
                return true;
        }

        return false;
    }
}
