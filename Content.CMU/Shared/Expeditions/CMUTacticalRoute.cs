namespace Content.Shared.CMU14.Expeditions;

/// <summary>Local A* with nonnegative exposure costs. Returns no route when blocked or over budget.</summary>
public static class CMUTacticalRoute
{
    public static List<int>? Find(int size, int start, int end, Func<int, bool> walkable,
        Func<int, float> danger, Func<int, int, bool> passage, out int expanded, int budget = 256)
    {
        var open = new PriorityQueue<(int Cell, float Cost), float>();
        var costs = new Dictionary<int, float> { [start] = 0 };
        var parents = new Dictionary<int, int>();
        open.Enqueue((start, 0), Distance(start, end, size));
        expanded = 0;
        while (open.TryDequeue(out var current, out _) && expanded < budget)
        {
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
            foreach (var next in new[] { current.Cell - 1, current.Cell + 1, current.Cell - size, current.Cell + size })
            {
                if (next < 0 || next >= size * size || Distance(current.Cell, next, size) != 1 ||
                    !walkable(next) || !passage(current.Cell, next))
                    continue;
                var penalty = danger(next);
                if (!float.IsFinite(penalty) || penalty < 0)
                    continue;
                var cost = current.Cost + 1 + penalty;
                if (costs.TryGetValue(next, out var old) && old <= cost)
                    continue;
                costs[next] = cost;
                parents[next] = current.Cell;
                open.Enqueue((next, cost), cost + Distance(next, end, size));
            }
        }
        return null;
    }

    private static int Distance(int a, int b, int size) => Math.Abs(a % size - b % size) + Math.Abs(a / size - b / size);
}
