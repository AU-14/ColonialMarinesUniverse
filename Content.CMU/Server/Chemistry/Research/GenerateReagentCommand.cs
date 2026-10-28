using System.Linq;
using Content.Server.Administration;
using Content.Server.CMU14.Chemistry.Reagents;
using Content.Shared.Administration;
using Content.Shared.CMU14.Chemistry.Reagent;
using Content.Shared.CMU14.Chemistry.Reagents;
using Content.Shared._RMC14.Chemistry.Reagent;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.Console;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Chemistry.Research;

[AdminCommand(AdminFlags.Debug)]
internal sealed partial class GenerateReagentCommand : LocalizedEntityCommands
{
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private ServerReagentGeneratorSystem _generator = default!;
    [Dependency] private ServerResearchDataTerminalSystem _research = default!;
    [Dependency] private RMCReagentSystem _reagent = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;

    public override string Command => "research:generatereagent";
    public override string Description => Loc.GetString("cmd-research-generatereagent-desc");
    public override string Help => Loc.GetString("cmd-research-generatereagent-help", ("command", Command));

    private static readonly (int MaxVolume, EntProtoId Container, string Solution)[] Containers =
    {
        (30, "RMCVial", "beaker"),
        (60, "CMBeaker", "beaker"),
        (120, "CMBeakerLarge", "beaker"),
        (300, "RMCBeakerHighCapacity", "beaker"),
        (500, "RMCReagentJug", "beaker"),
        (1000, "RMCTankReagentEmpty", "tank"),
    };

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 3)
        {
            shell.WriteError(Help);
            return;
        }

        if (!int.TryParse(args[0], out var tier) || tier is < 1 or > 3)
        {
            shell.WriteError("Tier must be number");
            return;
        }

        if (!int.TryParse(args[1], out var volume) || volume < 1 || volume > Containers[^1].MaxVolume)
        {
            shell.WriteError($"Volume must be a number 1-{Containers[^1].MaxVolume}");
            return;
        }

        var effects = new Dictionary<string, int>();
        for (var i = 2; i < args.Length; i++)
        {
            var split = args[i].Split(':');
            if (split.Length != 2 || !int.TryParse(split[1], out var level) || level < 1)
            {
                shell.WriteError($"Malformed \"{args[i]}\" - expected Property:Level");
                return;
            }

            if (!_prototype.TryIndex<ReagentPropertyPrototype>(split[0], out var property) || !IsGeneratable(property))
            {
                var valid = string.Join(", ", GeneratableProperties().Select(p => p.ID).OrderBy(id => id));
                shell.WriteError($"Unknown property \"{split[0]}\". Valid properties: {valid}");
                return;
            }

            if (level > property.MaxLevel)
            {
                shell.WriteLine($"{property.ID} level {level} exceeds its max of {property.MaxLevel} clamping");
                level = property.MaxLevel;
            }

            effects[property.ID] = level;
        }

        var data = new GeneratedReagentData
        {
            Class = ReagentClass.Ultra,
            GenTier = tier,
            ScanPointYield = tier switch { 1 => 3, 2 => 5, _ => 7 },
        };

        _generator.GenerateStats(ref data, true);
        _generator.GenerateName(ref data);
        data.Effects = effects;
        data.PropertyHint = effects.Keys.First();

        if (!_research.LegalizeChem(data) ||
            !_generator.ProceduralReagentData.TryGetValue(data.ID, out var registered) ||
            !_reagent.TryIndex(data.ID, out var reagentProto))
        {
            shell.WriteError("Generation failed");
            return;
        }

        shell.WriteLine($"Generated reagent: {registered.ID} (\"{reagentProto.LocalizedName}\")");
        shell.WriteLine($"  Tier: {registered.GenTier}  Color: {reagentProto.SubstanceColor.ToHexNoAlpha()}");
        shell.WriteLine($"  Overdose: {reagentProto.Overdose?.ToString() ?? "none"} / Critical: {reagentProto.CriticalOverdose?.ToString() ?? "none"}");
        shell.WriteLine("  Properties:");
        foreach (var (id, level) in registered.Effects)
            shell.WriteLine($"    - {id} {level}");

        if (registered.Recipe.Count > 0)
        {
            shell.WriteLine("  Recipe:");
            foreach (var (reactant, (amount, catalyst)) in registered.Recipe)
                shell.WriteLine($"    - {reactant} x{amount}{(catalyst ? " (catalyst)" : "")}");
        }
        else
        {
            shell.WriteLine("  No recipe generated.");
        }

        var coords = EntityCoordinates.Invalid;
        if (shell.Player?.AttachedEntity is { } attached && EntityManager.TryGetComponent(attached, out TransformComponent? xform))
            coords = xform.Coordinates;

        if (!coords.IsValid(EntityManager))
        {
            shell.WriteLine("No entity to spawncontainer at - skipping");
            return;
        }

        var (_, container, solutionName) = Array.Find(Containers, c => volume <= c.MaxVolume);
        var spawned = EntityManager.SpawnEntity(container, coords);
        if (_solution.TryGetSolution(spawned, solutionName, out var soln, out _))
        {
            _solution.TryAddReagent(soln.Value, registered.ID, FixedPoint2.New(volume));
            shell.WriteLine($"Spawned a {container} with {volume}u of {registered.ID}");
        }
        else
        {
            shell.WriteError($"Spawned {container} but could not find solution");
        }
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
            return CompletionResult.FromHint("<tier 1-3>");

        if (args.Length == 2)
            return CompletionResult.FromHint($"<volume 1-{Containers[^1].MaxVolume}>");

        var used = args.Skip(2).Select(a => a.Split(':')[0]).ToHashSet();
        var options = GeneratableProperties()
            .Where(p => !used.Contains(p.ID))
            .Select(p => new CompletionOption(p.ID, $"{p.LocalizedName} (max {p.MaxLevel})"))
            .OrderBy(o => o.Value);

        return CompletionResult.FromHintOptions(options, "<Property:Level>");
    }

    private IEnumerable<ReagentPropertyPrototype> GeneratableProperties()
    {
        return _prototype.EnumeratePrototypes<ReagentPropertyPrototype>().Where(IsGeneratable);
    }

    private static bool IsGeneratable(ReagentPropertyPrototype property)
    {
        return !property.Abstract && !string.IsNullOrEmpty(property.EffectName);
    }
}
