using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.CMU14.Expeditions;
using Robust.Shared.Console;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using System.Linq;

namespace Content.Server.CMU14.Expeditions;

[AdminCommand(AdminFlags.Mapping)]
public sealed partial class CMUExpeditionCommand : LocalizedEntityCommands
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private CMUExpeditionSystem _expeditions = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    public override string Command => "cmu-expedition";
    public override string Description => Loc.GetString("cmd-cmu-expedition-desc");
    public override string Help => Loc.GetString("cmd-cmu-expedition-help");

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 0)
            return CompletionResult.Empty;
        if (args.Length == 1)
            return CompletionResult.FromOptions(new[] { "scenarios", "scenario", "generate", "status", "open", "briefing", "time" });
        if (args.Length == 2)
        {
            return args[0] switch
            {
                "scenario" => CompletionResult.FromHintOptions(_prototypes.EnumeratePrototypes<CMUExpeditionScenarioPrototype>()
                    .OrderBy(proto => proto.ID).Select(proto => new CompletionOption(proto.ID, Loc.GetString(proto.Name))),
                    Loc.GetString("cmu-expedition-hint-scenario")),
                "generate" => CompletionResult.FromOptions(Enum.GetNames<CMUExpeditionBiome>()),
                "status" or "open" or "briefing" or "time" => CMUExpeditionCommandCompletion.Maps(EntityManager, shell, expeditionsOnly: true),
                _ => CompletionResult.Empty,
            };
        }
        if (args.Length == 3 && args[0] is "scenario" or "generate")
            return CompletionResult.FromHint(Loc.GetString("cmu-expedition-hint-seed"));
        if (args[0] == "generate")
            return args.Length switch
            {
                4 => CompletionResult.FromOptions(Enum.GetNames<CMUExpeditionLandform>()),
                5 => CompletionResult.FromOptions(Enum.GetNames<CMUExpeditionStory>()),
                _ => CompletionResult.Empty,
            };
        if (args[0] == "time")
            return args.Length switch
            {
                3 => CompletionResult.FromOptions(new[] { "hour" }),
                4 => CompletionResult.FromHintOptions(new[] { "0", "6", "12", "18" }, Loc.GetString("cmu-expedition-hint-hour")),
                _ => CompletionResult.Empty,
            };
        return CompletionResult.Empty;
    }

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length == 4 && args[0] == "time" && int.TryParse(args[1], out var timeMap) &&
            args[2] == "hour" && float.TryParse(args[3], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var hour) && _map.MapExists(new MapId(timeMap)))
        {
            shell.WriteLine(Loc.GetString(_expeditions.SetExpeditionTime(_map.GetMap(new MapId(timeMap)), hour)
                ? "cmu-expedition-time-set" : "cmu-expedition-not-ready"));
            return;
        }
        if (args.Length == 1 && args[0] == "scenarios")
        {
            foreach (var scenario in _prototypes.EnumeratePrototypes<CMUExpeditionScenarioPrototype>().OrderBy(s => s.ID))
                shell.WriteLine($"{scenario.ID}: {Loc.GetString(scenario.Name)} — {Loc.GetString(scenario.Briefing)}");
            return;
        }

        if (args.Length == 3 && args[0] == "scenario" && int.TryParse(args[2], out var scenarioSeed))
        {
            if (!_expeditions.TryGenerateScenario(args[1], scenarioSeed, out var uid, out var error))
            {
                shell.WriteError(Loc.GetString(error));
                return;
            }
            _entities.GetComponent<CMUExpeditionMapComponent>(uid).AutoOpen = true;
            shell.WriteLine(Loc.GetString("cmu-expedition-started",
                ("map", _entities.GetComponent<MapComponent>(uid).MapId), ("seed", scenarioSeed)));
            return;
        }

        if (args.Length is >= 3 and <= 5 && args[0] == "generate" &&
            Enum.TryParse<CMUExpeditionBiome>(args[1], true, out var biome) && Enum.IsDefined(biome) &&
            int.TryParse(args[2], out var seed))
        {
            var landform = (CMUExpeditionLandform) (CMUExpeditionGenerator.Hash(seed, 0, 0, 200) %
                (uint) Enum.GetValues<CMUExpeditionLandform>().Length);
            landform = biome switch
            {
                CMUExpeditionBiome.Beach => CMUExpeditionLandform.Coast,
                CMUExpeditionBiome.Mountain => CMUExpeditionLandform.Highlands,
                CMUExpeditionBiome.SwampJungle => CMUExpeditionLandform.Wetlands,
                _ => landform,
            };
            var story = CMUExpeditionStory.CrashRecovery;
            if ((args.Length >= 4 && !Enum.TryParse(args[3], true, out landform)) ||
                (args.Length == 5 && !Enum.TryParse(args[4], true, out story)) || !Enum.IsDefined(landform) || !Enum.IsDefined(story))
            {
                shell.WriteError(Help);
                return;
            }

            if (!_expeditions.TryGenerate($"CMUExpedition{biome}", seed, landform, story, out var uid, out var error))
            {
                shell.WriteError(Loc.GetString(error));
                return;
            }
            _entities.GetComponent<CMUExpeditionMapComponent>(uid).AutoOpen = true;
            var mapId = _entities.GetComponent<MapComponent>(uid).MapId;
            shell.WriteLine(Loc.GetString("cmu-expedition-started", ("map", mapId), ("seed", seed)));
            return;
        }

        if (args.Length == 2 && (args[0] == "status" || args[0] == "open" || args[0] == "briefing") &&
            int.TryParse(args[1], out var number) && _map.MapExists(new MapId(number)))
        {
            var uid = _map.GetMap(new MapId(number));
            if (!_entities.TryGetComponent<CMUExpeditionMapComponent>(uid, out var expedition))
            {
                shell.WriteError(Loc.GetString("cmu-expedition-not-map"));
                return;
            }
            if (args[0] == "open")
            {
                shell.WriteLine(Loc.GetString(_expeditions.OpenLandingZone(uid) ? "cmu-expedition-opened" : "cmu-expedition-not-ready"));
                return;
            }
            var plan = expedition.Plan;
            if (plan.WreckName is { } wreck)
                shell.WriteLine(Loc.GetString("cmu-expedition-wreck-report", ("ship", Loc.GetString(wreck)),
                    ("impact", Loc.GetString($"cmu-expedition-impact-{plan.CrashImpact.ToString().ToLowerInvariant()}"))));
            if (args[0] == "briefing")
            {
                if (expedition.Scenario is not { } scenarioId)
                {
                    shell.WriteError(Loc.GetString("cmu-expedition-no-scenario"));
                    return;
                }
                var scenario = _prototypes.Index(scenarioId);
                shell.WriteLine(Loc.GetString(scenario.Name));
                shell.WriteLine(Loc.GetString(scenario.History));
                shell.WriteLine(Loc.GetString(scenario.Briefing));
                shell.WriteLine(Loc.GetString("cmu-expedition-objective-location", ("x", plan.Objective.X), ("y", plan.Objective.Y)));
                return;
            }
            shell.WriteLine(Loc.GetString(expedition.Ready ? "cmu-expedition-ready" : "cmu-expedition-not-ready"));
            shell.WriteLine(Loc.GetString("cmu-expedition-details", ("seed", plan.Seed), ("biome", plan.Biome.ToString()),
                ("landform", plan.Landform.ToString()), ("story", plan.Story.ToString()),
                ("x", plan.LandingZone.X), ("y", plan.LandingZone.Y)));
            return;
        }
        shell.WriteError(Help);
    }
}
