using Content.Shared.CMU14.ColonyEconomy;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.ColonyEconomy;

public sealed partial class DepartmentConsoleSystem
{
    [Dependency] private SharedHandsSystem _hands = default!;

    // ASRS category indices are non-negative; this category is fulfilled by the console itself.
    private const int SupplyCategory = -1;

    private void AddSupplyCatalog(DepartmentConsoleComponent console, List<DepartmentOrderCatalogCategory> catalog)
    {
        if (console.Supplies.Count == 0)
            return;

        var entries = new List<DepartmentOrderCatalogEntry>();
        for (var i = 0; i < console.Supplies.Count; i++)
        {
            var supply = console.Supplies[i];
            entries.Add(new DepartmentOrderCatalogEntry(SupplyCategory, i,
                _prototypeManager.Index(supply.Item).Name, supply.Cost));
        }

        catalog.Add(new DepartmentOrderCatalogCategory(Loc.GetString("cmu-department-console-supplies"), entries));
    }

    public bool TryBuySupply(Entity<DepartmentConsoleComponent> console, EntityUid buyer, int index)
    {
        if (!_accessReader.IsAllowed(buyer, console.Owner) ||
            index < 0 || index >= console.Comp.Supplies.Count)
            return false;

        var supply = console.Comp.Supplies[index];
        if (supply.Cost < 0 || console.Comp.DepartmentBudget < supply.Cost)
            return false;

        var item = Spawn(supply.Item, Transform(console).Coordinates);
        console.Comp.DepartmentBudget -= supply.Cost;
        _hands.PickupOrDrop(buyer, item);
        UpdateAllUiForDepartment(console, console.Comp);
        return true;
    }
}
