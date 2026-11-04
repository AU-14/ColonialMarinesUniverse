using Content.Server.Administration;
using Content.Server.Chat.Managers;
using Content.Shared.CCVar;
using Content.Shared.Administration;
using Robust.Shared.Configuration;
using Robust.Shared.Console;

namespace Content.Server.CMU14.Util.Admin.Console;

[AdminCommand(AdminFlags.Round)]
public sealed partial class EndRoundHoldCommand : LocalizedEntityCommands
{
    [Dependency] private IChatManager _chatManager = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    public override string Command => "endroundhold";

    public override string Description => "Holds or releases admin control of the round end.";

    public override string Help
        => "endroundhold [true/false]\n"
        + " While on, anything that tries to end the round (threat win rules, shuttle arrival) is muted: no end screen, no ghost reveal, no OOC - the round keeps running and admins get notified each time an end was suppressed.\n"
        + " Use for events: endroundhold, spawnthreat with nowin=true to stack threats, play it out, then endround to finish (endround releases the hold automatically).\n"
        + " No argument toggles. This is separate from rmcdelayroundend, which holds the restart AFTER the end screen.";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length > 1)
        {
            shell.WriteError("Usage: endroundhold [true/false]");
            return;
        }

        var value = !_cfg.GetCVar(CCVars.HoldRoundEnd);
        if (args.Length == 1 && !bool.TryParse(args[0], out value))
        {
            shell.WriteError("Argument must be true or false.");
            return;
        }

        _cfg.SetCVar(CCVars.HoldRoundEnd, value);
        _chatManager.SendAdminAnnouncement(
            $"{shell.Player} set the round-end hold to {value}. {(value ? "Round-ending attempts are now muted; end the round with endround when you are ready." : "Round-ending attempts are no longer muted.")}");
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHintOptions(CompletionHelper.Booleans, "<true/false (toggles if omitted)>"),
            _ => CompletionResult.Empty
        };
    }
}
