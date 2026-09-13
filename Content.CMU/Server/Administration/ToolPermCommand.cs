// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using System.Linq;
using Content.Server.Administration;
using Content.Shared.CMU14.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server.CMU14.Administration;

/// <summary>
/// Console fallback for the Tool Permissions window: grant/revoke per-tool editor access by ckey.
/// Host-only, unlike jobwhitelistadd which lower admin ranks could reach.
/// </summary>
[AdminCommand(AdminFlags.Host)]
public sealed class ToolPermCommand : IConsoleCommand
{
    public string Command => "toolperm";
    public string Description => Loc.GetString("cmu-cmd-toolperm-desc");
    public string Help => Loc.GetString("cmu-cmd-toolperm-help",
        ("tools", string.Join(", ", AU14ToolPermissions.AllTools.Select(t => t.Id))));

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var entMan = IoCManager.Resolve<IEntityManager>();
        var perms = entMan.System<AU14ToolPermissionSystem>();

        if (args.Length == 1 && args[0] == "list")
        {
            foreach (var (ckey, tools) in perms.AllGrants.OrderBy(kv => kv.Key))
                shell.WriteLine($"{ckey}: {string.Join(", ", tools.OrderBy(t => t))}");
            if (perms.AllGrants.Count == 0)
                shell.WriteLine(Loc.GetString("cmu-cmd-toolperm-no-grants"));
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
            shell.WriteError(Loc.GetString("cmu-cmd-toolperm-unknown-tool",
                ("tool", tool),
                ("tools", string.Join(", ", AU14ToolPermissions.AllTools.Select(t => t.Id)))));
            return;
        }

        var grant = args[0] == "add";
        if (perms.SetGrant(args[1], tool, grant))
        {
            perms.Save();
            shell.WriteLine(Loc.GetString(grant ? "cmu-cmd-toolperm-granted" : "cmu-cmd-toolperm-revoked",
                ("tool", tool),
                ("ckey", args[1])));
        }
        else
        {
            shell.WriteLine(Loc.GetString("cmu-cmd-toolperm-no-change"));
        }
    }
}
