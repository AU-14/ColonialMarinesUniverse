#pragma warning disable RA0002 // Fire components are read and primed directly to exercise the fire paths.

using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Atmos;
using Content.Server.CMU14.Chemistry;
using Content.Shared._RMC14.Atmos;
using Content.Shared._RMC14.Explosion;
using Content.Shared._RMC14.Weapons.Ranged.Flamer;
using Content.Shared.CCVar;
using Content.Shared.CMU14.Atmos;
using Content.Shared.CMU14.Chemistry.Fuel;
using Content.Shared.CMU14.Chemistry.Reagents;
using Content.Shared.CMU14.Weapons;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.FixedPoint;
using Content.Shared.Payload.Components;
using Content.Shared.Trigger.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._CMU14.Research;

/// <summary>
/// FLOW-22: one fire spec for every ignition path. The same fuel burns with the same intensity, duration and colour
/// tier from a flamer, a heated container, a chem grenade and a molotov; blue (40+) stays specialist-only; containers,
/// grenades and molotovs fill a circle while the flamer projects a cone; stock napalm ignites when heated; lidded
/// bottles of anything that burns make molotovs that carry it; Flowing fuels creep and Viscous ones never do.
/// </summary>
[TestFixture]
public sealed class CMUFireConsistencyTest : GameTest
{
    // Generated prototypes survive entity cleanup and would change later tests' reactions.
    public override PoolSettings PoolSettings => new() { Connected = false, Destructive = true };

    private static readonly string[] StockFuels =
    [
        "RMCNapalm", "RMCNapalmSticky", "RMCNapalmHighCombustion", "RMCNapalmUT", "RMCNapalmUTTank", "RMCBGel",
        "RMCNapalmX", "RMCNapalmB", "RMCNapalmE", "RMCNapalmEX", "RMCR189",
    ];

    private static int CircleTiles(int radius)
    {
        var count = 0;
        for (var x = -radius; x <= radius; x++)
        {
            for (var y = -radius; y <= radius; y++)
            {
                if (x * x + y * y <= radius * radius + radius)
                    count++;
            }
        }

        return count;
    }

    private static Solution Pure(string reagent, int units)
    {
        var solution = new Solution();
        solution.AddReagent(reagent, FixedPoint2.New(units));
        return solution;
    }

    private GeneratedReagentData Generate(int index, params (string Property, int Level)[] effects)
    {
        var data = new GeneratedReagentData
        {
            ID = $"CMUFireConsistencyTest-{index}",
            Name = $"fire consistency {index}",
            GenTier = 2,
            Class = ReagentClass.Ultra,
            Effects = [],
            Recipe = [],
        };
        foreach (var (property, level) in effects)
            data.Effects[property] = level;
        data.Recipe["RMCGold"] = (1, false);
        data.Recipe["RMCPlatinum"] = (1, false);
        data.Recipe["CMUTearGas"] = (1, false);
        SEntMan.EventBus.RaiseEvent(EventSource.Local, new GenerateReagentEvent(data));
        return data;
    }

    private void Floor(Robust.UnitTesting.Pool.TestMapData map, int radius)
    {
        var maps = Server.System<SharedMapSystem>();
        for (var x = -radius; x <= radius; x++)
        {
            for (var y = -radius; y <= radius; y++)
                maps.SetTile(map.Grid, new Vector2i(x, y), map.Tile.Tile);
        }
    }

    private Entity<TileFireComponent>[] Fires() => SEntMan.QueryEntities<TileFireComponent>().ToArray();

    private void ClearFires()
    {
        foreach (var fire in Fires())
            SEntMan.DeleteEntity(fire.Owner);
    }

    private Entity<RMCFlamerTankComponent> Tank(string id, EntityCoordinates coords, string? reload = null, int units = 100)
    {
        var tank = SEntMan.SpawnEntity(id, coords);
        var comp = SComp<RMCFlamerTankComponent>(tank);
        if (reload != null)
        {
            var solutions = SEntMan.System<SharedSolutionContainerSystem>();
            Assert.That(solutions.TryGetSolution(tank, comp.SolutionId, out var soln, out _), Is.True);
            solutions.RemoveAllSolution(soln!.Value);
            solutions.TryAddReagent(soln.Value, reload, FixedPoint2.New(units));
        }

        return (tank, comp);
    }

    private void AssertTiered(Entity<TileFireComponent> fire, int intensity)
    {
        var tier = SComp<CMUFireTierComponent>(fire);
        Assert.That(SComp<RMCIgniteOnCollideComponent>(fire).Intensity, Is.EqualTo(intensity), "fire intensity");
        Assert.That(tier.Tier, Is.EqualTo(CMUFuelProfileSystem.TierFor(intensity)), "colour tier follows intensity");
        Assert.That(tier.Color, Is.EqualTo(CMUFuelProfileSystem.TierColor(tier.Tier)));
    }

    /// <summary>
    /// The same fuel burns at the same intensity and tier from every non-specialist source. A tank caps its duration;
    /// chemical fires (containers, grenades, molotovs) are sized by their contents and keep the fuel's own duration.
    /// </summary>
    [Test]
    public async Task FireSpecParityAcrossSources()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var fuel = SEntMan.System<CMUFuelProfileSystem>();
            var flamer = SEntMan.System<SharedRMCFlamerSystem>();
            foreach (var reagent in new[] { "RMCNapalmUT", "RMCNapalmX", "RMCNapalm" })
            {
                var tank = Tank("RMCTankFlamer", map.GridCoords, reagent);
                Assert.That(flamer.TryGetTankFireSpec(tank, out var fromFlamer), Is.True, reagent);
                Assert.That(fuel.TryResolveFire(Pure(reagent, 100), CMUFireSource.Container, out var fromContainer), Is.True, reagent);
                Assert.That(fuel.TryResolveFire(Pure(reagent, 100), CMUFireSource.Explosion, out var fromGrenade), Is.True, reagent);
                Assert.That(fuel.TryResolveFire(Pure(reagent, 100), CMUFireSource.Molotov, out var fromMolotov), Is.True, reagent);
                Assert.Multiple(() =>
                {
                    foreach (var other in new[] { fromContainer, fromGrenade, fromMolotov })
                    {
                        Assert.That(other.Intensity, Is.EqualTo(fromFlamer.Intensity), $"{reagent} intensity");
                        Assert.That(other.Tier, Is.EqualTo(fromFlamer.Tier), $"{reagent} tier");
                        Assert.That(other.Duration, Is.EqualTo(SProtoMan.Index<ReagentPrototype>(reagent).Duration), $"{reagent} keeps its own duration");
                        Assert.That(other.Duration, Is.GreaterThanOrEqualTo(fromFlamer.Duration), $"{reagent} duration is only capped by tanks");
                    }
                });
                SEntMan.DeleteEntity(tank);
            }

            // Napalm UT burns at its own stats everywhere: 30 intensity, 20 duration, orange.
            Assert.That(fuel.TryResolveFire(Pure("RMCNapalmUT", 100), CMUFireSource.Container, out var ut), Is.True);
            Assert.That((ut.Intensity, ut.Duration, ut.Tier), Is.EqualTo((30, 20, 2)));
        });
    }

    /// <summary>Blue (intensity 40+) is specialist-only; every other source caps at 39.</summary>
    [Test]
    public async Task NonSpecialistSourcesNeverReachBlueTier()
    {
        Assert.Multiple(() =>
        {
            Assert.That(CMUFuelProfileSystem.TierFor(19), Is.EqualTo(1));
            Assert.That(CMUFuelProfileSystem.TierFor(20), Is.EqualTo(2));
            Assert.That(CMUFuelProfileSystem.TierFor(34), Is.EqualTo(2));
            Assert.That(CMUFuelProfileSystem.TierFor(35), Is.EqualTo(3));
            Assert.That(CMUFuelProfileSystem.TierFor(39), Is.EqualTo(3));
            Assert.That(CMUFuelProfileSystem.TierFor(40), Is.EqualTo(4));
            Assert.That(CMUFuelProfileSystem.TierFor(59), Is.EqualTo(4));
            Assert.That(CMUFuelProfileSystem.TierFor(60), Is.EqualTo(5));
            Assert.That(CMUFuelProfileSystem.TierMaxStacks(3), Is.EqualTo(20));
            Assert.That(CMUFuelProfileSystem.TierMaxStacks(4), Is.EqualTo(40));
            Assert.That(CMUFuelProfileSystem.TierMaxStacks(5), Is.EqualTo(70));
            Assert.That(CMUFuelProfileSystem.TierMaxStacks(5, 10), Is.EqualTo(10), "green gel keeps its own cap");
        });

        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var fuel = SEntMan.System<CMUFuelProfileSystem>();
            var flamer = SEntMan.System<SharedRMCFlamerSystem>();
            var oxidizer = Generate(1, ("Oxidizing", 6));
            var reagents = StockFuels.Append(oxidizer.ID).ToArray();
            Assert.Multiple(() =>
            {
                foreach (var reagent in reagents)
                {
                    foreach (var source in new[] { CMUFireSource.Container, CMUFireSource.Explosion, CMUFireSource.Molotov })
                    {
                        Assert.That(fuel.TryResolveFire(Pure(reagent, 100), source, out var spec), Is.True, $"{reagent} {source}");
                        Assert.That(spec.Intensity, Is.LessThanOrEqualTo(CMUFuelProfileSystem.NonSpecialistMaxIntensity), $"{reagent} {source}");
                        Assert.That(spec.Tier, Is.LessThan(4), $"{reagent} {source} must not burn blue");
                        Assert.That(fuel.IsBlueFire(spec.BehaviourEntity), Is.False, $"{reagent} {source} must not spawn the blue entity");
                    }

                    foreach (var tankId in new[] { "RMCTankFlamer", "RMCTankFlamerUpgraded" })
                    {
                        var tank = Tank(tankId, map.GridCoords, reagent);
                        Assert.That(flamer.TryGetTankFireSpec(tank, out var spec), Is.True, $"{reagent} in {tankId}");
                        Assert.That(spec.Tier, Is.LessThan(4), $"{reagent} in the non-specialist {tankId} must not burn blue");
                        SEntMan.DeleteEntity(tank);
                    }
                }
            });

            // Hot fuels still burn blue from specialist tanks.
            var specialist = Tank("RMCTankFlamerFL3X", map.GridCoords);
            Assert.That(flamer.TryGetTankFireSpec(specialist, out var blue), Is.True);
            Assert.That(blue.Tier, Is.EqualTo(4), "Napalm X from the FL3 is blue");
            var hot = Tank("RMCTankFlamerFL3X", map.GridCoords, oxidizer.ID, 300);
            Assert.That(flamer.TryGetTankFireSpec(hot, out var hotSpec), Is.True);
            Assert.That(hotSpec.Intensity, Is.GreaterThanOrEqualTo(CMUFuelProfileSystem.BlueTierIntensity),
                "a strong oxidizer may exceed 39 in a specialist tank");
        });
    }

    /// <summary>A beaker of Napalm UT is inert at room temperature and fills a tiered circle of fire at 510 K.</summary>
    [Test]
    public async Task StockNapalmIgnitesWhenHeated()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                foreach (var reagent in StockFuels)
                {
                    Assert.That(SProtoMan.TryIndex<ReactionPrototype>(reagent + "Ignition", out var ignition), Is.True, $"{reagent} has no ignition");
                    Assert.That(ignition!.MinimumTemperature, Is.EqualTo(500f));
                    Assert.That(ignition.ConserveEnergy, Is.False);
                    Assert.That(ignition.Priority, Is.LessThan(SharedReagentGeneratorSystem.GeneratedRecipePriority),
                        "ignition must never take reactants from a synthesis or mixing reaction");
                }
            });

            Floor(map, 4);
            var solutions = SEntMan.System<SharedSolutionContainerSystem>();
            var beaker = SEntMan.SpawnEntity("Beaker", map.GridCoords);
            Assert.That(solutions.TryGetSolution(beaker, "beaker", out var soln, out var solution), Is.True);
            solutions.TryAddReagent(soln!.Value, "RMCNapalmUT", FixedPoint2.New(9));
            Assert.Multiple(() =>
            {
                Assert.That(solution!.GetTotalPrototypeQuantity("RMCNapalmUT"), Is.EqualTo(FixedPoint2.New(9)), "napalm must not ignite at room temperature");
                Assert.That(Fires(), Is.Empty);
            });

            solutions.SetTemperature(soln.Value, 510f);
            var fires = Fires();
            Assert.Multiple(() =>
            {
                Assert.That(solution!.GetTotalPrototypeQuantity("RMCNapalmUT"), Is.EqualTo(FixedPoint2.Zero), "the napalm burnt");
                Assert.That(fires, Has.Length.EqualTo(CircleTiles(3)), "9u is a radius 3 circle");
                foreach (var fire in fires)
                    AssertTiered(fire, 30);
            });
            ClearFires();
        });
    }

    /// <summary>A chem grenade with napalm in its beakers bursts into a circle of fire.</summary>
    [Test]
    public async Task ChemGrenadeWithNapalmSpawnsCircle()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            Floor(map, 6);
            var solutions = SEntMan.System<SharedSolutionContainerSystem>();
            var slots = SEntMan.System<ItemSlotsSystem>();
            var payload = SEntMan.SpawnEntity("ChemicalPayload", map.GridCoords);
            var payloadComp = SComp<ChemicalPayloadComponent>(payload);
            var beakers = new List<EntityUid>();
            foreach (var slot in new[] { payloadComp.BeakerSlotA, payloadComp.BeakerSlotB })
            {
                var beaker = SEntMan.SpawnEntity("CMBeaker", map.GridCoords);
                Assert.That(solutions.TryGetSolution(beaker, "beaker", out var soln, out _), Is.True);
                solutions.TryAddReagent(soln!.Value, "RMCNapalmUT", FixedPoint2.New(10));
                Assert.That(slots.TryInsert(payload, slot, beaker, null), Is.True, "the beaker should load into the payload");
                beakers.Add(beaker);
            }

            SEntMan.System<TriggerSystem>().Trigger(payload);
            var fires = Fires();
            Assert.Multiple(() =>
            {
                Assert.That(fires, Has.Length.EqualTo(CircleTiles(CMUFuelProfileSystem.CircleRadius(FixedPoint2.New(20), 5))),
                    "20u bursts into a √20 ≈ 4 tile circle");
                foreach (var fire in fires)
                    AssertTiered(fire, 30);
                foreach (var beaker in beakers)
                {
                    Assert.That(solutions.TryGetSolution(beaker, "beaker", out _, out var left), Is.True);
                    Assert.That(left!.GetTotalPrototypeQuantity("RMCNapalmUT"), Is.EqualTo(FixedPoint2.Zero), "the burst used the napalm up");
                }
            });
            ClearFires();

            // Nothing that burns: no fire.
            var dud = SEntMan.SpawnEntity("ChemicalPayload", map.GridCoords);
            var dudComp = SComp<ChemicalPayloadComponent>(dud);
            foreach (var slot in new[] { dudComp.BeakerSlotA, dudComp.BeakerSlotB })
            {
                var beaker = SEntMan.SpawnEntity("CMBeaker", map.GridCoords);
                Assert.That(solutions.TryGetSolution(beaker, "beaker", out var soln, out _), Is.True);
                solutions.TryAddReagent(soln!.Value, "Water", FixedPoint2.New(10));
                Assert.That(slots.TryInsert(dud, slot, beaker, null), Is.True);
            }

            Assert.That(SEntMan.System<CMUPayloadFireSystem>().TryIgnite((dud, dudComp)), Is.False);
            Assert.That(Fires(), Is.Empty);
        });
    }

    /// <summary>
    /// The flamer projects an arrow along its whole reach. Reach and width both depend on the tank: an LC9 with UT
    /// reaches 3 rows (1 -> 2 -> 3), with B-gel 4 rows up to 3 wide, while a specialist B-gel tank reaches its full range
    /// and widens 2 -> 3 -> 3 -> 4 -> 5.
    /// </summary>
    [Test]
    public async Task FlamerConeWidensWithDistance()
    {
        Assert.Multiple(() =>
        {
            Assert.That(CMUFuelProfileSystem.NonSpecialistReach(5), Is.EqualTo(3), "LC9 with UT");
            Assert.That(CMUFuelProfileSystem.NonSpecialistReach(7), Is.EqualTo(4), "LC9 with B-gel");
            Assert.That(CMUFuelProfileSystem.NonSpecialistReach(1), Is.EqualTo(1));
            Assert.That(CMUFlameConeSystem.RowWidth(5, true, true), Is.EqualTo(5));
            Assert.That(CMUFlameConeSystem.RowWidth(5, true, false), Is.EqualTo(3));
            Assert.That(CMUFlameConeSystem.RowWidth(1, false, false), Is.EqualTo(1));
        });

        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var flamer = SEntMan.System<SharedRMCFlamerSystem>();
            var containers = SEntMan.System<SharedContainerSystem>();
            var transform = SEntMan.System<SharedTransformSystem>();
            var origin = map.GridCoords;
            var originTile = transform.ToMapCoordinates(origin.SnapToGrid(SEntMan)).Position;

            List<int> Rows(string tankId)
            {
                var gun = SEntMan.SpawnEntity("RMCWeaponFlamerUnloaded", origin);
                var provider = SComp<RMCFlamerAmmoProviderComponent>(gun);
                var tank = SEntMan.SpawnEntity(tankId, origin);
                Assert.That(containers.Insert(tank, containers.GetContainer(gun, provider.ContainerId)), Is.True);
                Assert.That(flamer.TryGetPreviewTiles((gun, provider), origin, origin.Offset(new Vector2(8, 0)), out var tiles), Is.True);

                var widths = new SortedDictionary<int, int>();
                foreach (var tile in tiles!)
                {
                    var dx = (int) MathF.Floor(tile.Coordinates.Position.X) - (int) MathF.Floor(originTile.X);
                    widths[dx] = widths.GetValueOrDefault(dx) + 1;
                }

                SEntMan.DeleteEntity(gun);
                return widths.Values.ToList();
            }

            Assert.Multiple(() =>
            {
                Assert.That(Rows("RMCTankFlamer"), Is.EqualTo(new List<int> { 1, 2, 3 }), "LC9 with UT napthal");
                Assert.That(Rows("RMCTankFlamerBGel"), Is.EqualTo(new List<int> { 2, 3, 3, 3 }), "LC9 with B-gel");
                // The M240-T type-B tank reaches 6: 2 -> 3 -> 3 -> 4 -> 5, then stays 5 wide.
                Assert.That(Rows("RMCTankFlamerLargeB"), Is.EqualTo(new List<int> { 2, 3, 3, 4, 5, 5 }), "specialist B-gel");
            });
        });
    }

    /// <summary>
    /// Paper on a lidded bottle of napalm makes a molotov that carries the napalm and shatters into a tiered circle;
    /// a bottle of water and a lidless beaker are refused.
    /// </summary>
    [Test]
    public async Task LiddedBottlesOfFuelMakeMolotovs()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            Floor(map, 6);
            var flammable = SEntMan.System<SharedRMCFlammableSystem>();
            var solutions = SEntMan.System<SharedSolutionContainerSystem>();

            EntityUid Bottle(string id, string solutionId, string reagent)
            {
                var bottle = SEntMan.SpawnEntity(id, map.GridCoords);
                Assert.That(solutions.TryGetSolution(bottle, solutionId, out var soln, out _), Is.True, $"{id} has no {solutionId} solution");
                solutions.TryAddReagent(soln!.Value, reagent, FixedPoint2.New(30));
                return bottle;
            }

            // Lidded bottle of napalm: becomes a molotov that holds the napalm.
            var napalm = Bottle("CMBottleEmpty", "drink", "RMCNapalmUT");
            Assert.That(flammable.CMUIsMolotovBottle(napalm), Is.True, "a lidded bottle is a molotov bottle");
            var paper = SEntMan.SpawnEntity("Paper", map.GridCoords);
            Assert.That(flammable.CMUTryCraftMolotov(napalm, paper, null, out var molotov), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.Deleted(napalm), Is.True, "the bottle became the molotov");
                Assert.That(SEntMan.Deleted(paper), Is.True, "the paper became the rag");
                Assert.That(solutions.TryGetSolution(molotov!.Value, SharedRMCFlammableSystem.CMUMolotovSolution, out _, out var carried), Is.True);
                Assert.That(carried!.GetTotalPrototypeQuantity("RMCNapalmUT"), Is.EqualTo(FixedPoint2.New(30)), "the molotov contains the napalm");
            });

            // Shattering burns the contents in a circle with the napalm's own stats.
            var ev = new RMCTriggerEvent(null, false);
            SEntMan.EventBus.RaiseLocalEvent(molotov!.Value, ref ev);
            var fires = Fires();
            Assert.Multiple(() =>
            {
                Assert.That(fires, Has.Length.EqualTo(CircleTiles(CMUFuelProfileSystem.CircleRadius(FixedPoint2.New(30), 5))));
                foreach (var fire in fires)
                    AssertTiered(fire, 30);
            });
            ClearFires();

            // Water does not burn.
            var water = Bottle("CMBottleEmpty", "drink", "Water");
            var paper2 = SEntMan.SpawnEntity("Paper", map.GridCoords);
            Assert.Multiple(() =>
            {
                Assert.That(flammable.CMUCanCraftMolotov(water, null, false, out _, out _), Is.False);
                Assert.That(flammable.CMUTryCraftMolotov(water, paper2, null, out _), Is.False, "water must be refused");
                Assert.That(SEntMan.Deleted(water), Is.False);
                Assert.That(SEntMan.Deleted(paper2), Is.False);
            });

            // No lid: refused even with napalm.
            var lidless = Bottle("Beaker", "beaker", "RMCNapalmUT");
            Assert.Multiple(() =>
            {
                Assert.That(flammable.CMUIsMolotovBottle(lidless), Is.False, "a lidless beaker is not a bottle");
                Assert.That(flammable.CMUTryCraftMolotov(lidless, paper2, null, out _), Is.False);
                Assert.That(SEntMan.Deleted(lidless), Is.False);
            });
        });
    }

    /// <summary>
    /// Flowing fuels creep even where the map disables spread, Viscous fuels never creep even where it is enabled,
    /// and plain fuels follow the map default.
    /// </summary>
    [Test]
    public async Task FlowingSpreadsAndViscousSuppressesSpread()
    {
        Assert.Multiple(() =>
        {
            Assert.That(CMUFuelProfileSystem.SpreadDepthFor(0, 0), Is.Zero);
            Assert.That(CMUFuelProfileSystem.SpreadDepthFor(1, 0), Is.EqualTo(2));
            Assert.That(CMUFuelProfileSystem.SpreadDepthFor(2, 0), Is.EqualTo(3));
            Assert.That(CMUFuelProfileSystem.SpreadDepthFor(9, 0), Is.EqualTo(CMUFuelProfileSystem.MaxSpreadDepth));
            Assert.That(CMUFuelProfileSystem.SpreadDepthFor(2, 2), Is.EqualTo(CMUFireSpec.NoSpread));
            Assert.That(CMUFuelProfileSystem.SpreadDepthFor(0, 1), Is.EqualTo(CMUFireSpec.NoSpread));
            Assert.That(CMUFuelProfileSystem.SpreadDepthFor(3, 1), Is.EqualTo(4));
        });

        var map = await Pair.CreateTestMap();
        await OverrideCVar(Side.Server, CCVars.CMUFireSpreadDepth, 0);
        CMUFireSpec flowingSpec = default, viscousSpec = default, plainSpec = default;
        await Server.WaitAssertion(() =>
        {
            Floor(map, 3);
            var fuel = SEntMan.System<CMUFuelProfileSystem>();
            var flowing = Generate(2, ("Fueling", 1), ("Flowing", 2));
            var viscous = Generate(3, ("Fueling", 1), ("Viscous", 2));
            Assert.That(fuel.TryResolveFire(Pure(flowing.ID, 50), CMUFireSource.Container, out flowingSpec), Is.True);
            Assert.That(fuel.TryResolveFire(Pure(viscous.ID, 50), CMUFireSource.Container, out viscousSpec), Is.True);
            Assert.That(fuel.TryResolveFire(Pure("RMCNapalmUT", 50), CMUFireSource.Container, out plainSpec), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(flowingSpec.SpreadDepth, Is.EqualTo(3), "Flowing 2 creeps three generations");
                Assert.That(viscousSpec.SpreadDepth, Is.EqualTo(CMUFireSpec.NoSpread));
                Assert.That(plainSpec.SpreadDepth, Is.Zero);
            });

            // Spread disabled on the map: the Flowing fire still creeps.
            var flammable = SEntMan.System<SharedRMCFlammableSystem>();
            var spreading = Server.System<CMUSpreadingFireSystem>();
            flammable.SpawnFireCircle(flowingSpec.BehaviourEntity, map.GridCoords, 0, flowingSpec);
            var fire = Fires().Single();
            Assert.That(SEntMan.HasComponent<CMUAllowFireSpreadComponent>(fire.Owner), Is.True);
            var spread = SComp<CMUSpreadingFireComponent>(fire.Owner);
            Assert.That(spread.Depth, Is.EqualTo(3));
            spread.NextSpread = TimeSpan.Zero;
            spreading.Update(0);
            var fires = Fires();
            Assert.That(fires, Has.Length.EqualTo(2), "the Flowing fire spread to a neighbouring tile");
            var child = fires.Single(f => f.Owner != fire.Owner);
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<CMUAllowFireSpreadComponent>(child.Owner), Is.True, "the child keeps creeping");
                Assert.That(SComp<CMUSpreadingFireComponent>(child.Owner).Depth, Is.EqualTo(2));
                Assert.That(SComp<CMUFireTierComponent>(child.Owner).Tier, Is.EqualTo(flowingSpec.Tier), "the child keeps the colour tier");
            });
            ClearFires();

            // A plain fuel follows the map default (disabled): nothing creeps.
            flammable.SpawnFireCircle(plainSpec.BehaviourEntity, map.GridCoords, 0, plainSpec);
            spreading.Update(0);
            Assert.That(Fires(), Has.Length.EqualTo(1));
            Assert.That(SEntMan.HasComponent<CMUSpreadingFireComponent>(Fires()[0].Owner), Is.False);
            ClearFires();
        });

        // Spread enabled on the map: an AU14 fire from a Viscous fuel never creeps, a plain one does.
        await OverrideCVar(Side.Server, CCVars.CMUFireSpreadDepth, 3);
        await Server.WaitAssertion(() =>
        {
            var flammable = SEntMan.System<SharedRMCFlammableSystem>();
            var spreading = Server.System<CMUSpreadingFireSystem>();

            flammable.SpawnFireCircle("AU14TileFire", map.GridCoords, 0, viscousSpec);
            var thick = Fires().Single();
            Assert.That(SEntMan.HasComponent<CMUAllowFireSpreadComponent>(thick.Owner), Is.False, "Viscous fuel suppresses spread");
            spreading.Update(0);
            Assert.That(SEntMan.HasComponent<CMUSpreadingFireComponent>(thick.Owner), Is.False);
            Assert.That(Fires(), Has.Length.EqualTo(1));
            ClearFires();

            flammable.SpawnFireCircle("AU14TileFire", map.GridCoords, 0, plainSpec);
            var plain = Fires().Single();
            spreading.Update(0);
            Assert.That(SEntMan.HasComponent<CMUSpreadingFireComponent>(plain.Owner), Is.True, "plain fuel follows the map default");
            Assert.That(SComp<CMUSpreadingFireComponent>(plain.Owner).Depth, Is.EqualTo(3));
            ClearFires();
        });
    }
}
