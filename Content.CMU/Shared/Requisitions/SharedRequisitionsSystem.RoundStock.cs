using Content.Shared._RMC14.Requisitions.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Requisitions;

public abstract partial class SharedRequisitionsSystem
{
    private readonly Dictionary<(string Faction, EntProtoId Crate), int> _roundPurchases = new();

    protected int GetRoundStock(Entity<RequisitionsComputerComponent> computer, RequisitionsEntry entry)
        => Math.Max(0, entry.RoundLimit - _roundPurchases.GetValueOrDefault((computer.Comp.Faction, entry.Crate)));

    protected bool TryTakeRoundStock(Entity<RequisitionsComputerComponent> computer, RequisitionsEntry entry)
    {
        if (GetRoundStock(computer, entry) <= 0)
            return false;
        var key = (computer.Comp.Faction, entry.Crate);
        _roundPurchases[key] = _roundPurchases.GetValueOrDefault(key) + 1;
        return true;
    }
}
