using System.Linq;
using Content.Server.Administration;
using Content.Shared._CMU14.Yautja;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._CMU14.Yautja;

[AdminCommand(AdminFlags.Admin)]
public sealed partial class YautjaYoungbloodCallCommand : IConsoleCommand
{
    [Dependency] private IEntityManager _entities = default!;

    public string Command => "yautja_youngblood_call";
    public string Description => "Creates a Youngblood hunting-ground call without eligibility checks.";
    public string Help => "Usage: yautja_youngblood_call <call id>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError(Help);
            return;
        }

        if (!TryGetBloodingConsole(out var console))
        {
            shell.WriteError("No blooding console is available.");
            return;
        }

        if (!HasYoungbloodDestination())
        {
            shell.WriteError("No hunting ground is active. Select one at the hunter flight console first.");
            return;
        }

        if (!HasYoungbloodSpawnPoint())
        {
            shell.WriteError("No Yautja ship Youngblood spawn is available.");
            return;
        }

        var option = console.Comp.BloodingCallOptions
            .FirstOrDefault(candidate => string.Equals(candidate.Id, args[0], StringComparison.OrdinalIgnoreCase));
        if (option == null)
        {
            shell.WriteError($"Unknown Youngblood call id: {args[0]}");
            return;
        }

        var requester = shell.Player?.AttachedEntity ?? console.Owner;
        if (!_entities.System<YautjaHuntConsoleSystem>()
                .TryCreateYoungbloodCall(console, requester, option, bypassEligibility: true))
        {
            shell.WriteError("Unable to create the Youngblood call.");
            return;
        }

        shell.WriteLine($"Created Youngblood call {option.Id} without eligibility checks.");
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length != 1 || !TryGetBloodingConsole(out var console))
            return CompletionResult.Empty;

        return CompletionResult.FromHintOptions(
            console.Comp.BloodingCallOptions.Select(option => option.Id),
            "<call id>");
    }

    private bool TryGetBloodingConsole(out Entity<YautjaHuntConsoleComponent> console)
    {
        var query = _entities.EntityQueryEnumerator<YautjaHuntConsoleComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (component.Kind != YautjaHuntConsoleKind.Blooding)
                continue;

            console = (uid, component);
            return true;
        }

        console = default;
        return false;
    }

    private bool HasYoungbloodDestination()
    {
        var query = _entities.EntityQueryEnumerator<YautjaHuntTeleportDestinationComponent>();
        while (query.MoveNext(out _, out var destination))
        {
            if (destination.Kind == YautjaHuntTeleporterKind.Young)
                return true;
        }

        return false;
    }

    private bool HasYoungbloodSpawnPoint()
    {
        var query = _entities.EntityQueryEnumerator<YautjaHuntSpawnPointComponent>();
        while (query.MoveNext(out _, out var spawnPoint))
        {
            if (spawnPoint.Kind == YautjaHuntSpawnKind.Youngblood)
                return true;
        }

        return false;
    }
}
