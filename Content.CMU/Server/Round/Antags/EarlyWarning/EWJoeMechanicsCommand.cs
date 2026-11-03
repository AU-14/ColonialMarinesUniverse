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

    public override string Description => "Enables or disables the Early Warning Joe antagonist and its mechanics.";

    public override string Help
        => "ewjoemechanics [true/false]\n"
        + " Off: the Early Warning Joe is not rolled or made, and any Joe already in the round is suspended: its actions"
        + " are greyed out, its directives stop advancing, and it neither respawns nor turns bullets. Turning it back on"
        + " restores all of that. No argument toggles.";

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
            $"{shell.Player} {(value ? "enabled" : "disabled")} the Early Warning Joe. {(value ? "It can be rolled again and any suspended Joe is active." : "It will not be rolled, and any Joe in the round is suspended.")}");
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
