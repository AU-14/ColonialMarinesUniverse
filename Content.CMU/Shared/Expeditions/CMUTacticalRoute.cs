using Robust.Shared.Utility;

namespace Content.Shared.CMU14.Expeditions;

/// <summary>Local A* with nonnegative exposure costs. Returns no route when blocked or over budget.</summary>
public static class CMUTacticalRoute
{
    /// <summary>
    /// Reusable integer search state. Lease separately for nested searches; each Find
    /// clears the previous costs and parents, including after a failed bounded search.
    /// </summary>
    public sealed class Workspace
    {
        internal readonly Dictionary<int, float> Costs = new();
        internal readonly Dictionary<int, int> Parents = new();
    }

    private static readonly IComparer<(int Cell, float Cost, float Priority)> PriorityComparer =
        Comparer<(int Cell, float Cost, float Priority)>.Create((a, b) => b.Priority.CompareTo(a.Priority));

    public static List<int>? Find(int size, int start, int end, Func<int, bool> walkable,
        Func<int, float> danger, Func<int, int, bool> passage, out int expanded, int budget = 256,
        Workspace? workspace = null)
    {
        // Keep path cost separate from A* priority; Robust's queue serves the maximum first.
        var open = new PriorityQueue<(int Cell, float Cost, float Priority)>(PriorityComparer);
        workspace ??= new Workspace();
        var costs = workspace.Costs;
        var parents = workspace.Parents;
        costs.Clear();
        parents.Clear();
        costs[start] = 0;
        open.Add((start, 0, Distance(start, end, size)));
        expanded = 0;
        while (open.Count > 0 && expanded < budget)
        {
            var current = open.Take();
            if (current.Cost > costs[current.Cell])
                continue;
            expanded++;
            if (current.Cell == end)
            {
                var route = new List<int> { end };
                while (parents.TryGetValue(route[^1], out var parent))
                    route.Add(parent);
                route.Reverse();
                return route;
            }
            for (var direction = 0; direction < 4; direction++)
            {
                var next = direction switch
                {
                    0 => current.Cell - 1,
                    1 => current.Cell + 1,
                    2 => current.Cell - size,
                    _ => current.Cell + size,
                };
                if (next < 0 || next >= size * size || Distance(current.Cell, next, size) != 1)
                    continue;
                // Exposure is nonnegative. A visited cell with an equal or cheaper
                // cost cannot improve even before the expensive terrain/body casts.
                if (costs.TryGetValue(next, out var old) && old <= current.Cost + 1)
                    continue;
                if (!walkable(next) || !passage(current.Cell, next))
                    continue;
                var penalty = danger(next);
                if (!float.IsFinite(penalty) || penalty < 0)
                    continue;
                var cost = current.Cost + 1 + penalty;
                if (costs.TryGetValue(next, out old) && old <= cost)
                    continue;
                costs[next] = cost;
                parents[next] = current.Cell;
                open.Add((next, cost, cost + Distance(next, end, size)));
            }
        }
        return null;
    }

    private static int Distance(int a, int b, int size) => Math.Abs(a % size - b % size) + Math.Abs(a / size - b / size);
}
