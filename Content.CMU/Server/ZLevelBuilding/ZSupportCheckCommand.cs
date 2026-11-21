// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using Content.Server.Administration;
using Content.Shared.CMU14.ZLevelBuilding;
using Content.Shared.CMU14.ZLevels.Core.Components; // CMU14: dump mode
using Content.Shared.Station.Components; // CMU14: dump mode
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Map.Components;

namespace Content.Server.CMU14.ZLevelBuilding;

/// <summary>
/// Building overhaul (z-level), Phase 1 test aid: forces a support-graph recompute on the grid you are
/// standing on (or every grid with <c>au_zsupport all</c>) and reports how many structures are
/// supported vs unsupported. Pair it with ViewVariables on a structure's <see cref="StructuralSupportComponent.Supported"/>.
/// </summary>
[AdminCommand(AdminFlags.Debug)]
public sealed class ZSupportCheckCommand : IConsoleCommand
{
    public string Command => "au_zsupport";
    public string Description => "Recompute the z-level structural support graph and report supported/unsupported counts, or dump collapse state without recomputing.";
    public string Help => "au_zsupport [all|dump]";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var entMan = IoCManager.Resolve<IEntityManager>();
        var sysMan = IoCManager.Resolve<IEntitySystemManager>();
        var support = sysMan.GetEntitySystem<ZLevelSupportSystem>();

        // CMU14: read-only diagnostics (GraphAudit TEST-01). Never recomputes, so it cannot schedule collapses.
        if (args.Length >= 1 && args[0].Equals("dump", System.StringComparison.OrdinalIgnoreCase))
        {
            Dump(shell, entMan, support);
            return;
        }

        var all = args.Length >= 1 && args[0].Equals("all", System.StringComparison.OrdinalIgnoreCase);

        if (all)
        {
            var query = entMan.AllEntityQueryEnumerator<MapGridComponent>();
            var grids = 0;
            while (query.MoveNext(out var uid, out var grid))
            {
                support.RecomputeGrid((uid, grid));
                grids++;
            }

            Report(shell, entMan, $"Recomputed {grids} grid(s).");
            return;
        }

        if (shell.Player?.AttachedEntity is not { } attached)
        {
            shell.WriteError("Run this as an in-game player, or use 'au_zsupport all'.");
            return;
        }

        var gridUid = entMan.GetComponent<TransformComponent>(attached).GridUid;
        if (gridUid == null || !entMan.TryGetComponent<MapGridComponent>(gridUid, out var gridComp))
        {
            shell.WriteError("You are not standing on a grid. Try 'au_zsupport all'.");
            return;
        }

        support.RecomputeGrid((gridUid.Value, gridComp));
        Report(shell, entMan, $"Recomputed your grid {gridUid}.");
    }

    private static void Report(IConsoleShell shell, IEntityManager entMan, string prefix)
    {
        var supported = 0;
        var unsupported = 0;
        var query = entMan.AllEntityQueryEnumerator<StructuralSupportComponent>();
        while (query.MoveNext(out var comp))
        {
            if (comp.Supported)
                supported++;
            else
                unsupported++;
        }

        shell.WriteLine($"{prefix} Supports: {supported} supported, {unsupported} unsupported.");
    }

    // CMU14 Begin: per Z map, the state that decides whether a level can collapse. Reads only.
    private static void Dump(IConsoleShell shell, IEntityManager entMan, ZLevelSupportSystem support)
    {
        var levels = 0;
        var maps = entMan.AllEntityQueryEnumerator<CMUZLevelMapComponent>();
        while (maps.MoveNext(out var mapUid, out var zMap))
        {
            levels++;
            var becomesStation = entMan.HasComponent<BecomesStationComponent>(mapUid);
            var pending = 0;
            var gridCount = 0;
            var grids = entMan.AllEntityQueryEnumerator<MapGridComponent, TransformComponent>();
            while (grids.MoveNext(out var gridUid, out _, out var gridXform))
            {
                if (gridXform.MapUid != mapUid)
                    continue;

                gridCount++;
                becomesStation |= entMan.HasComponent<BecomesStationComponent>(gridUid);
                pending += support.PendingCollapseCount(gridUid);
            }

            var authored = entMan.HasComponent<ZAuthoredLevelComponent>(mapUid);
            var line = $"map={entMan.ToPrettyString(mapUid)} depth={zMap.Depth} grids={gridCount} " +
                       $"becomesStation={becomesStation} authoredMarker={authored} supportPendingCollapse={pending}";

            if (entMan.TryGetComponent<ZGeneratedStoneComponent>(mapUid, out var stone))
            {
                var caveQueueRemaining = System.Math.Max(0, stone.CollapseQueue.Count - stone.CollapseQueueIndex);
                line += $" stone: localized={stone.LocalizedToAuthoredLevel} stoneGrid={stone.StoneGrid} " +
                        $"chunks={stone.GeneratedChunks.Count} tiles={stone.GeneratedTiles.Count} " +
                        $"dirtyTiles={stone.DirtyTiles.Count} pendingCollapse={stone.PendingCollapse.Count} " +
                        $"caveQueueRemaining={caveQueueRemaining}";
            }
            else
            {
                line += " stone: none";
            }

            shell.WriteLine(line);
        }

        shell.WriteLine($"Dumped {levels} z-level map(s). Nothing was recomputed.");
    }
    // CMU14 End
}
