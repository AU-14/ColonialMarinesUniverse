using System.Linq;
using Content.Server.Administration;
using Content.Server.AU14.Round;
using Content.Server.GameTicking.Presets;
using Content.Shared._RMC14.Rules;
using Content.Shared.Administration;
using Content.Shared.AU14.Scenario;
using Content.Shared._CMU14.Threats;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;

namespace Content.Server.AU14.Scenario.Commands;

[AdminCommand(AdminFlags.Debug)]
public sealed class ScenarioPlanValidateCommand : IConsoleCommand
{
    private readonly string _command;
    private readonly string _surfaceName;

    private static readonly string[] TargetPresetIds =
    {
        "DistressSignal",
        "Insurgency",
        "ColonyFall",
    };

    public ScenarioPlanValidateCommand() : this("scenarioplanvalidate", "Scenario Plan")
    {
    }

    private ScenarioPlanValidateCommand(string command, string surfaceName)
    {
        _command = command;
        _surfaceName = surfaceName;
    }

    public static ScenarioPlanValidateCommand CreateAlias(string command, string surfaceName)
    {
        return new ScenarioPlanValidateCommand(command, surfaceName);
    }

    public string Command => _command;
    public string Description => $"Runs AU14 {_surfaceName} marker validation and prints the report.";
    public string Help =>
        $"Usage: {_command} [presetId|all] [playerCount] [planetPrototypeId] [selectedThreatId] [--markers] [--choices] [--backups]\n" +
        "Defaults to all target presets, max(current players, 100), current selected planet, and current selected threat.";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var includeMarkerReport = args.Any(IsMarkerReportArg);
        var includeChoicesReport = args.Any(IsChoicesReportArg);
        var includeBackupReport = args.Any(IsBackupReportArg);
        var positionalArgs = args
            .Where(arg => !IsMarkerReportArg(arg) && !IsChoicesReportArg(arg) && !IsBackupReportArg(arg))
            .ToArray();

        if (positionalArgs.Length > 4)
        {
            shell.WriteError(Help);
            return;
        }

        var prototypes = IoCManager.Resolve<IPrototypeManager>();
        var componentFactory = IoCManager.Resolve<IComponentFactory>();
        var playerManager = IoCManager.Resolve<IPlayerManager>();
        var systems = IoCManager.Resolve<IEntitySystemManager>();
        var round = systems.GetEntitySystem<AuRoundSystem>();
        var platoons = systems.GetEntitySystem<PlatoonSpawnRuleSystem>();
        var generator = systems.GetEntitySystem<ScenarioPlanSystem>();

        var presetIds = ResolvePresetIds(positionalArgs);
        if (presetIds.Count == 0)
        {
            shell.WriteError($"No preset matched '{positionalArgs[0]}'.");
            return;
        }

        var playerCount = Math.Max(playerManager.PlayerCount, 100);
        if (positionalArgs.Length >= 2 && !int.TryParse(positionalArgs[1], out playerCount))
        {
            shell.WriteError($"Invalid player count '{positionalArgs[1]}'.");
            return;
        }

        if (playerCount < 0)
        {
            shell.WriteError("Player count must be zero or greater.");
            return;
        }

        var planetId = positionalArgs.Length >= 3
            ? positionalArgs[2]
            : round.GetSelectedPlanetId();
        var mapId = round.GetSelectedPlanet()?.MapId;
        if (!string.IsNullOrWhiteSpace(planetId))
        {
            if (!TryGetPlanetMapId(prototypes, componentFactory, planetId, out mapId))
            {
                shell.WriteError($"Planet prototype '{planetId}' was not found or has no RMC planet map component.");
                return;
            }
        }

        var selectedThreatId = positionalArgs.Length >= 4
            ? positionalArgs[3]
            : round.SelectedThreat?.ID;

        foreach (var presetId in presetIds)
        {
            var request = new ScenarioPlanValidationRequest(
                presetId,
                playerCount,
                platoons.SelectedGovforPlatoon?.ID,
                platoons.SelectedOpforPlatoon?.ID,
                planetId,
                mapId,
                selectedThreatId,
                round.GetSelectedGovforShip(),
                round.GetSelectedOpforShip());

            var report = generator.ValidateMarkerCoverage(request);
            shell.WriteLine(
                $"{_surfaceName} validation for {presetId} " +
                $"(players={playerCount}, planet={planetId ?? "<any>"}, map={mapId ?? "<any>"}, threat={selectedThreatId ?? "<deferred/any>"}): " +
                $"{(report.IsValid ? "PASS" : "FAIL")}");
            shell.WriteLine(report.ToString());

            if (includeMarkerReport)
            {
                shell.WriteLine($"{_surfaceName} Spawn Marker migration report:");
                shell.WriteLine(generator.BuildMarkerMigrationReport(request).ToString());
            }

            if (includeChoicesReport)
            {
                shell.WriteLine("Voting Choices prototype migration report:");
                WriteVotingChoicesPrototypeReport(
                    shell,
                    generator,
                    prototypes,
                    componentFactory,
                    presetId,
                    playerCount,
                    planetId,
                    mapId);
            }

            if (includeBackupReport)
            {
                shell.WriteLine("Voting Backup prototype report:");
                WriteVotingBackupPrototypeReport(
                    shell,
                    generator,
                    prototypes,
                    componentFactory,
                    presetId,
                    playerCount,
                    planetId,
                    mapId);
            }
        }
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHintOptions(GetPresetCompletions(), "<presetId|all>"),
            2 => CompletionResult.FromHint("<playerCount>"),
            3 => CompletionResult.FromHintOptions(GetPlanetCompletions(), "<planetPrototypeId>"),
            4 => CompletionResult.FromHintOptions(GetThreatCompletions(), "<selectedThreatId>"),
            5 => CompletionResult.FromHintOptions(new[] { "--markers", "markers", "--choices", "choices", "--backups", "backups" }, "[--markers|--choices|--backups]"),
            _ => CompletionResult.Empty,
        };
    }

    private static bool IsMarkerReportArg(string arg)
    {
        return arg.Equals("--markers", StringComparison.OrdinalIgnoreCase) ||
               arg.Equals("markers", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsChoicesReportArg(string arg)
    {
        return arg.Equals("--choices", StringComparison.OrdinalIgnoreCase) ||
               arg.Equals("choices", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBackupReportArg(string arg)
    {
        return arg.Equals("--backups", StringComparison.OrdinalIgnoreCase) ||
               arg.Equals("backups", StringComparison.OrdinalIgnoreCase);
    }

    private static void WriteVotingChoicesPrototypeReport(
        IConsoleShell shell,
        ScenarioPlanSystem generator,
        IPrototypeManager prototypes,
        IComponentFactory componentFactory,
        string presetId,
        int playerCount,
        string? planetId,
        string? mapId)
    {
        var matchedAny = false;
        foreach (var votingChoices in prototypes.EnumeratePrototypes<VotingChoicesPrototype>()
                     .Where(choice => SupportsValue(choice.Presets, presetId))
                     .Where(choice => planetId == null || SupportsValue(choice.SupportedPlanets, planetId))
                     .OrderBy(choice => choice.ID, StringComparer.OrdinalIgnoreCase))
        {
            matchedAny = true;
            var choicePlanets = planetId != null
                ? new[] { planetId }
                : votingChoices.SupportedPlanets.ToArray();

            if (choicePlanets.Length == 0)
            {
                shell.WriteLine(
                    $"SKIP: Voting Choices {votingChoices.ID} has no supported planets to resolve.");
                continue;
            }

            foreach (var choicePlanetId in choicePlanets)
            {
                var choiceMapId = mapId;
                if (planetId == null &&
                    !TryGetPlanetMapId(prototypes, componentFactory, choicePlanetId, out choiceMapId))
                {
                    shell.WriteLine(
                        $"FAIL: Voting Choices {votingChoices.ID}/{choicePlanetId} could not resolve its planet map.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(choiceMapId))
                {
                    shell.WriteLine(
                        $"FAIL: Voting Choices {votingChoices.ID}/{choicePlanetId} has no map id to resolve.");
                    continue;
                }

                var choiceReport = generator.ValidateVotingChoicesPrototypeCoverage(
                    votingChoices.ID,
                    presetId,
                    choicePlanetId,
                    choiceMapId,
                    playerCount);
                if (choiceReport.Plans.Count == 0)
                {
                    shell.WriteLine(
                        $"FAIL: Voting Choices {votingChoices.ID}/{choicePlanetId} could not resolve. {choiceReport}");
                    continue;
                }

                var prototypePlan = choiceReport.Plans[0];
                if (!choiceReport.IsValid)
                {
                    shell.WriteLine(
                        $"FAIL: Voting Choices {votingChoices.ID}/{choicePlanetId} ({choiceMapId}) " +
                        $"groups={prototypePlan.Forces.Count}, deferredChoices={prototypePlan.DeferredForceChoices.Count}, " +
                        $"markers={prototypePlan.SpawnMarkers.Count}");
                    shell.WriteLine(choiceReport.ToString());
                    continue;
                }

                var warnings = choiceReport.Diagnostics.Count(diagnostic =>
                    diagnostic.Severity == ScenarioDiagnosticSeverity.Warning);
                shell.WriteLine(
                    $"OK: Voting Choices {votingChoices.ID}/{choicePlanetId} ({choiceMapId}) " +
                    $"groups={prototypePlan.Forces.Count}, deferredChoices={prototypePlan.DeferredForceChoices.Count}, " +
                    $"markers={prototypePlan.SpawnMarkers.Count}, warnings={warnings}");
            }
        }

        if (!matchedAny)
            shell.WriteLine($"No Voting Choices prototypes matched preset '{presetId}' and planet '{planetId ?? "<any>"}'.");
    }

    private static void WriteVotingBackupPrototypeReport(
        IConsoleShell shell,
        ScenarioPlanSystem generator,
        IPrototypeManager prototypes,
        IComponentFactory componentFactory,
        string presetId,
        int playerCount,
        string? planetId,
        string? mapId)
    {
        var matchedAny = false;
        foreach (var backup in prototypes.EnumeratePrototypes<VotingBackupPrototype>()
                     .Where(backup => backup.Preset.Equals(presetId, StringComparison.OrdinalIgnoreCase))
                     .Where(backup => planetId == null || SupportsValue(backup.SupportedPlanets, planetId))
                     .OrderBy(backup => backup.ID, StringComparer.OrdinalIgnoreCase))
        {
            matchedAny = true;
            var backupPlanets = planetId != null
                ? new[] { planetId }
                : backup.SupportedPlanets.ToArray();

            if (backupPlanets.Length == 0)
            {
                shell.WriteLine(
                    $"SKIP: Voting Backup {backup.ID} has no supported planets to resolve.");
                continue;
            }

            foreach (var backupPlanetId in backupPlanets)
            {
                var backupMapId = mapId;
                if (planetId == null &&
                    !TryGetPlanetMapId(prototypes, componentFactory, backupPlanetId, out backupMapId))
                {
                    shell.WriteLine(
                        $"FAIL: Voting Backup {backup.ID}/{backupPlanetId} could not resolve its planet map.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(backupMapId))
                {
                    shell.WriteLine(
                        $"FAIL: Voting Backup {backup.ID}/{backupPlanetId} has no map id to resolve.");
                    continue;
                }

                var backupReport = generator.ValidateVotingChoicesPrototypeCoverage(
                    backup.VotingChoices.Id,
                    presetId,
                    backupPlanetId,
                    backupMapId,
                    playerCount);
                if (backupReport.Plans.Count == 0)
                {
                    shell.WriteLine(
                        $"FAIL: Voting Backup {backup.ID}/{backupPlanetId} choices {backup.VotingChoices.Id} could not resolve. {backupReport}");
                    continue;
                }

                var backupPlan = backupReport.Plans[0];
                if (!backupReport.IsValid)
                {
                    shell.WriteLine(
                        $"FAIL: Voting Backup {backup.ID}/{backupPlanetId} choices {backup.VotingChoices.Id} ({backupMapId}) " +
                        $"groups={backupPlan.Forces.Count}, deferredChoices={backupPlan.DeferredForceChoices.Count}, " +
                        $"markers={backupPlan.SpawnMarkers.Count}");
                    shell.WriteLine(backupReport.ToString());
                    continue;
                }

                var warnings = backupReport.Diagnostics.Count(diagnostic =>
                    diagnostic.Severity == ScenarioDiagnosticSeverity.Warning);
                shell.WriteLine(
                    $"OK: Voting Backup {backup.ID}/{backupPlanetId} choices {backup.VotingChoices.Id} ({backupMapId}) " +
                    $"groups={backupPlan.Forces.Count}, deferredChoices={backupPlan.DeferredForceChoices.Count}, " +
                    $"markers={backupPlan.SpawnMarkers.Count}, warnings={warnings}");
            }
        }

        if (!matchedAny)
            shell.WriteLine($"No Voting Backup prototypes matched preset '{presetId}' and planet '{planetId ?? "<any>"}'.");
    }

    private static bool SupportsValue(IReadOnlyCollection<string> values, string value)
    {
        return values.Count == 0 ||
               values.Any(candidate => candidate.Equals(value, StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<string> ResolvePresetIds(string[] args)
    {
        if (args.Length == 0 ||
            args[0].Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return TargetPresetIds;
        }

        var prototypes = IoCManager.Resolve<IPrototypeManager>();
        if (!prototypes.HasIndex<GamePresetPrototype>(args[0]))
            return Array.Empty<string>();

        return new[] { args[0] };
    }

    private static bool TryGetPlanetMapId(
        IPrototypeManager prototypes,
        IComponentFactory componentFactory,
        string planetId,
        out string? mapId)
    {
        mapId = null;
        if (!prototypes.TryIndex<EntityPrototype>(planetId, out var prototype) ||
            !prototype.TryGetComponent(out RMCPlanetMapPrototypeComponent? planet, componentFactory))
        {
            return false;
        }

        mapId = planet.MapId;
        return true;
    }

    private static IReadOnlyList<string> GetPresetCompletions()
    {
        var prototypes = IoCManager.Resolve<IPrototypeManager>();
        return TargetPresetIds
            .Append("all")
            .Concat(prototypes.EnumeratePrototypes<GamePresetPrototype>().Select(preset => preset.ID))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<string> GetPlanetCompletions()
    {
        var prototypes = IoCManager.Resolve<IPrototypeManager>();
        var componentFactory = IoCManager.Resolve<IComponentFactory>();
        return prototypes.EnumeratePrototypes<EntityPrototype>()
            .Where(prototype => prototype.TryGetComponent(out RMCPlanetMapPrototypeComponent? _, componentFactory))
            .Select(prototype => prototype.ID)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<string> GetThreatCompletions()
    {
        var prototypes = IoCManager.Resolve<IPrototypeManager>();
        return prototypes.EnumeratePrototypes<ThreatPrototype>()
            .Select(threat => threat.ID)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

[AdminCommand(AdminFlags.Debug)]
public sealed class RoundSetupValidateCommand : IConsoleCommand
{
    private readonly ScenarioPlanValidateCommand _inner =
        ScenarioPlanValidateCommand.CreateAlias("roundsetupvalidate", "Round Setup");

    public string Command => _inner.Command;
    public string Description => _inner.Description;
    public string Help => _inner.Help;

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        _inner.Execute(shell, argStr, args);
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return _inner.GetCompletion(shell, args);
    }
}
