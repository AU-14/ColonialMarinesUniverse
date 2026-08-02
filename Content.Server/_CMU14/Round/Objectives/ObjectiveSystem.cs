using System.Linq;
using Content.Shared._CMU14.Round.Objectives.Component;

namespace Content.Server._CMU14.Round.Objectives;

public abstract partial class ObjectiveSystem : EntitySystem
{
    /// <summary>Each Objective type sets this to its own sawmill ("obj-fetch") in Initialize().</summary>
    protected ISawmill _logs = default!;

    protected static bool ShouldCompleteForFaction(
        CMUObjectiveComponent auComp,
        string faction,
        int currentAmount,
        int requiredAmount)
        => currentAmount >= requiredAmount && (auComp.FactionNeutral || faction == auComp.Faction.ToLowerInvariant());

    protected static string? GetCreditFaction(
        CMUObjectiveComponent auComp,
        IEnumerable<string> entityFactions,
        string targetFaction,
        string? presetId,
        ObjectiveControlSystem ctrl)
    {
        if (auComp.FactionNeutral)
        {
            string? result = null;
            foreach (var f in entityFactions)
            {
                var opposite = ctrl.GetOppositeFaction(f, presetId);
                if (!string.IsNullOrEmpty(opposite))
                    result = opposite;
            }
            return result;
        }

        if (!entityFactions.Contains(targetFaction.ToLowerInvariant()))
            return null;

        return auComp.Faction.ToLowerInvariant();
    }
}
