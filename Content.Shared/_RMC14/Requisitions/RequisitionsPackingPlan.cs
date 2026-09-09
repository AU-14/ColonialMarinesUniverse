using System.Linq;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Requisitions;

/// <summary>
/// Deterministic itemized ASRS packing used by both the client preview and server checkout.
/// </summary>
public static class RequisitionsPackingPlan
{
    public static RequisitionsPackedOrder Build(
        IEnumerable<(RequisitionsItemEntry Item, int Amount)> requests,
        int weightLimit)
    {
        var result = new RequisitionsPackedOrder(Math.Max(1, weightLimit));
        var packable = new List<RequisitionsItemEntry>();

        foreach (var (item, amount) in requests.OrderBy(request => request.Item.Prototype.Id))
        {
            for (var i = 0; i < amount; i++)
            {
                if (item.Packable && item.Weight <= result.WeightLimit)
                    packable.Add(item);
                else
                    result.Loose.Add(new RequisitionsPackedLoose(item.Prototype, item.Weight));
            }
        }

        foreach (var item in packable
                     .OrderByDescending(item => item.Weight)
                     .ThenBy(item => item.Prototype.Id))
        {
            var crate = result.Crates.FirstOrDefault(crate =>
                crate.Weight + item.Weight <= result.WeightLimit);
            if (crate == null)
            {
                crate = new RequisitionsPackedCrate();
                result.Crates.Add(crate);
            }

            crate.Items.Add(item.Prototype);
            crate.Weight += item.Weight;
        }

        return result;
    }
}

public sealed class RequisitionsPackedOrder(int weightLimit)
{
    public int WeightLimit { get; } = weightLimit;
    public List<RequisitionsPackedCrate> Crates { get; } = new();
    public List<RequisitionsPackedLoose> Loose { get; } = new();
    public int ShipmentCount => Crates.Count + Loose.Count;
    public int TotalWeight => Crates.Sum(crate => crate.Weight) + Loose.Sum(item => item.Weight);
}

public sealed class RequisitionsPackedCrate
{
    public int Weight;
    public List<EntProtoId> Items { get; } = new();
}

public readonly record struct RequisitionsPackedLoose(EntProtoId Prototype, int Weight);
