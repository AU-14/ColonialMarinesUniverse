// SPDX-License-Identifier: AGPL-3.0-only
// Ported from the farming of crystall-punk-14 (MIT / CC-BY-SA) and adapted to this project.
using Content.Shared.Botany.Components;
using Content.Shared.Burial.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.CMU14.Farming;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Farming;

// Server side of farming: plants grow with time, take light and water from their surroundings, and spend them.
public sealed class FarmingSystem : SharedFarmingSystem
{
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly IRobustRandom _rand = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private readonly SharedPopupSystem _popups = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private EntityQuery<SolutionManagerComponent> _solutionQuery;

    public override void Initialize()
    {
        base.Initialize();

        _solutionQuery = GetEntityQuery<SolutionManagerComponent>();

        SubscribeLocalEvent<FarmPlantComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<FarmLightEnergyComponent, FarmPlantUpdateEvent>(OnTakeEnergyFromLight);
        SubscribeLocalEvent<FarmMetabolizerComponent, FarmPlantUpdateEvent>(OnPlantMetabolizing);
        SubscribeLocalEvent<FarmGrowingComponent, FarmAfterPlantUpdateEvent>(OnPlantGrowing);
        SubscribeLocalEvent<FarmPlanterSoilComponent, InteractUsingEvent>(OnPlanterInteractUsing);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<FarmPlantComponent>();
        while (query.MoveNext(out var uid, out var plant))
        {
            if (_gameTiming.CurTime <= plant.NextUpdateTime)
                continue;

            plant.NextUpdateTime = _gameTiming.CurTime + TimeSpan.FromSeconds(_rand.NextFloat(plant.UpdateFrequency));

            var ev = new FarmPlantUpdateEvent((uid, plant));
            RaiseLocalEvent(uid, ev);

            AffectResource((uid, plant), ev.ResourceDelta);
            AffectEnergy((uid, plant), ev.EnergyDelta);

            var after = new FarmAfterPlantUpdateEvent((uid, plant));
            RaiseLocalEvent(uid, after);

            Dirty(uid, plant);
        }
    }

    private void OnMapInit(Entity<FarmPlantComponent> plant, ref MapInitEvent args)
    {
        plant.Comp.NextUpdateTime = _gameTiming.CurTime + TimeSpan.FromSeconds(_rand.NextFloat(plant.Comp.UpdateFrequency));
    }

    private void OnTakeEnergyFromLight(Entity<FarmLightEnergyComponent> light, ref FarmPlantUpdateEvent args)
    {
        args.EnergyDelta += light.Comp.Energy;
    }

    private void OnPlantGrowing(Entity<FarmGrowingComponent> growing, ref FarmAfterPlantUpdateEvent args)
    {
        if (args.Plant.Comp.Energy < growing.Comp.EnergyCost ||
            args.Plant.Comp.Resource < growing.Comp.ResourceCost ||
            args.Plant.Comp.GrowthLevel >= 1)
        {
            return;
        }

        AffectEnergy(args.Plant, -growing.Comp.EnergyCost);
        AffectResource(args.Plant, -growing.Comp.ResourceCost);
        AffectGrowth(args.Plant, growing.Comp.GrowthPerUpdate);
    }

    private void OnPlantMetabolizing(Entity<FarmMetabolizerComponent> ent, ref FarmPlantUpdateEvent args)
    {
        if (!PlantQuery.TryComp(ent, out var plant) || plant.Solution is not { } solutionName || !_solutionQuery.TryComp(args.Plant, out var manager))
            return;

        var solEntity = new Entity<SolutionManagerComponent?>(args.Plant, manager);
        if (!_solutionContainer.TryGetSolution(solEntity, solutionName, out var soln, out _))
            return;

        if (!_proto.TryIndex(ent.Comp.MetabolizerId, out var metabolizer))
            return;

        var drunk = _solutionContainer.SplitSolution(soln.Value, ent.Comp.SolutionPerUpdate);
        foreach (var reagent in drunk)
        {
            if (!metabolizer.Metabolization.TryGetValue(reagent.Reagent.ToString(), out var effects))
                continue;

            foreach (var effect in effects)
            {
                effect.Effect((ent, plant), reagent.Quantity, EntityManager);
            }
        }
    }

    // A shovel put to a planter: the empty one gets soil (it is swapped for the filled one), and the filled one is dug out again, unless something grows in it.
    private void OnPlanterInteractUsing(Entity<FarmPlanterSoilComponent> planter, ref InteractUsingEvent args)
    {
        if (args.Handled || !HasComp<ShovelComponent>(args.Used))
            return;

        args.Handled = true;

        var planterXform = Transform(planter);
        var coords = planterXform.Coordinates;

        // A planter with something growing in it keeps its soil.
        if (planter.Comp.Filled && TryComp<PlantTrayComponent>(planter, out var tray) && tray.PlantEntity != null)
        {
            _popups.PopupEntity(Loc.GetString("au14-farming-planter-planted"), planter, args.User);
            return;
        }

        var swapped = Spawn(planter.Comp.Swap, coords);
        var xform = Transform(swapped);
        _transform.SetLocalRotation(swapped, planterXform.LocalRotation, xform);

        // Preserve the original planter's anchoring without writing to the read-only transform property.
        if (planterXform.Anchored && !xform.Anchored)
            _transform.AnchorEntity(swapped, xform);
        else if (!planterXform.Anchored && xform.Anchored)
            _transform.Unanchor(swapped, xform);

        _popups.PopupEntity(Loc.GetString(planter.Comp.Filled ? "au14-farming-planter-emptied" : "au14-farming-planter-filled"), swapped, args.User);
        QueueDel(planter);
    }
}
