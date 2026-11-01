using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server.CMU14.Yautja;

// cmss13 "Report: Yautja Overseer" is R_MOD, same here
[AdminCommand(AdminFlags.Moderator)]
public sealed class YautjaOverseerCommand : LocalizedEntityCommands
{
    [Dependency] private YautjaOverseerSystem _overseer = default!;

    public override string Command => "yautjaannounce";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 2 || !YautjaOverseerSystem.TryParseNetwork(args[0], out var network))
        {
            shell.WriteError(Loc.GetString("cmd-yautjaannounce-usage"));
            return;
        }

        var text = string.Join(" ", args, 1, args.Length - 1).Trim();
        if (text.Length == 0)
        {
            shell.WriteError(Loc.GetString("cmd-yautjaannounce-usage"));
            return;
        }

        var author = shell.Player?.Name ?? "Server";
        var count = _overseer.SendOverseerMessage(text, network, author);
        shell.WriteLine(Loc.GetString("cmd-yautjaannounce-sent", ("count", count)));
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length == 1
            ? CompletionResult.FromHintOptions(["all", "hunt", "stranded"], Loc.GetString("cmd-yautjaannounce-arg-network"))
            : CompletionResult.FromHint(Loc.GetString("cmd-yautjaannounce-arg-message"));
    }
}
