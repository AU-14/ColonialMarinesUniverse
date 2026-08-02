using System.Linq;
using Content.Server.Administration;
using Content.Shared._CMU14.Yautja;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Utility;

namespace Content.Server._CMU14.Yautja;

[AdminCommand(AdminFlags.Clans)]
public sealed partial class YautjaYoungbloodCallCommand : IConsoleCommand
{
    [Dependency] private IEntityManager _entities = default!;

    public string Command => "yautja_youngblood_call";
    public string Description => Loc.GetString("cmu-yautja-admin-youngblood-description");
    public string Help => Loc.GetString("cmu-yautja-admin-youngblood-help");

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError(Help);
            return;
        }

        if (!TryGetBloodingConsole(out var console))
        {
            shell.WriteError(Loc.GetString("cmu-yautja-admin-youngblood-no-console"));
            return;
        }

        if (!HasYoungbloodDestination())
        {
            shell.WriteError(Loc.GetString("cmu-yautja-admin-youngblood-no-ground"));
            return;
        }

        if (!HasYoungbloodSpawnPoint())
        {
            shell.WriteError(Loc.GetString("cmu-yautja-admin-youngblood-no-spawn"));
            return;
        }

        var option = console.Comp.BloodingCallOptions
            .FirstOrDefault(candidate => string.Equals(candidate.Id, args[0], StringComparison.OrdinalIgnoreCase));
        if (option == null)
        {
            shell.WriteError(Loc.GetString("cmu-yautja-admin-youngblood-unknown-call", ("id", args[0])));
            return;
        }

        var requester = shell.Player?.AttachedEntity ?? console.Owner;
        if (!_entities.System<YautjaHuntConsoleSystem>()
                .TryCreateYoungbloodCall(console, requester, option, bypassEligibility: true))
        {
            shell.WriteError(Loc.GetString("cmu-yautja-admin-youngblood-create-failed"));
            return;
        }

        shell.WriteLine(Loc.GetString("cmu-yautja-admin-youngblood-created", ("id", option.Id)));
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length != 1 || !TryGetBloodingConsole(out var console))
            return CompletionResult.Empty;

        return CompletionResult.FromHintOptions(
            console.Comp.BloodingCallOptions.Select(option => option.Id),
            Loc.GetString("cmu-yautja-admin-youngblood-call-id"));
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
