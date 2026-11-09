using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server.CMU14.Lobby;

[AdminCommand(AdminFlags.Admin)]
public sealed partial class CMUEventSlotsCommand : LocalizedEntityCommands
{
    [Dependency] private CMUEventSlotsSystem _slots = default!;
    [Dependency] private CMUEventJobTargetsSystem _targets = default!;

    public override string Command => "eventslots";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args is ["clear"])
        {
            _slots.ClearPending();
            shell.WriteLine(Loc.GetString("cmu-event-slots-cleared"));
            return;
        }

        if (args is ["list", var side])
        {
            foreach (var line in _slots.Describe(side))
                shell.WriteLine(line);
            return;
        }

        if (args.Length != 4 || args[0] is not ("add" or "set") || !int.TryParse(args[3], out var amount))
        {
            shell.WriteError(Help);
            return;
        }

        if (_slots.TryChange(args[1], args[2], amount, args[0] == "set", out var message))
            shell.WriteLine(message);
        else
            shell.WriteError(message);
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args) => args.Length switch
    {
        1 => CompletionResult.FromOptions(["add", "set", "list", "clear"]),
        2 when args[0] is "add" or "set" or "list" => CompletionResult.FromOptions(_targets.SideOptions()),
        3 when args[0] is "add" or "set" => CompletionResult.FromOptions(_targets.RoleOptions(args[1])),
        4 when args[0] == "set" => CompletionResult.FromOptions(["-1", "0", "1", "2", "5", "10", "20"]),
        4 when args[0] == "add" => CompletionResult.FromOptions(["-5", "-1", "1", "2", "5", "10", "20"]),
        _ => CompletionResult.Empty,
    };
}
