using System.Linq;
using Content.Server.Payload.EntitySystems;
using Content.Shared._RMC14.Atmos;
using Content.Shared.CMU14.Chemistry.Fuel;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.FixedPoint;
using Content.Shared.Payload.Components;
using Content.Shared.Trigger;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Chemistry;

/// <summary>
/// FLOW-22: a chem grenade that detonates with a fuel mix sprays it around itself as fire. After
/// <see cref="PayloadSystem"/> has merged and mixed the two beakers (and split the result back between them), the
/// combined solution is resolved as an <see cref="CMUFireSource.Explosion"/> fire spec and, when it burns, fills a
/// circle of √units radius capped by the spec; the fuel is consumed so a dud cannot light twice.
/// </summary>
/// <remarks>
/// <see cref="PayloadSystem"/> owns the <c>ChemicalPayloadComponent</c> trigger subscription, so this listens on the
/// payload's <see cref="ReactionMixerComponent"/> (every chemical payload has one for CMUMix) and checks for the
/// payload itself.
/// </remarks>
public sealed partial class CMUPayloadFireSystem : EntitySystem
{
    [Dependency] private CMUFuelProfileSystem _fuel = default!;
    [Dependency] private SharedRMCFlammableSystem _flammable = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ReactionMixerComponent, TriggerEvent>(OnTrigger, after: new[] { typeof(PayloadSystem) });
    }

    private void OnTrigger(Entity<ReactionMixerComponent> ent, ref TriggerEvent args)
    {
        if (!TryComp(ent, out ChemicalPayloadComponent? payload) ||
            (args.Key != null && !payload.KeysIn.Contains(args.Key)))
        {
            return;
        }

        TryIgnite((ent, payload));
    }

    /// <summary>Lights the payload's combined beakers if they resolve to a fire. True when a fire was spawned.</summary>
    public bool TryIgnite(Entity<ChemicalPayloadComponent> payload)
    {
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

        if (merged.Volume <= FixedPoint2.Zero ||
            !_fuel.TryResolveFire(merged, CMUFireSource.Explosion, out var spec))
        {
            return false;
        }

        var center = _transform.GetMoverCoordinates(payload).SnapToGrid(EntityManager);
        if (_transform.GetMapId(center) == MapId.Nullspace)
            return false;

        var radius = CMUFuelProfileSystem.CircleRadius(merged.Volume, spec);
        _flammable.SpawnFireCircle(spec.BehaviourEntity, center, radius, spec);

        // The burst used the fuel up.
        foreach (var soln in beakers)
        {
            var solution = soln.Comp.Solution;
            foreach (var quantity in solution.Contents.ToArray())
            {
                if (_fuel.IsFuel(quantity.Reagent.Prototype))
                    solution.RemoveReagent(quantity.Reagent, quantity.Quantity);
            }

            _solutions.UpdateChemicals(soln, false);
        }

        return true;
    }
}
