using Robust.Shared.Utility;

namespace Content.Shared.CMU14.Expeditions;

[Flags]
public enum CMUTacticalFact : uint
{
    None = 0, Armed = 1, Safe = 2, SpareAmmo = 4, Medicine = 8, Wounded = 16,
    CoverAvailable = 32, Contact = 64, Casualty = 128, AtCasualty = 256,
    Dragging = 512, Rescued = 1024, Grenade = 2048, ThrowLane = 4096,
    PressureApplied = 8192, FlankAvailable = 16384, Flanked = 32768, Engaged = 65536,
}

public enum CMUTacticalAction : byte { TakeCover, Reload, Treat, ApproachCasualty, GrabCasualty, DragCasualty, ThrowGrenade, Flank, Attack }
public enum CMUTacticalGoal : byte { Fight, Rearm, Recover, Rescue, Flush, Flank }

public readonly record struct CMUTacticalOperator(CMUTacticalAction Action, CMUTacticalFact Requires,
    CMUTacticalFact Forbids, CMUTacticalFact Adds, CMUTacticalFact Removes, float Cost);

/// <summary>Bounded uniform-cost GOAP search. Executors must revalidate every precondition against the live world.</summary>
public static class CMUTacticalPlanner
{
    public static List<CMUTacticalAction>? Plan(CMUTacticalFact initial, CMUTacticalFact required,
        CMUTacticalFact forbidden, IReadOnlyList<CMUTacticalOperator> actions, out int expanded, int budget = 128)
    {
        // Robust's sandbox-safe queue is a max-heap, so reverse the cost comparison.
        var frontier = new PriorityQueue<(CMUTacticalFact State, float Cost)>(
            Comparer<(CMUTacticalFact State, float Cost)>.Create((a, b) => b.Cost.CompareTo(a.Cost)));
        var costs = new Dictionary<CMUTacticalFact, float> { [initial] = 0 };
        var previous = new Dictionary<CMUTacticalFact, (CMUTacticalFact State, CMUTacticalAction Action)>();
        frontier.Add((initial, 0));
        expanded = 0;
        while (frontier.Count > 0 && expanded < budget)
        {
            var node = frontier.Take();
            if (node.Cost > costs[node.State])
                continue;
            expanded++;
            if ((node.State & required) == required && (node.State & forbidden) == 0)
            {
                var result = new List<CMUTacticalAction>();
                var state = node.State;
                while (previous.TryGetValue(state, out var parent))
                {
                    result.Add(parent.Action);
                    state = parent.State;
                }
                result.Reverse();
                return result;
            }
            foreach (var action in actions)
            {
                if (!float.IsFinite(action.Cost) || action.Cost <= 0 ||
                    (node.State & action.Requires) != action.Requires || (node.State & action.Forbids) != 0)
                    continue;
                var next = (node.State | action.Adds) & ~action.Removes;
                var cost = node.Cost + action.Cost;
                if (costs.TryGetValue(next, out var old) && old <= cost)
                    continue;
                costs[next] = cost;
                previous[next] = (node.State, action.Action);
                frontier.Add((next, cost));
            }
        }
        return null;
    }
}
