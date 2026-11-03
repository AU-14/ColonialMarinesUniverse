using Content.Server.Administration;
using Content.Server.Chat.Managers;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Console;

namespace Content.Server.CMU14.Round.Antags.EarlyWarning;

[AdminCommand(AdminFlags.Round)]
public sealed partial class EWJoeMechanicsCommand : LocalizedEntityCommands
{
    [Dependency] private IChatManager _chatManager = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    public override string Command => "ewjoemechanics";

    public override string Description => "Enables or disables rolling the Early Warning Joe antagonist.";

    public override string Help
        => "ewjoemechanics [true/false]\n"
        + " Off: the Early Warning Joe game rules roll nobody, and an untaken Distress Signal ghost role is withdrawn."
        + " Joes already in the round are not affected. No argument toggles.";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length > 1)
        {
            shell.WriteError("Usage: ewjoemechanics [true/false]");
            return;
        }

        var value = !_cfg.GetCVar(CCVars.CMUEWJoeEnabled);
        if (args.Length == 1 && !bool.TryParse(args[0], out value))
        {
            shell.WriteError("Argument must be true or false.");
            return;
        }

        _cfg.SetCVar(CCVars.CMUEWJoeEnabled, value);
        _chatManager.SendAdminAnnouncement(
            $"{shell.Player} {(value ? "enabled" : "disabled")} the Early Warning Joe. {(value ? "It can be rolled again." : "It will not be rolled; Joes already in the round are unaffected.")}");
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
