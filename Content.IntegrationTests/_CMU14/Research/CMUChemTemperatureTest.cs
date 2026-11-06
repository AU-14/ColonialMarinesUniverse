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
/// Generated fuel ignition and synthesis temperature through real solution reactions.
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
