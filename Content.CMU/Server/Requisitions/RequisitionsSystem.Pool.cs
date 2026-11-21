using System.Linq;
using Content.Shared._RMC14.Requisitions;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared.Cargo.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._RMC14.Requisitions;

public sealed partial class RequisitionsSystem
{
    private static readonly EntProtoId PoolTableParts = "CMUPoolTableParts";

    internal void AddPoolTableParts(EntityUid console)
    {
        const string categoryName = "Furniture";
        if (!HasComp<CargoOrderConsoleComponent>(console)
            || !TryComp<RequisitionsComputerComponent>(console, out var computer)
            || computer.Categories.Any(category => category.Entries.Any(entry => entry.Crate == PoolTableParts)))
            return;

        if (!computer.Categories.Any(category => category.Name == categoryName))
            computer.Categories.Add(new RequisitionsCategory { Name = categoryName });

        AddEntryToCategory(console, computer, categoryName, new RequisitionsEntry
        {
            Crate = PoolTableParts,

            Cost = 21,
        });
    }
}
