using System.Linq;
using Content.Shared._RMC14.Requisitions.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._RMC14.Requisitions;

public sealed partial class RequisitionsSystem
{
    /// <summary>Append whole-crate allowances without resetting an existing terminal's itemized or local stock.</summary>
    public void AddRoundLimitedCatalog(Entity<RequisitionsComputerComponent> computer, EntProtoId catalogId)
    {
        var catalog = (RequisitionsComputerComponent) _prototypeManager.Index(catalogId)
            .Components["RequisitionsComputer"].Component;
        var additions = _serialization.CreateCopy(catalog.Categories, notNullableOverride: true);
        var existing = computer.Comp.Categories.SelectMany(c => c.Entries).Select(e => e.Crate).ToHashSet();
        foreach (var category in additions)
            category.Entries.RemoveAll(e => e.RoundLimit <= 0 || existing.Contains(e.Crate));
        computer.Comp.Categories = computer.Comp.Categories.Concat(additions.Where(c => c.Entries.Count > 0)).ToList();
        computer.Comp.StockEntriesInitialized = false;
        Dirty(computer);
        SendUIState(computer);
    }
}
