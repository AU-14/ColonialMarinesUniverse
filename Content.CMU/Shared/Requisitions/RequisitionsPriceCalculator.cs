using System.Linq;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Requisitions;

/// <summary>
/// Infers stable per-entity prices from the prices and deterministic contents of legacy bundles.
/// </summary>
public static class RequisitionsPriceCalculator
{
    public static Dictionary<EntProtoId, int> Calculate(IEnumerable<RequisitionsPriceSource> sources)
    {
        var validSources = sources
            .Where(source => source.Cost > 0 && source.Manifest.Any(item => item.Value > 0))
            .ToList();
        var candidates = new Dictionary<EntProtoId, List<double>>();
        var anchors = new Dictionary<EntProtoId, List<double>>();

        // Only homogeneous bundles establish known unit prices. Estimates from mixed bundles must
        // not feed back into other bundles and repeatedly drive their remaining contents toward zero.
        foreach (var source in validSources)
        {
            var items = source.Manifest.Where(item => item.Value > 0).ToArray();
            if (items.Length != 1)
                continue;

            var item = items[0];
            AddCandidate(anchors, item.Key, (double) source.Cost / item.Value);
        }

        var fixedPrices = anchors.ToDictionary(pair => pair.Key, pair => Median(pair.Value));
        foreach (var source in validSources)
        {
            var remainingCost = (double) source.Cost;
            var objectCount = 0;
            var unpricedCount = 0;
            foreach (var (prototype, amount) in source.Manifest)
            {
                if (amount <= 0)
                    continue;

                objectCount += amount;
                if (fixedPrices.TryGetValue(prototype, out var price))
                    remainingCost -= price * amount;
                else
                    unpricedCount += amount;
            }

            if (unpricedCount == 0)
                continue;

            // A discounted bundle can cost less than its separately sold accessories. That does
            // not make the other items worthless: fall back to the bundle's equal-object share.
            var unitPrice = remainingCost > 0
                ? remainingCost / unpricedCount
                : (double) source.Cost / objectCount;
            foreach (var (prototype, amount) in source.Manifest)
            {
                if (amount > 0 && !fixedPrices.ContainsKey(prototype))
                    AddCandidate(candidates, prototype, unitPrice);
            }
        }

        var prices = new Dictionary<EntProtoId, double>(fixedPrices);
        foreach (var (prototype, itemCandidates) in candidates)
            prices[prototype] = Median(itemCandidates);

        return prices.ToDictionary(
            pair => pair.Key,
            pair => Math.Max(1, (int) Math.Round(pair.Value, MidpointRounding.AwayFromZero)));
    }

    private static void AddCandidate(
        Dictionary<EntProtoId, List<double>> candidates,
        EntProtoId prototype,
        double price)
    {
        if (!candidates.TryGetValue(prototype, out var values))
        {
            values = new List<double>();
            candidates[prototype] = values;
        }

        values.Add(price);
    }

    private static double Median(List<double> values)
    {
        values.Sort();
        var middle = values.Count / 2;
        return values.Count % 2 == 0
            ? (values[middle - 1] + values[middle]) / 2d
            : values[middle];
    }
}

public sealed record RequisitionsPriceSource(
    int Cost,
    IReadOnlyDictionary<EntProtoId, int> Manifest);
