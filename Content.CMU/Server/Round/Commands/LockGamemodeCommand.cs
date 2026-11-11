using System.Linq;
using Content.Server.Administration;
using Content.Server.GameTicking.Presets;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Round.Commands;

/// <summary>
///     Makes the gamemode vote offer only one gamemode for a number of rounds.
/// </summary>
[AdminCommand(AdminFlags.Admin)]
public sealed class LockGamemodeCommand : IConsoleCommand
{
    [Dependency] private IEntitySystemManager _systems = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    public string Command => "lockgamemode";
    public string Description => "Makes the gamemode vote only offer one gamemode, for a number of rounds or until cleared.";
    public string Help => "Usage: lockgamemode <presetId> [rounds] | lockgamemode clear | lockgamemode";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var votes = _systems.GetEntitySystem<CMUPresetVoteSystem>();
        switch (args.Length)
        {
            case 0:
                if (votes.LockedPreset is not { } locked)
                    shell.WriteLine("The gamemode vote isn't locked.");
                else if (votes.LockedRounds is { } left)
                    shell.WriteLine($"The gamemode vote only offers {locked} for {left} more round(s).");
                else
                    shell.WriteLine($"The gamemode vote only offers {locked} until the lock is cleared.");
                return;
            case 1 when args[0].Equals("clear", StringComparison.OrdinalIgnoreCase):
                votes.ClearLock();
                shell.WriteLine("The gamemode vote offers every gamemode again.");
                return;
            case 1:
            case 2:
                break;
            default:
                shell.WriteError(Help);
                return;
        }

        if (!_prototypes.TryIndex<GamePresetPrototype>(args[0], out var preset))
        {
            shell.WriteError($"No gamemode preset called {args[0]}.");
            return;
        }

        if (args.Length == 1)
        {
            votes.LockPreset(preset.ID);
            shell.WriteLine($"The gamemode vote will only offer {preset.ID} until you run lockgamemode clear.");
            return;
        }

        if (!int.TryParse(args[1], out var rounds) || rounds < 1)
        {
            shell.WriteError("Rounds must be a whole number of at least 1.");
            return;
        }

        votes.LockPreset(preset.ID, rounds);
        shell.WriteLine($"The gamemode vote will only offer {preset.ID} for the next {rounds} round(s).");
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHintOptions(
                _prototypes.EnumeratePrototypes<GamePresetPrototype>()
                    .Select(p => new CompletionOption(p.ID, Loc.GetString(p.ModeTitle)))
                    .Append(new CompletionOption("clear", "Remove the lock")),
                "<presetId>"),
            2 => CompletionResult.FromHint("[rounds, or leave out to lock until cleared]"),
            _ => CompletionResult.Empty,
        };
    }
}
