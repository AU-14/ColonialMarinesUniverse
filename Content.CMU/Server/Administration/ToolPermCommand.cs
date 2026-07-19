// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using System.Linq;
using Content.Server.Administration;
using Content.Shared._AU14.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._AU14.Administration;

/// <summary>
/// Console fallback for the Tool Permissions window: grant/revoke per-tool editor access by ckey.
/// Host-only, unlike jobwhitelistadd which lower admin ranks could reach.
/// </summary>
[AdminCommand(AdminFlags.Host)]
public sealed class ToolPermCommand : IConsoleCommand
{
    public string Command => "toolperm";
    public string Description => "Grant, revoke, or list per-tool editor permissions by ckey.";
    public string Help => "Usage: toolperm add <ckey> <tool> | toolperm remove <ckey> <tool> | toolperm list\n" +
                          "Tools: " + string.Join(", ", AU14ToolPermissions.AllTools.Select(t => t.Id));

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var entMan = IoCManager.Resolve<IEntityManager>();
        var perms = entMan.System<AU14ToolPermissionSystem>();

        if (args.Length == 1 && args[0] == "list")
        {
            foreach (var (ckey, tools) in perms.AllGrants.OrderBy(kv => kv.Key))
                shell.WriteLine($"{ckey}: {string.Join(", ", tools.OrderBy(t => t))}");
            if (perms.AllGrants.Count == 0)
                shell.WriteLine("No tool grants.");
            return;
        }

        if (args.Length != 3 || (args[0] != "add" && args[0] != "remove"))
        {
            shell.WriteError(Help);
            return;
        }

        var tool = args[2];
        if (!AU14ToolPermissions.IsValidTool(tool))
        {
            shell.WriteError($"Unknown tool '{tool}'. Tools: {string.Join(", ", AU14ToolPermissions.AllTools.Select(t => t.Id))}");
            return;
        }

        var grant = args[0] == "add";
        if (perms.SetGrant(args[1], tool, grant))
        {
            perms.Save();
            shell.WriteLine($"{(grant ? "Granted" : "Revoked")} '{tool}' {(grant ? "to" : "from")} {args[1]}.");
        }
        else
        {
            shell.WriteLine("Nothing changed.");
        }
    }
}
