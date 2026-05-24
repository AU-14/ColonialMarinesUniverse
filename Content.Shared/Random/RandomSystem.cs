using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Shared.Random;

public sealed class RandomSystem : EntitySystem
{
    public IBudgetEntry? GetBudgetEntry(ref float budget, ref float probSum, IList<IBudgetEntry> entries, System.Random random)
    {
        DebugTools.Assert(budget > 0f);

        if (entries.Count == 0)
            return null;

        // - Pick an entry
        // - Remove the cost from budget
        // - If our remaining budget is under maxCost then start pruning unavailable entries.
        Shuffle(entries, random);
        var budgetEntry = (IBudgetEntry) GetProbEntry(entries, probSum, random);

        budget -= budgetEntry.Cost;

        // Prune invalid entries.
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];

            if (entry.Cost < budget)
                continue;

            entries.RemoveSwap(i);
            i--;
            probSum -= entry.Prob;
        }

        return budgetEntry;
    }

    /// <summary>
    /// Gets a random entry based on each entry having a different probability.
    /// </summary>
    public IProbEntry GetProbEntry(IEnumerable<IProbEntry> entries, float probSum, System.Random random)
    {
        var value = (float) random.NextDouble() * probSum;

        foreach (var entry in entries)
        {
            value -= entry.Prob;

            if (value < 0f)
            {
                return entry;
            }
        }

        throw new InvalidOperationException();
    }

    private static void Shuffle<T>(IList<T> list, System.Random random)
    {
        var n = list.Count;
        while (n > 1)
        {
            n -= 1;
            var k = random.Next(n + 1);
            (list[k], list[n]) = (list[n], list[k]);
        }
    }
}
