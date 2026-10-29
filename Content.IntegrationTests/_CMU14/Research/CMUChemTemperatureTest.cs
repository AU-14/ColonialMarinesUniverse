using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Chemistry.Reagents;
using Content.Shared._RMC14.Atmos;
using Content.Shared.CMU14.Atmos;
using Content.Shared.CMU14.Chemistry.Effects.Reaction;
using Content.Shared.CMU14.Chemistry.Fuel;
using Content.Shared.CMU14.Chemistry.Reagent;
using Content.Shared.CMU14.Chemistry.Reagents;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.EntityEffects.Effects.Solution;
using Content.Shared.EntityEffects.Effects.Transform;
using Content.Shared.Chemistry.Components;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._CMU14.Research;

/// <summary>
/// Procgen chemistry and temperature: level-scaled Explosive blasts (FLOW-01), ignition reactions for
/// combustible chemicals (ARCH-01 P2), exo/endothermic synthesis (ARCH-01 P1), heat-gated hand-authored
/// reactions (FLOW-10) and the Hard-contract mix gate (ARCH-04).
/// </summary>
[TestFixture]
public sealed class CMUChemTemperatureTest : GameTest
{
    // Generated prototypes survive entity cleanup and would change later tests' reactions.
    public override PoolSettings PoolSettings => new() { Connected = false, Destructive = true };

    private static GeneratedReagentData NewOffer(int tier, int index)
        => new()
        {
            ID = $"CMUHeatTest-T{tier}-{index}",
            Name = $"heat test {tier}-{index}",
            GenTier = tier,
            Class = ReagentClass.Ultra,
            Effects = [],
            Recipe = [],
        };

    /// <summary>Ingredients known not to collide with a hand-authored reaction (see CMUProcgenChemistryTest).</summary>
    private static void AddRecipe(ref GeneratedReagentData data)
    {
        data.Recipe["RMCGold"] = (1, false);
        data.Recipe["RMCPlatinum"] = (1, false);
        data.Recipe["CMUTearGas"] = (1, false);
    }

    private GeneratedReagentData Generate(int tier, int index, params (string Property, int Level)[] effects)
    {
        var data = NewOffer(tier, index);
        foreach (var (property, level) in effects)
            data.Effects[property] = level;
        AddRecipe(ref data);
        SEntMan.EventBus.RaiseEvent(EventSource.Local, new GenerateReagentEvent(data));
        return data;
    }

    [Test]
    public void ExplosionScalesWithLevelAndCapsAtStrongIed()
    {
        float lastRadius = 0, lastMax = 0, lastTotal = 0;
        for (var level = 1; level <= SharedReagentGeneratorSystem.ExplosiveLevelCap; level++)
        {
            SharedReagentGeneratorSystem.ComputeExplosion(level, out var slope, out var max, out var total, out var perUnit);
            var radius = max / slope;
            Assert.Multiple(() =>
            {
                Assert.That(radius, Is.GreaterThan(lastRadius), $"L{level} radius must grow");
                Assert.That(max, Is.GreaterThan(lastMax), $"L{level} peak must grow");
                Assert.That(total, Is.GreaterThan(lastTotal), $"L{level} total must grow");
                Assert.That(perUnit * SharedReagentGeneratorSystem.ExplosionUnitsToCap, Is.EqualTo(total).Within(0.01f),
                    "a few units must reach the cap");
            });
            lastRadius = radius;
            lastMax = max;
            lastTotal = total;
        }

        // L10 is the strong IED (AU14IEDLarge: total 1800, slope 1.5 -> radius ~10.5, peak ~15.7).
        SharedReagentGeneratorSystem.ComputeExplosion(10, out var capSlope, out var capMax, out var capTotal, out _);
        Assert.Multiple(() =>
        {
            Assert.That(capMax / capSlope, Is.EqualTo(10.5f).Within(0.01f));
            Assert.That(capMax, Is.EqualTo(15.7f).Within(0.01f));
            Assert.That(capSlope, Is.EqualTo(1.5f).Within(0.01f));
            Assert.That(capTotal, Is.EqualTo(1800f).Within(1).Percent);
        });

        // Above the cap nothing grows any further.
        SharedReagentGeneratorSystem.ComputeExplosion(20, out var slope20, out var max20, out var total20, out var perUnit20);
        SharedReagentGeneratorSystem.ComputeExplosion(10, out var slope10, out var max10, out var total10, out var perUnit10);
        Assert.Multiple(() =>
        {
            Assert.That(slope20, Is.EqualTo(slope10));
            Assert.That(max20, Is.EqualTo(max10));
            Assert.That(total20, Is.EqualTo(total10));
            Assert.That(perUnit20, Is.EqualTo(perUnit10));
        });
    }

    [Test]
    public async Task CombustibleChemicalsIgniteWhenHeatedNotOnSynthesis()
    {
        await Server.WaitAssertion(() =>
        {
            var gen = SEntMan.System<ServerReagentGeneratorSystem>();

            // Explosive: the synthesis is inert, the ignition reaction detonates.
            var explosive = Generate(2, 1, ("Explosive", 4));
            var synthesis = SProtoMan.Index<ReactionPrototype>(explosive.ID);
            Assert.That(synthesis.Effects.OfType<Explosion>(), Is.Empty, "Explosive must not detonate on synthesis");

            var ignitionId = SharedReagentGeneratorSystem.IgnitionRecipeId(explosive.ID);
            Assert.That(SProtoMan.TryIndex<ReactionPrototype>(ignitionId, out var ignition), Is.True, "no ignition reaction");
            SharedReagentGeneratorSystem.ComputeExplosion(4, out var slope, out var max, out var total, out var perUnit);
            Assert.Multiple(() =>
            {
                Assert.That(gen.GeneratedRecipes, Does.Contain(ignitionId), "ignition must be tracked for round cleanup");
                Assert.That(ignition!.MinimumTemperature, Is.GreaterThanOrEqualTo(SharedReagentGeneratorSystem.IgnitionMinTemperature));
                Assert.That(ignition.MinimumTemperature, Is.EqualTo(SharedReagentGeneratorSystem.IgnitionTemperature(4)).Within(0.01f));
                Assert.That(ignition.Priority, Is.EqualTo(SharedReagentGeneratorSystem.GeneratedRecipePriority));
                Assert.That(ignition.Products, Is.Empty);
                Assert.That(ignition.Reactants.Keys.Select(k => k.Id), Is.EquivalentTo(new[] { explosive.ID }));
                Assert.That(ignition.Reactants[explosive.ID].Amount, Is.EqualTo(FixedPoint2.New(1)));
                Assert.That(ignition.Reactants[explosive.ID].Catalyst, Is.False);
                Assert.That(ignition.Effects.OfType<CMUSolutionTileFire>(), Is.Empty, "Explosive alone does not set fires");
            });
            var blast = ignition!.Effects.OfType<Explosion>().Single();
            Assert.Multiple(() =>
            {
                Assert.That(blast.MaxIntensity, Is.EqualTo(max).Within(0.01f));
                Assert.That(blast.IntensitySlope, Is.EqualTo(slope).Within(0.01f));
                Assert.That(blast.MaxTotalIntensity, Is.EqualTo(total).Within(0.01f));
                Assert.That(blast.IntensityPerUnit, Is.EqualTo(perUnit).Within(0.01f));
            });

            // Fueling: tile fire whose stats come from the burning solution (FLOW-22), nothing fixed in the reaction.
            var fuel = Generate(2, 2, ("Fueling", 2));
            Assert.That(SProtoMan.Index<ReactionPrototype>(fuel.ID).Effects.OfType<CMUSolutionTileFire>(), Is.Empty);
            var fuelIgnition = SProtoMan.Index<ReactionPrototype>(SharedReagentGeneratorSystem.IgnitionRecipeId(fuel.ID));
            var fire = fuelIgnition.Effects.OfType<CMUSolutionTileFire>().Single();
            Assert.Multiple(() =>
            {
                Assert.That(fuelIgnition.MinimumTemperature, Is.EqualTo(SharedReagentGeneratorSystem.IgnitionTemperature(2)).Within(0.01f));
                Assert.That(fuelIgnition.Effects.OfType<Explosion>(), Is.Empty);
                Assert.That(fire.Reagent?.Id, Is.EqualTo(fuel.ID));
                Assert.That(fire.Intensity, Is.Null, "intensity comes from the solution's fire spec");
                Assert.That(fire.Duration, Is.Null, "duration comes from the solution's fire spec");
                Assert.That(fire.FireEntity, Is.Null, "colour comes from the intensity tier, not a forced entity");
            });

            // A range property widens the fire through the chemical's radius in the fire spec, capped for containers.
            var wide = Generate(2, 3, ("Fueling", 1), ("Radius", 2));
            var wideFire = SProtoMan.Index<ReactionPrototype>(SharedReagentGeneratorSystem.IgnitionRecipeId(wide.ID))
                .Effects.OfType<CMUSolutionTileFire>().Single();
            Assert.That(wideFire.Reagent?.Id, Is.EqualTo(wide.ID));
            var wideSolution = new Solution();
            wideSolution.AddReagent(wide.ID, FixedPoint2.New(100));
            Assert.That(SEntMan.System<CMUFuelProfileSystem>().TryResolveFire(wideSolution, CMUFireSource.Container, out var wideSpec), Is.True);
            Assert.That(wideSpec.Radius, Is.EqualTo(Math.Min(CMUFuelProfileSystem.MaxChemicalRadius, SProtoMan.Index<ReagentPrototype>(wide.ID).Radius)));
            Assert.That(wideSpec.Radius, Is.GreaterThan(1));

            // Igniting is a combustion property too: it gets an ignition reaction that sets a fire.
            var igniting = Generate(2, 7, ("Igniting", 2));
            Assert.That(SProtoMan.TryIndex<ReactionPrototype>(SharedReagentGeneratorSystem.IgnitionRecipeId(igniting.ID), out var ignitingIgnition),
                Is.True, "Igniting must ignite when heated");
            Assert.That(ignitingIgnition!.Effects.OfType<CMUSolutionTileFire>().Count(), Is.EqualTo(1));

            // The strongest combustion property sets the ignition point, which never drops below the floor.
            var volatileMix = Generate(2, 4, ("Fueling", 1), ("Oxidizing", 9), ("Explosive", 2));
            var volatileIgnition = SProtoMan.Index<ReactionPrototype>(SharedReagentGeneratorSystem.IgnitionRecipeId(volatileMix.ID));
            Assert.Multiple(() =>
            {
                Assert.That(volatileIgnition.MinimumTemperature, Is.EqualTo(SharedReagentGeneratorSystem.IgnitionMinTemperature).Within(0.01f));
                Assert.That(volatileIgnition.Effects.OfType<Explosion>().Count(), Is.EqualTo(1));
                Assert.That(volatileIgnition.Effects.OfType<CMUSolutionTileFire>().Count(), Is.EqualTo(1));
            });

            // A non-combustible chemical gets no ignition reaction.
            var inert = Generate(2, 5, ("Antitoxic", 1));
            Assert.That(SProtoMan.HasIndex<ReactionPrototype>(SharedReagentGeneratorSystem.IgnitionRecipeId(inert.ID)), Is.False);

            // An ignition reaction never counts as a recipe collision for a recipe that uses that chemical.
            // The chemical alone is the worst case: its ignition's reactant set is exactly the recipe's.
            var user = NewOffer(2, 6);
            user.Recipe[explosive.ID] = (1, false);
            Assert.That(gen.IsDuplicate(ref user, out _), Is.False, "a generated chemical's ignition blocked a recipe using it");
        });
    }

    [Test]
    public async Task ThermicPropertiesHeatOrCoolTheSynthesis()
    {
        await Server.WaitAssertion(() =>
        {
            var hot = Generate(2, 10, ("Hyperthermic", 2));
            var heat = SProtoMan.Index<ReactionPrototype>(hot.ID).Effects.OfType<AdjustSolutionThermalEnergy>().Single();
            var cold = Generate(2, 11, ("Hypothermic", 2));
            var chill = SProtoMan.Index<ReactionPrototype>(cold.ID).Effects.OfType<AdjustSolutionThermalEnergy>().Single();
            var plain = Generate(2, 12, ("Antitoxic", 1));
            Assert.Multiple(() =>
            {
                Assert.That(heat.Delta, Is.GreaterThan(0));
                Assert.That(chill.Delta, Is.LessThan(0));
                Assert.That(heat.MaxTemp, Is.LessThan(SharedReagentGeneratorSystem.IgnitionMinTemperature),
                    "an exothermic synthesis must not be able to set off an ignition reaction");
                Assert.That(chill.MinTemp, Is.GreaterThan(0));
                Assert.That(SProtoMan.Index<ReactionPrototype>(plain.ID).Effects.OfType<AdjustSolutionThermalEnergy>(), Is.Empty);
                Assert.That(SProtoMan.Index<ReactionPrototype>(plain.ID).ConserveEnergy, Is.False,
                    "generated synthesis must not multiply the solution temperature");
            });
        });
    }

    [Test]
    public async Task HandAuthoredDrugReactionsNeedHeat()
    {
        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(SProtoMan.Index<ReactionPrototype>("CMUMethamphetamine").MinimumTemperature, Is.EqualTo(380f));
                Assert.That(SProtoMan.Index<ReactionPrototype>("CMUMethBase").MinimumTemperature, Is.EqualTo(380f));
                Assert.That(SProtoMan.Index<ReactionPrototype>("CMUFentanyl").MinimumTemperature, Is.EqualTo(370f));
                Assert.That(SProtoMan.Index<ReactionPrototype>("CMUPhenolFormaldehyde").MinimumTemperature, Is.EqualTo(374f));
            });
        });
    }

    [Test]
    public async Task HardGeneratedRecipesNeedMixing()
    {
        await Server.WaitAssertion(() =>
        {
            var hard = Generate(3, 20, ("Fueling", 1));
            var easy = Generate(1, 21, ("Antitoxic", 1));
            var hardRecipe = SProtoMan.Index<ReactionPrototype>(hard.ID);
            var easyRecipe = SProtoMan.Index<ReactionPrototype>(easy.ID);
            var hardIgnition = SProtoMan.Index<ReactionPrototype>(SharedReagentGeneratorSystem.IgnitionRecipeId(hard.ID));
            Assert.Multiple(() =>
            {
                Assert.That(hardRecipe.MixingCategories?.Select(c => c.Id),
                    Does.Contain(SharedReagentGeneratorSystem.GeneratedMixingCategory));
                Assert.That(easyRecipe.MixingCategories, Is.Null.Or.Empty);
                Assert.That(hardIgnition.MixingCategories, Is.Null.Or.Empty, "heat alone must ignite a Hard chemical");
            });
        });
    }

    [Test]
    public async Task HeatingAFuelSetsTheFloorOnFire()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var solutions = SEntMan.System<SharedSolutionContainerSystem>();
            var fuel = Generate(2, 30, ("Fueling", 2));
            // Its own reactants: sharing the fuel's recipe would let the fuel reaction take them in the flask.
            var hot = NewOffer(2, 31);
            hot.Effects["Hyperthermic"] = 2;
            hot.Recipe["RMCGold"] = (1, false);
            hot.Recipe["RMCUranium"] = (1, false);
            hot.Recipe["CMUTearGas"] = (1, false);
            SEntMan.EventBus.RaiseEvent(EventSource.Local, new GenerateReagentEvent(hot));

            // Room temperature: the fuel just sits there.
            var beaker = SEntMan.SpawnEntity("Beaker", map.GridCoords);
            Assert.That(solutions.TryGetSolution(beaker, "beaker", out var soln, out var solution), Is.True);
            solutions.TryAddReagent(soln!.Value, fuel.ID, FixedPoint2.New(5));
            Assert.Multiple(() =>
            {
                Assert.That(solution!.GetTotalPrototypeQuantity(fuel.ID), Is.EqualTo(FixedPoint2.New(5)));
                Assert.That(SEntMan.QueryEntities<TileFireComponent>().Any(), Is.False);
            });

            // Hotplate temperature: it burns away and lights a circle of floor around the beaker, √5 ≈ 2 tiles across.
            var maps = Server.System<SharedMapSystem>();
            for (var x = -3; x <= 3; x++)
                for (var y = -3; y <= 3; y++)
                    maps.SetTile(map.Grid, new Vector2i(x, y), map.Tile.Tile);
            var fuelSolution = new Solution();
            fuelSolution.AddReagent(fuel.ID, FixedPoint2.New(5));
            Assert.That(SEntMan.System<CMUFuelProfileSystem>().TryResolveFire(fuelSolution, CMUFireSource.Container, out var expected), Is.True);

            solutions.SetTemperature(soln.Value, SharedReagentGeneratorSystem.IgnitionTemperature(2) + 10f);
            var fires = SEntMan.QueryEntities<TileFireComponent>().ToArray();
            Assert.Multiple(() =>
            {
                Assert.That(solution!.GetTotalPrototypeQuantity(fuel.ID), Is.EqualTo(FixedPoint2.Zero), "the fuel did not ignite");
                Assert.That(fires, Is.Not.Empty, "igniting a fuel must spawn tile fire");
                Assert.That(fires, Has.Length.EqualTo(21), "radius 2 circle (dx² + dy² ≤ 6) on open floor");
                foreach (var tileFire in fires)
                {
                    Assert.That(SComp<RMCIgniteOnCollideComponent>(tileFire.Owner).Intensity, Is.EqualTo(expected.Intensity));
                    Assert.That(SComp<CMUFireTierComponent>(tileFire.Owner).Tier, Is.EqualTo(CMUFuelProfileSystem.TierFor(expected.Intensity)));
                }
            });
            foreach (var tileFire in fires)
                SEntMan.DeleteEntity(tileFire.Owner);

            // An exothermic synthesis warms the beaker but stays below every ignition point.
            var flask = SEntMan.SpawnEntity("Beaker", map.GridCoords);
            Assert.That(solutions.TryGetSolution(flask, "beaker", out var flaskSoln, out var flaskSolution), Is.True);
            var start = flaskSolution!.Temperature;
            foreach (var reagent in hot.Recipe.Keys)
                solutions.TryAddReagent(flaskSoln!.Value, reagent, FixedPoint2.New(10));
            Assert.Multiple(() =>
            {
                Assert.That(flaskSolution.GetTotalPrototypeQuantity(hot.ID), Is.GreaterThan(FixedPoint2.Zero), "synthesis did not run");
                Assert.That(flaskSolution.Temperature, Is.GreaterThan(start));
                Assert.That(flaskSolution.Temperature, Is.LessThanOrEqualTo(SharedReagentGeneratorSystem.SynthesisMaxTemperature));
            });
        });
    }
}
