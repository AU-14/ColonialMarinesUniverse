using System.Linq;
using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Chemistry.Reagents;
using Content.Server.CMU14.Chemistry.Research;
using Content.Server.Explosion.EntitySystems;
using Content.Shared._RMC14.Atmos;
using Content.Shared.CMU14.Chemistry.Fuel;
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

    private EntityUid Detonate(Robust.UnitTesting.Pool.TestMapData map, (string Reagent, int Amount)[] a, (string Reagent, int Amount)[] b)
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
        return payload;
    }

    // N4 (FLOW-01): a generated explosive in a chem grenade blows up whether it was loaded pre-made or only forms when the
    // beakers merge, and is used up; the same chemical on a bench never explodes below its ignition point.
    [Test]
    public async Task ChemGrenadeDetonatesGeneratedExplosive()
    {
        // One map per case: queued explosions of one type on the same map combine into one entry.
        var premadeMap = await Pair.CreateTestMap();
        var mergeMap = await Pair.CreateTestMap();
        var benchMap = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            const int level = 2;
            var explosive = Generate("CMUIgnitionHeatTest-Explosive", 2, effects: ("Explosive", level));
            Assert.That(Server.System<CMUFuelProfileSystem>().IsFuel(explosive.ID), Is.False,
                "a plain explosive should not be a fuel, or the fire step would be what removes it");
            SharedReagentGeneratorSystem.ComputeExplosion(level, out var slope, out _, out var total, out var perUnit);

            // (a) Pre-made: 10u is past the 5u cap.
            Assert.That(QueuedOn(premadeMap), Is.Empty);
            var premade = Detonate(premadeMap, [(explosive.ID, 5)], [(explosive.ID, 5)]);
            var blasts = QueuedOn(premadeMap);
            Assert.That(blasts, Has.Length.EqualTo(1), "a grenade loaded with a generated explosive did not explode");
            Assert.Multiple(() =>
            {
                Assert.That(blasts[0].Cause, Is.EqualTo(premade), "the blast should come from the payload, not a reaction");
                Assert.That(blasts[0].TotalIntensity, Is.EqualTo(MathF.Min(perUnit * 10, total)).Within(0.1f));
                Assert.That(blasts[0].Slope, Is.EqualTo(slope).Within(0.01f));
                Assert.That(PayloadLeft(premade, explosive.ID), Is.EqualTo(FixedPoint2.Zero), "the blast should use the explosive up");
                Assert.That(FiresOn(premadeMap), Is.False, "a plain explosive should not also burn");
            });

            // (b) The explosive only forms when the beakers merge: 3u, under the cap.
            var merged = Detonate(mergeMap, [(Ingredients[0], 3), (Ingredients[1], 3)], [(Ingredients[2], 3)]);
            blasts = QueuedOn(mergeMap);
            Assert.That(blasts, Has.Length.EqualTo(1), "a grenade that synthesizes a generated explosive on detonation did not explode");
            Assert.Multiple(() =>
            {
                Assert.That(blasts[0].Cause, Is.EqualTo(merged));
                Assert.That(blasts[0].TotalIntensity, Is.EqualTo(MathF.Min(perUnit * 3, total)).Within(0.1f));
            });

            // (d) On a bench it waits for its ignition point: not at room temperature, not at the 400 K synthesis cap.
            var solutions = Server.System<SharedSolutionContainerSystem>();
            var (soln, solution) = NewBeaker(benchMap.GridCoords);
            Assert.That(solutions.TryAddReagent(soln, explosive.ID, FixedPoint2.New(5)), Is.True);
            Assert.That(QueuedOn(benchMap), Is.Empty, "a bench beaker exploded at room temperature");
            solutions.SetTemperature(soln, SharedReagentGeneratorSystem.SynthesisMaxTemperature);
            Assert.Multiple(() =>
            {
                Assert.That(QueuedOn(benchMap), Is.Empty, "a bench beaker exploded at the synthesis heat cap");
                Assert.That(solution.GetTotalPrototypeQuantity(explosive.ID), Is.EqualTo(FixedPoint2.New(5)));
            });

            // Probe check: past its ignition point the bench beaker does explode, so the empty results above are real.
            solutions.SetTemperature(soln, SharedReagentGeneratorSystem.IgnitionTemperature(level) + 10f);
            Assert.That(QueuedOn(benchMap), Has.Length.EqualTo(1), "the explosion probe did not see an ignition blast");
        });
    }

    // N4 (FLOW-02): an explosive fuel both blasts and burns; a plain fuel only burns.
    [Test]
    public async Task ChemGrenadeExplosiveFuelBlastsAndBurns()
    {
        var blendMap = await Pair.CreateTestMap();
        var napalmMap = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            Floor(blendMap, 6);
            Floor(napalmMap, 6);

            const int level = 2;
            var blend = Generate("CMUIgnitionHeatTest-ExplosiveFuel", 2, 0f, 0f, ("Explosive", level), ("Fueling", 2));
            Assert.That(Server.System<CMUFuelProfileSystem>().IsFuel(blend.ID), Is.True, "an Explosive + Fueling chemical should be a fuel");
            SharedReagentGeneratorSystem.ComputeExplosion(level, out _, out _, out var total, out var perUnit);

            // (c) The blast leaves the fuel for the fire step, which then burns it.
            var payload = Detonate(blendMap, [(blend.ID, 5)], [(blend.ID, 5)]);
            var blasts = QueuedOn(blendMap);
            Assert.That(blasts, Has.Length.EqualTo(1), "a grenade loaded with an explosive fuel did not explode");
            Assert.Multiple(() =>
            {
                Assert.That(blasts[0].Cause, Is.EqualTo(payload));
                Assert.That(blasts[0].TotalIntensity, Is.EqualTo(MathF.Min(perUnit * 10, total)).Within(0.1f));
                Assert.That(FiresOn(blendMap), Is.True, "an explosive fuel should burn as well as blast");
                Assert.That(PayloadLeft(payload, blend.ID), Is.EqualTo(FixedPoint2.Zero), "the fire should use the fuel up");
            });

            // (e) Napalm only: fire, no blast.
            Detonate(napalmMap, [("RMCNapalmUT", 10)], [("RMCNapalmUT", 10)]);
            Assert.Multiple(() =>
            {
                Assert.That(FiresOn(napalmMap), Is.True, "a napalm grenade should still burn");
                Assert.That(QueuedOn(napalmMap), Is.Empty, "a napalm grenade should not explode");
            });
        });
    }

    // N4 repair: an explosive fuel too diluted to resolve a fire still blasts, and must not stay in the casing to
    // blast again on the next trigger (a ModularGrenade can be re-triggered by its own blast damage).
    [Test]
    public async Task DilutedExplosiveFuelDudDoesNotBlastTwice()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            Floor(map, 6);
            var blend = Generate("CMUIgnitionHeatTest-DudFuel", 2, 0f, 0f, ("Explosive", 2), ("Fueling", 2));

            // 1u of fuel in 120u: the fire spec's intensity rounds to 0, so no fire forms
            var payload = Detonate(map, [(blend.ID, 1), ("Water", 59)], [("Water", 60)]);
            Assert.Multiple(() =>
            {
                Assert.That(QueuedOn(map), Has.Length.EqualTo(1), "the diluted explosive should still blast");
                Assert.That(FiresOn(map), Is.False, "the diluted fuel should not form a fire");
                Assert.That(PayloadLeft(payload, blend.ID), Is.EqualTo(FixedPoint2.Zero),
                    "an explosive fuel that formed no fire stayed in the casing and would blast again");
            });
        });
    }

    /// <summary>
    /// Explosions queued on <paramref name="map"/> and not yet processed. No ticks run inside one WaitAssertion, so a
    /// blast queued there is still in the queue.
    /// </summary>
    private QueuedExplosion[] QueuedOn(Robust.UnitTesting.Pool.TestMapData map)
    {
        var field = typeof(ExplosionSystem).GetField("_queuedExplosions", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, "ExplosionSystem._queuedExplosions is gone; update this probe");
        var queued = (HashSet<QueuedExplosion>) field!.GetValue(Server.System<ExplosionSystem>())!;
        return queued.Where(q => q.Epicenter.MapId == map.MapId).ToArray();
    }

    private bool FiresOn(Robust.UnitTesting.Pool.TestMapData map)
        => SEntMan.QueryEntities<TileFireComponent>()
            .Any(fire => SEntMan.GetComponent<TransformComponent>(fire.Owner).MapID == map.MapId);

    private FixedPoint2 PayloadLeft(EntityUid payload, string reagent)
    {
        var solutions = Server.System<SharedSolutionContainerSystem>();
        var payloadComp = SComp<ChemicalPayloadComponent>(payload);
        var left = FixedPoint2.Zero;
        foreach (var slot in new[] { payloadComp.BeakerSlotA, payloadComp.BeakerSlotB })
        {
            if (slot.Item is { } beaker && solutions.TryGetSolution(beaker, "beaker", out _, out var solution))
                left += solution.GetTotalPrototypeQuantity(reagent);
        }
        return left;
    }

    private void Floor(Robust.UnitTesting.Pool.TestMapData map, int radius)
    {
        var maps = Server.System<SharedMapSystem>();
        for (var x = -radius; x <= radius; x++)
            for (var y = -radius; y <= radius; y++)
                maps.SetTile(map.Grid, new Vector2i(x, y), map.Tile.Tile);
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
