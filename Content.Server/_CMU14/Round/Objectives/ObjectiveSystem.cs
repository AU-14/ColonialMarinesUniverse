using System.Linq;
using Content.Shared._CMU14.Round.Objectives.Component;
using Content.Shared._CMU14.ZLevels.Core.EntitySystems;
using Robust.Shared.Map;

namespace Content.Server._CMU14.Round.Objectives;

public abstract partial class ObjectiveSystem : EntitySystem
{
    [Dependency] private CMUSharedZLevelsSystem _zLevels = default!;
    [Dependency] private SharedMapSystem _mapSystem = default!;
    [Dependency] private SharedTransformSystem _objTransform = default!;

    /// <summary>Each Objective type sets this to its own sawmill ("obj-fetch") in Initialize().</summary>
    protected ISawmill _logs = default!;

    protected HashSet<MapId> GetZNetworkMapIds(MapId primaryMap)
    {
        var maps = new HashSet<MapId> { primaryMap };
        var mapUid = _mapSystem.GetMap(primaryMap);

        if (_zLevels.TryGetZNetwork(mapUid, out var network) &&
            _zLevels.TryGetDepthBounds(network.Value, out var minDepth, out var maxDepth))
        {
            for (var depth = minDepth; depth <= maxDepth; depth++)
            {
                if (_zLevels.TryGetMapAtDepth(network.Value, depth, out var connectedMapUid))
                    maps.Add(_objTransform.GetMapId(connectedMapUid));
            }
        }

        return maps;
    }

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
