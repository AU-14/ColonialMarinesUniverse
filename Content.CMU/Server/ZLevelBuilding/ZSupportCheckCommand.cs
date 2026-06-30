// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using Content.Server.Administration;
using Content.Shared._AU14.ZLevelBuilding;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Map.Components;

namespace Content.Server._AU14.ZLevelBuilding;

/// <summary>
/// Building overhaul (z-level), Phase 1 test aid: forces a support-graph recompute on the grid you are
/// standing on (or every grid with <c>au_zsupport all</c>) and reports how many structures are
/// supported vs unsupported. Pair it with ViewVariables on a structure's <see cref="StructuralSupportComponent.Supported"/>.
/// </summary>
[AdminCommand(AdminFlags.Debug)]
public sealed class ZSupportCheckCommand : IConsoleCommand
{
    public string Command => "au_zsupport";
    public string Description => "Recompute the z-level structural support graph and report supported/unsupported counts.";
    public string Help => "au_zsupport [all]";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var entMan = IoCManager.Resolve<IEntityManager>();
        var sysMan = IoCManager.Resolve<IEntitySystemManager>();
        var support = sysMan.GetEntitySystem<ZLevelSupportSystem>();

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
}
