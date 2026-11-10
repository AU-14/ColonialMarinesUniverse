using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Payload.EntitySystems;
using Content.Shared.CMU14.Chemistry.Fuel;
using Content.Shared.CMU14.Chemistry.Reagents;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.Database;
using Content.Shared.FixedPoint;
using Content.Shared.Payload.Components;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using ExplosionEffect = Content.Shared.EntityEffects.Effects.Transform.Explosion;

namespace Content.Server.CMU14.Chemistry;

/// <summary>
/// GraphAudit-ChemBotanyPlan N4 (FLOW-01): a chem grenade that detonates with an Explosive generated chemical blows up,
/// just as one with a fuel bursts into fire (<see cref="CMUPayloadFireSystem"/>). A generated chemical's blast lives in
/// its heat-gated <c>{ID}Ignition</c> reaction, which a grenade never heats; on detonation this reads that reaction's
/// <see cref="ExplosionEffect"/> for every reagent in the merged beakers and queues one combined explosion.
/// </summary>
/// <remarks>
/// COUPLING-01: this has no trigger subscription of its own. <see cref="PayloadSystem"/> owns the
/// <c>ChemicalPayloadComponent</c> trigger and <see cref="CMUPayloadFireSystem"/> the <c>ReactionMixerComponent</c> one,
/// and a second directed subscription to either pair fails at startup; the fire system calls
/// <see cref="TryDetonate"/> before it lights the payload. FLOW-02: explosive fuels are left in the beakers so the fire
/// step burns them as well.
/// </remarks>
public sealed partial class CMUPayloadExplosionSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private ExplosionSystem _explosion = default!;
    [Dependency] private CMUFuelProfileSystem _fuel = default!;
    [Dependency] private SharedReagentGeneratorSystem _generator = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    /// <summary>
    /// Queues one explosion for the Explosive generated chemicals in the payload's combined beakers, then removes the
    /// ones that are not also fuels. True when an explosion was queued.
    /// </summary>
    public bool TryDetonate(Entity<ChemicalPayloadComponent> payload)
        => TryDetonate(payload, out _);

    /// <summary>
    /// <see cref="TryDetonate(Entity{ChemicalPayloadComponent})"/>, also reporting the explosive fuels it left in the
    /// beakers for the fire step. If no fire forms, the caller must remove them with <see cref="RemoveReagents"/> or a
    /// later trigger of the same casing would blast again.
    /// </summary>
    public bool TryDetonate(Entity<ChemicalPayloadComponent> payload, out HashSet<string> keptFuels)
    {
        keptFuels = new HashSet<string>();
        var merged = new Solution { MaxVolume = FixedPoint2.MaxValue };
        var beakers = new List<Entity<SolutionComponent>>(2);
        foreach (var slot in new[] { payload.Comp.BeakerSlotA, payload.Comp.BeakerSlotB })
        {
            if (slot.Item is not { } beaker ||
                !TryComp(beaker, out FitsInDispenserComponent? fits) ||
                !_solutions.TryGetSolution(beaker, fits.Solution, out var soln, out var solution))
            {
                continue;
            }

            merged.AddSolution(solution, _prototypes);
            beakers.Add(soln.Value);
        }

        if (merged.Volume <= FixedPoint2.Zero)
            return false;

        var total = 0f;
        var slope = 0f;
        var maxIntensity = 0f;
        var tileBreakScale = 0f;
        var strongest = 0f;
        string? type = null;
        var spent = new HashSet<string>();
        foreach (var quantity in merged.Contents)
        {
            var reagent = quantity.Reagent.Prototype;
            var recipe = SharedReagentGeneratorSystem.IgnitionRecipeId(reagent);
            if (!_generator.IsIgnitionRecipe(recipe) ||
                !_prototypes.TryIndex<ReactionPrototype>(recipe, out var reaction))
            {
                continue;
            }

            var effect = reaction.Effects.OfType<ExplosionEffect>().FirstOrDefault();
            if (effect == null)
                continue;

            var intensity = MathF.Min(effect.IntensityPerUnit * quantity.Quantity.Float(), effect.MaxTotalIntensity);
            if (intensity <= 0f)
                continue;

            total += intensity;
            slope = MathF.Max(slope, effect.IntensitySlope);
            maxIntensity = MathF.Max(maxIntensity, effect.MaxIntensity);
            tileBreakScale = MathF.Max(tileBreakScale, effect.TileBreakScale);
            if (intensity > strongest)
            {
                strongest = intensity;
                type = effect.ExplosionType;
            }

            // FLOW-02: an explosive fuel stays for the fire step, which burns and strips every fuel.
            if (_fuel.IsFuel(reagent))
                keptFuels.Add(reagent);
            else
                spent.Add(reagent);
        }

        if (total <= 0f || type == null)
        {
            keptFuels.Clear();
            return false;
        }

        var center = _transform.GetMoverCoordinates(payload).SnapToGrid(EntityManager);
        var epicenter = _transform.ToMapCoordinates(center);
        if (epicenter.MapId == MapId.Nullspace)
        {
            keptFuels.Clear();
            return false;
        }

        _explosion.QueueExplosion(epicenter, type, total, slope, maxIntensity, payload.Owner, tileBreakScale, addLog: false);
        _adminLogger.Add(LogType.Explosion, LogImpact.High,
            $"Chemical bomb payload {ToPrettyString(payload.Owner):payload} detonated generated explosives ({type}) at {epicenter:coordinates} with intensity {total} slope {slope}: {SharedSolutionContainerSystem.ToPrettyString(merged):solution}");

        // The blast used the explosives up, so a dud casing cannot go off twice.
        RemoveReagents(payload, spent);

        return true;
    }

    /// <summary>Removes the given reagents from both of the payload's beakers.</summary>
    public void RemoveReagents(Entity<ChemicalPayloadComponent> payload, IReadOnlySet<string> reagents)
    {
        if (reagents.Count == 0)
            return;

        foreach (var slot in new[] { payload.Comp.BeakerSlotA, payload.Comp.BeakerSlotB })
        {
            if (slot.Item is not { } beaker ||
                !TryComp(beaker, out FitsInDispenserComponent? fits) ||
                !_solutions.TryGetSolution(beaker, fits.Solution, out var soln, out var solution))
            {
                continue;
            }

            foreach (var quantity in solution.Contents.ToArray())
            {
                if (reagents.Contains(quantity.Reagent.Prototype))
                    solution.RemoveReagent(quantity.Reagent, quantity.Quantity);
            }

            _solutions.UpdateChemicals(soln.Value, false);
        }
    }
}
