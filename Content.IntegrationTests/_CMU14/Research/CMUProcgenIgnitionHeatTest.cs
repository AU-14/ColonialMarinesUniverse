using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Chemistry.Reagents;
using Content.Server.CMU14.Chemistry.Research;
using Content.Shared._RMC14.Atmos;
using Content.Shared.CMU14.Chemistry.Reagent;
using Content.Shared.CMU14.Chemistry.Reagents;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.FixedPoint;
using Content.Shared.Payload.Components;
using Content.Shared.Trigger.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._CMU14.Research;

/// <summary>
/// GraphAudit-ProcgenIgnitionHeat: generated chemicals must not be made and burnt in one go by a mixture that is
/// already hot (FLOW-01), must not vanish without a trace when they ignite without a fire (FLOW-02), self-heating
/// must not jump a cold solution (FLOW-05), and contracts can require heating or cooling (§5).
/// </summary>
[TestFixture]
public sealed class CMUProcgenIgnitionHeatTest : GameTest
{
    // Generated prototypes survive entity cleanup and would change later tests' reactions.
    public override PoolSettings PoolSettings => new() { Connected = false, Destructive = true };

    private const string Residue = SharedReagentGeneratorSystem.IgnitionResidueReagent;

    /// <summary>Ingredients known not to collide with a hand-authored reaction (see CMUChemTemperatureTest).</summary>
    private static readonly string[] Ingredients = ["RMCGold", "RMCPlatinum", "CMUTearGas"];

    private GeneratedReagentData Generate(string id, int tier, float minTemp = 0f, float maxTemp = 0f,
        params (string Property, int Level)[] effects)
    {
        var data = new GeneratedReagentData
        {
            ID = id,
            Name = id,
            GenTier = tier,
            Class = ReagentClass.Ultra,
            Effects = [],
            Recipe = [],
            RequiredMinTemp = minTemp,
            RequiredMaxTemp = maxTemp,
        };
        foreach (var (property, level) in effects)
            data.Effects[property] = level;
        foreach (var ingredient in Ingredients)
            data.Recipe[ingredient] = (1, false);
        SEntMan.EventBus.RaiseEvent(EventSource.Local, new GenerateReagentEvent(data));
        return data;
    }

    private (Entity<SolutionComponent> Soln, Solution Solution) NewBeaker(EntityCoordinates coords)
    {
        var beaker = SEntMan.SpawnEntity("Beaker", coords);
        Assert.That(Server.System<SharedSolutionContainerSystem>().TryGetSolution(beaker, "beaker", out var soln, out var solution), Is.True);
        return (soln!.Value, solution!);
    }

    private void AddIngredients(Entity<SolutionComponent> soln, int amount)
    {
        var solutions = Server.System<SharedSolutionContainerSystem>();
        foreach (var ingredient in Ingredients)
            Assert.That(solutions.TryAddReagent(soln, ingredient, FixedPoint2.New(amount)), Is.True, ingredient);
    }

    // FLOW-01: a too-hot mixture waits instead of making the fuel and burning it in the same tick.
    [Test]
    public async Task HotMixtureDoesNotMakeAndBurnAFuel()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var solutions = Server.System<SharedSolutionContainerSystem>();
            // Fueling 6 ignites at the 410 K floor, just above the 400 K synthesis cap.
            var fuel = Generate("CMUIgnitionHeatTest-Hot", 2, effects: ("Fueling", 6));
            var ignition = SharedReagentGeneratorSystem.IgnitionTemperature(6);
            var reaction = SProtoMan.Index<ReactionPrototype>(fuel.ID);
            Assert.That(reaction.MaximumTemperature, Is.EqualTo(ignition - SharedReagentGeneratorSystem.IgnitionGuardMargin).Within(0.01f),
                "an ignitable synthesis must stop just under its ignition point");

            var (soln, solution) = NewBeaker(map.GridCoords);
            solutions.SetTemperature(soln, ignition + 40f);
            AddIngredients(soln, 2);
            Assert.Multiple(() =>
            {
                Assert.That(solution.GetTotalPrototypeQuantity(fuel.ID), Is.EqualTo(FixedPoint2.Zero));
                Assert.That(solution.GetTotalPrototypeQuantity(Residue), Is.EqualTo(FixedPoint2.Zero),
                    "the hot mixture made the fuel and burnt it");
                Assert.That(solution.GetTotalPrototypeQuantity(Ingredients[0]), Is.EqualTo(FixedPoint2.New(2)),
                    "the ingredients should wait for the mixture to cool");
            });

            solutions.SetTemperature(soln, 300f);
            Assert.That(solution.GetTotalPrototypeQuantity(fuel.ID), Is.EqualTo(FixedPoint2.New(2)),
                "once cooled the synthesis should run and the fuel survive");
        });
    }

    // FLOW-02: a fuel too diluted to burn leaves residue instead of vanishing; a clean one still burns without residue.
    [Test]
    public async Task DilutedIgnitionLeavesResidue()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var solutions = Server.System<SharedSolutionContainerSystem>();
            var maps = Server.System<SharedMapSystem>();
            for (var x = -3; x <= 3; x++)
                for (var y = -3; y <= 3; y++)
                    maps.SetTile(map.Grid, new Vector2i(x, y), map.Tile.Tile);

            var fuel = Generate("CMUIgnitionHeatTest-Fuel", 2, effects: ("Fueling", 2));
            var hot = SharedReagentGeneratorSystem.IgnitionTemperature(2) + 10f;

            // Brine-cooled beaker: mostly water, a single unit of fuel.
            var (diluted, dilutedSolution) = NewBeaker(map.GridCoords);
            Assert.That(solutions.TryAddReagent(diluted, "Water", FixedPoint2.New(48)), Is.True);
            Assert.That(solutions.TryAddReagent(diluted, fuel.ID, FixedPoint2.New(1)), Is.True);
            solutions.SetTemperature(diluted, hot);
            Assert.Multiple(() =>
            {
                Assert.That(dilutedSolution.GetTotalPrototypeQuantity(fuel.ID), Is.EqualTo(FixedPoint2.Zero), "the fuel did not ignite");
                Assert.That(dilutedSolution.GetTotalPrototypeQuantity(Residue), Is.EqualTo(FixedPoint2.New(1)),
                    "a diluted ignition must leave residue instead of deleting the fuel");
                Assert.That(SEntMan.QueryEntities<TileFireComponent>().Any(), Is.False, "too diluted to burn");
            });

            var (clean, cleanSolution) = NewBeaker(map.GridCoords);
            Assert.That(solutions.TryAddReagent(clean, fuel.ID, FixedPoint2.New(5)), Is.True);
            solutions.SetTemperature(clean, hot);
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.QueryEntities<TileFireComponent>().Any(), Is.True, "a clean fuel should still burn");
                Assert.That(cleanSolution.GetTotalPrototypeQuantity(Residue), Is.EqualTo(FixedPoint2.Zero),
                    "a fuel that burns should not also leave residue");
            });
        });
    }

    // FLOW-02: an oxidizer heated on its own ignites; whether or not the whole solution resolves to a fire, the
    // chemical must not just disappear: either the floor burns or residue is left.
    [Test]
    public async Task OxidizerIgnitionIsNeverSilent()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var solutions = Server.System<SharedSolutionContainerSystem>();
            var maps = Server.System<SharedMapSystem>();
            for (var x = -3; x <= 3; x++)
                for (var y = -3; y <= 3; y++)
                    maps.SetTile(map.Grid, new Vector2i(x, y), map.Tile.Tile);

            var oxidizer = Generate("CMUIgnitionHeatTest-Ox", 2, effects: ("Oxidizing", 2));
            var (soln, solution) = NewBeaker(map.GridCoords);
            Assert.That(solutions.TryAddReagent(soln, oxidizer.ID, FixedPoint2.New(3)), Is.True);
            solutions.SetTemperature(soln, SharedReagentGeneratorSystem.IgnitionTemperature(2) + 10f);

            var burned = SEntMan.QueryEntities<TileFireComponent>().Any();
            var residue = solution.GetTotalPrototypeQuantity(Residue);
            Assert.Multiple(() =>
            {
                Assert.That(solution.GetTotalPrototypeQuantity(oxidizer.ID), Is.EqualTo(FixedPoint2.Zero), "the oxidizer did not ignite");
                Assert.That(burned || residue == FixedPoint2.New(3), Is.True,
                    $"the oxidizer vanished with no fire and {residue}u residue");
                Assert.That(burned && residue > FixedPoint2.Zero, Is.False, "a burning oxidizer should not also leave residue");
            });
        });
    }

    // FLOW-01 guard vs chem grenades: a grenade lights its fuel from the merged solution's fire spec, not from heat, so the
    // synthesis ceiling must not stop a grenade loaded with a generated fuel, nor one that only forms the fuel when the
    // two beakers merge at detonation.
    [Test]
    public async Task ChemGrenadesStillIgniteGeneratedFuel()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            for (var x = -6; x <= 6; x++)
                for (var y = -6; y <= 6; y++)
                    maps.SetTile(map.Grid, new Vector2i(x, y), map.Tile.Tile);

            var fuel = Generate("CMUIgnitionHeatTest-Grenade", 2, effects: ("Fueling", 2));
            Assert.That(float.IsFinite(SProtoMan.Index<ReactionPrototype>(fuel.ID).MaximumTemperature), Is.True,
                "the fuel's synthesis should carry the ignition guard this test is about");

            // Pre-made fuel in both beakers.
            Detonate(map, [(fuel.ID, 5)], [(fuel.ID, 5)]);
            var fires = SEntMan.QueryEntities<TileFireComponent>().ToArray();
            Assert.That(fires, Is.Not.Empty, "a grenade loaded with a generated fuel did not ignite");
            foreach (var fire in fires)
                SEntMan.DeleteEntity(fire.Owner);

            // The fuel only forms when the beakers merge.
            Detonate(map, [(Ingredients[0], 3), (Ingredients[1], 3)], [(Ingredients[2], 3)]);
            Assert.That(SEntMan.QueryEntities<TileFireComponent>().Any(), Is.True,
                "a grenade that synthesizes a generated fuel on detonation did not ignite");
        });
    }

    private void Detonate(Robust.UnitTesting.Pool.TestMapData map, (string Reagent, int Amount)[] a, (string Reagent, int Amount)[] b)
    {
        var solutions = Server.System<SharedSolutionContainerSystem>();
        var slots = Server.System<ItemSlotsSystem>();
        var payload = SEntMan.SpawnEntity("ChemicalPayload", map.GridCoords);
        var payloadComp = SComp<ChemicalPayloadComponent>(payload);
        foreach (var (slot, contents) in new[] { (payloadComp.BeakerSlotA, a), (payloadComp.BeakerSlotB, b) })
        {
            var beaker = SEntMan.SpawnEntity("CMBeaker", map.GridCoords);
            Assert.That(solutions.TryGetSolution(beaker, "beaker", out var soln, out _), Is.True);
            foreach (var (reagent, amount) in contents)
                Assert.That(solutions.TryAddReagent(soln!.Value, reagent, FixedPoint2.New(amount)), Is.True, reagent);
            Assert.That(slots.TryInsert(payload, slot, beaker, null), Is.True, "the beaker should load into the payload");
        }

        SEntMan.System<TriggerSystem>().Trigger(payload);
    }

    // FLOW-05: a self-heating synthesis in a solution below its 250 K floor warms it a little, not straight to 250 K.
    [Test]
    public async Task SelfHeatingDoesNotJumpAColdSolution()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var solutions = Server.System<SharedSolutionContainerSystem>();
            var warm = Generate("CMUIgnitionHeatTest-Warm", 2, effects: ("Hyperthermic", 1));
            var (soln, solution) = NewBeaker(map.GridCoords);
            Assert.That(solutions.TryAddReagent(soln, "Water", FixedPoint2.New(40)), Is.True);
            solutions.SetTemperature(soln, 230f);
            AddIngredients(soln, 1);
            Assert.Multiple(() =>
            {
                Assert.That(solution.GetTotalPrototypeQuantity(warm.ID), Is.EqualTo(FixedPoint2.New(1)), "synthesis did not run");
                Assert.That(solution.Temperature, Is.GreaterThan(230f), "a Hyperthermic synthesis should warm the solution");
                Assert.That(solution.Temperature, Is.LessThan(240f), "the synthesis heat jumped the solution to its floor");
            });
        });
    }

    // §5: a heat-gated contract recipe waits for heat.
    [Test]
    public async Task HeatGatedContractNeedsHeat()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var solutions = Server.System<SharedSolutionContainerSystem>();
            var chem = Generate("CMUIgnitionHeatTest-HeatGate", 2, minTemp: 350f);
            var (soln, solution) = NewBeaker(map.GridCoords);
            AddIngredients(soln, 2);
            Assert.That(solution.GetTotalPrototypeQuantity(chem.ID), Is.EqualTo(FixedPoint2.Zero), "reacted at room temperature");

            solutions.SetTemperature(soln, 345f);
            Assert.That(solution.GetTotalPrototypeQuantity(chem.ID), Is.EqualTo(FixedPoint2.Zero), "reacted below the heat gate");

            solutions.SetTemperature(soln, 355f);
            Assert.That(solution.GetTotalPrototypeQuantity(chem.ID), Is.EqualTo(FixedPoint2.New(2)));
        });
    }

    // §5: a cooling-gated contract recipe waits for cold.
    [Test]
    public async Task CoolingGatedContractNeedsCold()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var solutions = Server.System<SharedSolutionContainerSystem>();
            var chem = Generate("CMUIgnitionHeatTest-ColdGate", 2, maxTemp: 270f);
            var (soln, solution) = NewBeaker(map.GridCoords);
            AddIngredients(soln, 2);
            Assert.That(solution.GetTotalPrototypeQuantity(chem.ID), Is.EqualTo(FixedPoint2.Zero), "reacted at room temperature");

            solutions.SetTemperature(soln, 265f);
            Assert.That(solution.GetTotalPrototypeQuantity(chem.ID), Is.EqualTo(FixedPoint2.New(2)));
        });
    }

    // §5: Easy never gets a gate; Intermediate and Hard sometimes do, within the bands, and never one their own
    // synthesis heat would undo. The contract paper states the gate.
    [Test]
    public async Task TemperatureGateRollsAndContractText()
    {
        await Server.WaitAssertion(() =>
        {
            var generator = Server.System<ServerReagentGeneratorSystem>();
            for (var tier = 1; tier <= 3; tier++)
            {
                foreach (var effects in new[]
                         {
                             new Dictionary<string, int>(),
                             new Dictionary<string, int> { ["Hyperthermic"] = 2 },
                             new Dictionary<string, int> { ["Hypothermic"] = 2 },
                             new Dictionary<string, int> { ["Fueling"] = 3 },
                         })
                {
                    var gated = 0;
                    const int rolls = 400;
                    for (var i = 0; i < rolls; i++)
                    {
                        var data = new GeneratedReagentData { GenTier = tier, Effects = effects };
                        generator.RollTemperatureGate(ref data);
                        Assert.That(data.RequiredMinTemp == 0f || data.RequiredMaxTemp == 0f, "both gates set");
                        if (data.RequiredMinTemp > 0)
                        {
                            gated++;
                            var max = effects.ContainsKey("Fueling")
                                ? SharedReagentGeneratorSystem.HeatGateMaxIgnitable
                                : SharedReagentGeneratorSystem.HeatGateMax;
                            Assert.That(data.RequiredMinTemp, Is.InRange(SharedReagentGeneratorSystem.HeatGateMin, max));
                            Assert.That(effects.ContainsKey("Hypothermic"), Is.False, "a self-cooling synthesis got a heat gate");
                        }
                        if (data.RequiredMaxTemp > 0)
                        {
                            gated++;
                            Assert.That(data.RequiredMaxTemp,
                                Is.InRange(SharedReagentGeneratorSystem.CoolGateMin, SharedReagentGeneratorSystem.CoolGateMax));
                            Assert.That(effects.ContainsKey("Hyperthermic"), Is.False, "a self-heating synthesis got a cooling gate");
                        }
                    }

                    if (tier == 1)
                        Assert.That(gated, Is.Zero, "Easy contracts never get a temperature gate");
                    else
                        Assert.That(gated, Is.InRange(rolls / 20, rolls * 3 / 4), $"tier {tier} gates should be occasional");
                }
            }

            var terminal = Server.System<ServerResearchDataTerminalSystem>();
            var heated = new GeneratedReagentData { ID = "CMUIgnitionHeatTest-Paper", Name = "paper", GenTier = 2, Effects = [], Recipe = [], RequiredMinTemp = 350f };
            var cooled = heated;
            cooled.RequiredMinTemp = 0f;
            cooled.RequiredMaxTemp = 265f;
            Assert.Multiple(() =>
            {
                Assert.That(terminal.FormatContract(heated, false),
                    Does.Contain(Loc.GetString("research-report-contract-heat-required", ("TEMP", 350))));
                Assert.That(terminal.FormatContract(cooled, false),
                    Does.Contain(Loc.GetString("research-report-contract-cooling-required", ("TEMP", 265))));
                // The XRF/simulator report uses the same text, including an ignitable chemical's synthesis ceiling.
                var fuel = new GeneratedReagentData { GenTier = 1, Effects = new() { ["Fueling"] = 6 }, Recipe = [] };
                Assert.That(generator.ProcessRequirementsText(fuel),
                    Does.Contain(Loc.GetString("research-report-contract-ignition-ceiling",
                        ("TEMP", (int) (SharedReagentGeneratorSystem.IgnitionTemperature(6) - SharedReagentGeneratorSystem.IgnitionGuardMargin)))));
            });
        });
    }
}
