namespace Content.Server.CMU14.ForceOnForce;

/// <summary>Joint side/job matching. Reassignments are internal to the roll, before any role is awarded.</summary>
public static class ForceOnForceAssignment
{
    public readonly record struct Slot(int Side, int Capacity);
    public readonly record struct Candidate(int Player, int Slot, int Cost);

    public static Dictionary<int, int> Assign(int players, IReadOnlyList<Slot> slots, IReadOnlyList<Candidate> candidates)
    {
        // Requiring a complete flow with equal side capacities enforces both a floor
        // and a ceiling. Merely capping the popular side can strand flexible players.
        var lower = 0;
        var upper = players / 2;
        var best = new Dictionary<int, int>();
        while (lower < upper)
        {
            var middle = (lower + upper + 1) / 2;
            var result = Match(players, slots, candidates, [middle, middle]);
            if (result.Count == middle * 2)
            {
                lower = middle;
                best = result;
            }
            else upper = middle - 1;
        }
        if (lower > 0 && best.Count != lower * 2)
            best = Match(players, slots, candidates, [lower, lower]);
        if (players == lower * 2) return best;

        var first = Match(players, slots, candidates, [lower + 1, lower]);
        var second = Match(players, slots, candidates, [lower, lower + 1]);
        if (first.Count != lower * 2 + 1) first = best;
        if (second.Count != lower * 2 + 1) second = best;
        if (first.Count != second.Count) return first.Count > second.Count ? first : second;
        var firstCost = 0;
        var secondCost = 0;
        foreach (var candidate in candidates)
        {
            if (first.TryGetValue(candidate.Player, out var a) && a == candidate.Slot) firstCost += candidate.Cost;
            if (second.TryGetValue(candidate.Player, out var b) && b == candidate.Slot) secondCost += candidate.Cost;
        }
        return firstCost <= secondCost ? first : second;
    }

    private sealed class Edge(int to, int reverse, int capacity, int cost)
    {
        public readonly int To = to;
        public readonly int Reverse = reverse;
        public int Capacity = capacity;
        public readonly int Cost = cost;
    }

    private static Dictionary<int, int> Match(int players, IReadOnlyList<Slot> slots,
        IReadOnlyList<Candidate> candidates, int[] caps)
    {
        var sideStart = 1 + players + slots.Count;
        var sink = sideStart + 2;
        var graph = new List<Edge>[sink + 1];
        for (var i = 0; i < graph.Length; i++) graph[i] = new();
        void Add(int from, int to, int capacity, int cost)
        {
            graph[from].Add(new Edge(to, graph[to].Count, capacity, cost));
            graph[to].Add(new Edge(from, graph[from].Count - 1, 0, -cost));
        }
        for (var player = 0; player < players; player++) Add(0, player + 1, 1, 0);
        foreach (var candidate in candidates)
            Add(candidate.Player + 1, 1 + players + candidate.Slot, 1, candidate.Cost);
        for (var slot = 0; slot < slots.Count; slot++)
            Add(1 + players + slot, sideStart + slots[slot].Side, Math.Min(players, slots[slot].Capacity), 0);
        for (var side = 0; side < 2; side++) Add(sideStart + side, sink, caps[side], 0);

        // Successive shortest augmenting paths also move earlier tentative choices when this
        // makes room for a player with fewer alternatives.
        while (true)
        {
            var distance = new int[graph.Length];
            Array.Fill(distance, int.MaxValue);
            var previous = new (int Node, int Edge)[graph.Length];
            var queued = new bool[graph.Length];
            var queue = new Queue<int>();
            distance[0] = 0;
            queue.Enqueue(0);
            queued[0] = true;
            while (queue.TryDequeue(out var node))
            {
                queued[node] = false;
                for (var i = 0; i < graph[node].Count; i++)
                {
                    var edge = graph[node][i];
                    if (edge.Capacity <= 0 || distance[edge.To] <= distance[node] + edge.Cost) continue;
                    distance[edge.To] = distance[node] + edge.Cost;
                    previous[edge.To] = (node, i);
                    if (queued[edge.To]) continue;
                    queue.Enqueue(edge.To);
                    queued[edge.To] = true;
                }
            }
            if (distance[sink] == int.MaxValue) break;
            for (var node = sink; node != 0;)
            {
                var (from, index) = previous[node];
                var edge = graph[from][index];
                edge.Capacity--;
                graph[node][edge.Reverse].Capacity++;
                node = from;
            }
        }
        var result = new Dictionary<int, int>();
        for (var player = 0; player < players; player++)
        {
            foreach (var edge in graph[player + 1])
            {
                if (edge.To >= players + 1 && edge.To < sideStart && edge.Capacity == 0)
                    result[player] = edge.To - players - 1;
            }
        }
        return result;
    }
}
