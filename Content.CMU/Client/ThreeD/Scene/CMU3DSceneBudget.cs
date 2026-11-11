namespace Content.Client.CMU14.ThreeD.Scene;

/// <summary>Give sparse floors their demand before sharing the remaining geometry capacity.</summary>
public static class CMU3DSceneBudget
{
    public static int[] Allocate(int capacity, IReadOnlyList<int> demand)
    {
        var budgets = new int[demand.Count];
        var pending = new bool[demand.Count];
        Array.Fill(pending, true);
        var remaining = capacity;
        var weight = demand.Count + 1;
        while (weight > 0)
        {
            var satisfied = false;
            for (var i = 0; i < demand.Count; i++)
            {
                var share = i == 0 ? 2 : 1;
                if (!pending[i] || demand[i] > (long) remaining * share / weight)
                    continue;
                budgets[i] = Math.Max(0, demand[i]);
                remaining -= budgets[i];
                weight -= share;
                pending[i] = false;
                satisfied = true;
            }
            if (!satisfied) break;
        }
        // If all known demand fits, retain headroom for entities still arriving via PVS.
        if (weight == 0)
        {
            Array.Fill(pending, true);
            weight = demand.Count + 1;
        }
        for (var i = 0; i < demand.Count; i++)
        {
            if (!pending[i]) continue;
            var share = i == 0 ? 2 : 1;
            var extra = (int) ((long) remaining * share / weight);
            budgets[i] += extra;
            remaining -= extra;
            weight -= share;
        }
        return budgets;
    }
}
