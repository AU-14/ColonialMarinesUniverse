using System.Linq;
using Content.Server.Administration;
using Content.Server.GameTicking.Presets;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Round.Commands;

/// <summary>
///     Makes the gamemode vote offer only the given gamemodes for a number of rounds.
/// </summary>
[AdminCommand(AdminFlags.Admin)]
public sealed class LockGamemodeCommand : IConsoleCommand
{
    [Dependency] private IEntitySystemManager _systems = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    public string Command => "lockgamemode";
    public string Description => "Makes the gamemode vote only offer the given gamemodes, for a number of rounds or until cleared.";
    public string Help => "Usage: lockgamemode <presetId> [presetId...] [rounds] | lockgamemode clear | lockgamemode";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var votes = _systems.GetEntitySystem<CMUPresetVoteSystem>();
        switch (args.Length)
        {
            case 0:
                if (votes.LockedPresets.Count == 0)
                    shell.WriteLine("The gamemode vote isn't locked.");
                else if (votes.LockedRounds is { } left)
                    shell.WriteLine($"The gamemode vote only offers {Describe(votes.LockedPresets)} for {left} more round(s).");
                else
                    shell.WriteLine($"The gamemode vote only offers {Describe(votes.LockedPresets)} until the lock is cleared.");
                return;
            case 1 when args[0].Equals("clear", StringComparison.OrdinalIgnoreCase):
                votes.ClearLock();
                shell.WriteLine("The gamemode vote offers every gamemode again.");
                return;
        }

        // Only the final argument may be a round count; preset IDs never parse as numbers.
        var rounds = 0;
        var presets = new List<ProtoId<GamePresetPrototype>>();
        for (var i = 0; i < args.Length; i++)
        {
            if (i == args.Length - 1 && int.TryParse(args[i], out var count))
            {
                if (count < 1)
                {
                    shell.WriteError("Rounds must be a whole number of at least 1.");
                    return;
                }

                rounds = count;
                break;
            }

            if (!_prototypes.TryIndex<GamePresetPrototype>(args[i], out var preset))
            {
                shell.WriteError($"No gamemode preset called {args[i]}.");
                return;
            }

            presets.Add(preset.ID);
        }

        if (presets.Count == 0)
        {
            shell.WriteError("Give at least one gamemode preset.");
            return;
        }

        votes.LockPresets(presets, rounds > 0 ? rounds : null);
        if (rounds > 0)
            shell.WriteLine($"The gamemode vote will only offer {Describe(votes.LockedPresets)} for the next {rounds} round(s).");
        else
            shell.WriteLine($"The gamemode vote will only offer {Describe(votes.LockedPresets)} until you run lockgamemode clear.");
    }

    private static string Describe(IEnumerable<ProtoId<GamePresetPrototype>> presets)
        => string.Join(", ", presets);

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
            return CompletionResult.FromHintOptions(
                _prototypes.EnumeratePrototypes<GamePresetPrototype>()
                    .Select(p => new CompletionOption(p.ID, Loc.GetString(p.ModeTitle)))
                    .Append(new CompletionOption("clear", "Remove the lock")),
                "<presetId>");

        return CompletionResult.FromHintOptions(
            _prototypes.EnumeratePrototypes<GamePresetPrototype>()
                .Select(p => new CompletionOption(p.ID, Loc.GetString(p.ModeTitle))),
            "<presetId...> [rounds, or leave out to lock until cleared]");
    }
}
