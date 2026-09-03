using System.Linq;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Requisitions;

/// <summary>
/// Infers stable per-entity prices from the prices and deterministic contents of legacy bundles.
/// </summary>
public static class RequisitionsPriceCalculator
{
    private const int RefinementPasses = 8;

    public static Dictionary<EntProtoId, int> Calculate(IEnumerable<RequisitionsPriceSource> sources)
    {
        var validSources = sources
            .Where(source => source.Cost > 0 && source.Manifest.Any(item => item.Value > 0))
            .ToList();
        var candidates = new Dictionary<EntProtoId, List<double>>();
        var anchors = new Dictionary<EntProtoId, List<double>>();

        // Equal-object shares are the neutral prior. Unlike physical weight, they correctly account
        // for repeated stacks, magazines, or tools without pretending that mass determines value.
        foreach (var source in validSources)
        {
            var objectCount = source.Manifest.Values.Where(amount => amount > 0).Sum();
            var equalShare = (double) source.Cost / objectCount;
            foreach (var (prototype, amount) in source.Manifest)
            {
                if (amount <= 0)
                    continue;
                AddCandidate(candidates, prototype, equalShare);
            }

            if (source.Manifest.Count != 1)
                continue;

            var item = source.Manifest.Single();
            if (item.Value > 0)
                AddCandidate(anchors, item.Key, (double) source.Cost / item.Value);
        }

        var prices = candidates.ToDictionary(pair => pair.Key, pair => Median(pair.Value));
        var fixedPrices = anchors.ToDictionary(pair => pair.Key, pair => Median(pair.Value));
        foreach (var (prototype, price) in fixedPrices)
            prices[prototype] = price;

        // Pure legacy bundles act as price anchors. Repeated passes propagate those known values
        // through mixed bundles, assigning the remaining value to their otherwise-unknown contents.
        for (var pass = 0; pass < RefinementPasses; pass++)
        {
            candidates.Clear();
            foreach (var source in validSources)
            {
                var sourceWeight = source.Manifest.Sum(item =>
                    item.Value > 0 && prices.TryGetValue(item.Key, out var price)
                        ? price * item.Value
                        : 0);
                if (sourceWeight <= 0)
                    continue;

                foreach (var (prototype, amount) in source.Manifest)
                {
                    if (amount <= 0 || !prices.TryGetValue(prototype, out var prior))
                        continue;

                    var allocatedUnitPrice = source.Cost * prior / sourceWeight;
                    AddCandidate(candidates, prototype, allocatedUnitPrice);
                }
            }

            foreach (var (prototype, itemCandidates) in candidates)
            {
                if (!fixedPrices.ContainsKey(prototype))
                    prices[prototype] = Median(itemCandidates);
            }
        }

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
