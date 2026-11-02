#pragma warning disable RA0002 // Flamer chains and flammable state are read and primed directly to exercise the fire path.

using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._RMC14.Atmos;
using Content.Server.Atmos.EntitySystems;
using Content.Shared._RMC14.Atmos;
using Content.Shared._RMC14.Weapons.Ranged.Flamer;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.CMU14.Chemistry.Fuel;
using Content.Shared.CMU14.Chemistry.Reagents;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using ServerFlammableSystem = Content.Server.Atmos.EntitySystems.FlammableSystem;

namespace Content.IntegrationTests._CMU14.Research;

/// <summary>
/// FLOW-04 napalm rebalance: blue fire is reserved for specialist tanks, generated fuels no longer apply their
/// own property modifiers on top of their per-level stats, and the xeno burn tick no longer grows without bound
/// as a fuel's duration shrinks.
/// </summary>
[TestFixture]
public sealed class CMUNapalmBalanceTest : GameTest
{
    // Generated prototypes survive entity cleanup and would change later tests' reactions.
    public override PoolSettings PoolSettings => new() { Connected = false, Destructive = true };

    private static readonly string[] BlueFireTanks =
    [
        "RMCTankFlamerLarge", "RMCTankFlamerLargeX", "RMCTankFlamerLargeCustom",
        "RMCTankFlamerFL3", "RMCTankFlamerFL3X", "RMCTankFlamerFL3Whiteout",
        "RMCTankFlamerXLargeUPP", "AU14TankFlamerM240A2HAZOPSNapalmX",
    ];

    private static readonly string[] RedFireTanks =
    [
        "RMCTankFlamer", "RMCTankFlamerCustom", "RMCTankFlamerUpgraded", "RMCTankFlamerBGel", "RMCTankFlamerSPP",
    ];

    /// <summary>
    /// A standard M240 tank loaded with Napalm X (a blue-fire fuel) fires the generic fire entity, both in the examine
    /// readout and in the chain it actually shoots, and (FLOW-22) is capped below the blue colour tier at 39 intensity;
    /// the M240-T and FL3 specialist tanks keep their blue fire entity and reach the blue tier.
    /// </summary>
    [Test]
    public async Task BlueFireIsReservedForSpecialistTanks()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                foreach (var id in BlueFireTanks)
                    Assert.That(TankPrototype(id).AllowBlueFire, Is.True, $"{id} is a specialist tank and should allow blue fire");
                foreach (var id in RedFireTanks)
                    Assert.That(TankPrototype(id).AllowBlueFire, Is.False, $"{id} is not a specialist tank");
            });

            var flamer = SEntMan.System<SharedRMCFlamerSystem>();
            var solutions = SEntMan.System<SharedSolutionContainerSystem>();
            var containers = SEntMan.System<SharedContainerSystem>();
            var origin = map.GridCoords;

            (EntProtoId Raw, EntProtoId Effective, EntProtoId Shot, int Intensity, int Tier) Fire(string tankId, string? reload)
            {
                DeleteChains();

                var tank = SEntMan.SpawnEntity(tankId, origin);
                var tankComp = SComp<RMCFlamerTankComponent>(tank);
                if (reload != null)
                {
                    Assert.That(solutions.TryGetSolution(tank, tankComp.SolutionId, out var soln, out var solution), Is.True);
                    solutions.RemoveAllSolution(soln!.Value);
                    solutions.TryAddReagent(soln.Value, reload, solution!.MaxVolume);
                }

                Assert.That(flamer.TryGetEffectiveFuel((tank, tankComp), out var raw, out var effective), Is.True,
                    $"{tankId} loaded with {reload} should report a fuel");
                Assert.That(flamer.TryGetTankFireSpec((tank, tankComp), out var spec), Is.True);
                Assert.That(effective.Intensity, Is.EqualTo(spec.Intensity), "the examine readout must match the fired spec");

                var gun = SEntMan.SpawnEntity("RMCWeaponFlamerUnloaded", origin);
                var provider = SComp<RMCFlamerAmmoProviderComponent>(gun);
                var magazine = containers.GetContainer(gun, provider.ContainerId);
                Assert.That(containers.Insert(tank, magazine), Is.True, $"{tankId} should load into the flamer");

                flamer.ShootFlamer((gun, provider), (gun, SComp<GunComponent>(gun)), null, origin, origin.Offset(new Vector2(-3, 0)));
                var chains = Chains();
                Assert.That(chains, Has.Count.EqualTo(1), $"{tankId} should fire one flamer chain");
                return (raw.FireEntity, effective.FireEntity, chains[0].Comp.Spawn, chains[0].Comp.Intensity, spec.Tier);
            }

            var standard = Fire("RMCTankFlamer", "RMCNapalmX");
            var upgraded = Fire("RMCTankFlamerUpgraded", "RMCNapalmX");
            var specialist = Fire("RMCTankFlamerLargeX", null);
            var fl3 = Fire("RMCTankFlamerFL3X", null);
            Assert.Multiple(() =>
            {
                Assert.That(standard.Raw, Is.EqualTo(SharedRMCFlamerSystem.BlueFire), "Napalm X itself burns blue");
                Assert.That(standard.Effective, Is.EqualTo(new EntProtoId("RMCTileFire")), "the examine readout must show the fallback");
                Assert.That(standard.Shot, Is.EqualTo(new EntProtoId("RMCTileFire")), "a standard M240 tank must fire red fire");
                Assert.That(upgraded.Shot, Is.EqualTo(new EntProtoId("RMCTileFire")), "an upgraded M240 tank must fire red fire");
                Assert.That(specialist.Effective, Is.EqualTo(SharedRMCFlamerSystem.BlueFire));
                Assert.That(specialist.Shot, Is.EqualTo(SharedRMCFlamerSystem.BlueFire), "the M240-T X tank keeps blue fire");
                Assert.That(fl3.Shot, Is.EqualTo(SharedRMCFlamerSystem.BlueFire), "the FL3 X tank keeps blue fire");

                // FLOW-22: blue means intensity 40+, which only specialist tanks may reach.
                Assert.That(standard.Intensity, Is.EqualTo(CMUFuelProfileSystem.NonSpecialistMaxIntensity),
                    "a standard tank caps Napalm X at 39");
                Assert.That(standard.Tier, Is.LessThan(4), "a standard tank must never reach the blue tier");
                Assert.That(upgraded.Intensity, Is.LessThanOrEqualTo(CMUFuelProfileSystem.NonSpecialistMaxIntensity),
                    "the upgraded tank's 60 cap does not make it a specialist tank");
                Assert.That(specialist.Intensity, Is.EqualTo(40));
                Assert.That(specialist.Tier, Is.EqualTo(4), "Napalm X from a specialist tank burns blue");
                Assert.That(fl3.Tier, Is.EqualTo(4));
            });

            DeleteChains();
        });
    }

    /// <summary>
    /// A generated fuel's per-level property bonuses are already in its intensity and duration; its own
    /// intensityMod/durationMod must not rescale a tank holding only that fuel.
    /// </summary>
    [Test]
    public async Task GeneratedFuelModifiersDoNotRescaleTheirOwnTank()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var data = new GeneratedReagentData
            {
                ID = "CMUNapalmBalanceTest-Oxidizer",
                Name = "napalm balance oxidizer",
                GenTier = 2,
                Class = ReagentClass.Ultra,
                Effects = [],
                Recipe = [],
            };
            data.Effects["Oxidizing"] = 2;
            data.Recipe["RMCGold"] = (1, false);
            data.Recipe["RMCPlatinum"] = (1, false);
            data.Recipe["CMUTearGas"] = (1, false);
            SEntMan.EventBus.RaiseEvent(EventSource.Local, new GenerateReagentEvent(data));

            var reagent = SProtoMan.Index<ReagentPrototype>(data.ID);
            Assert.Multiple(() =>
            {
                // base 15/20 with Oxidizing +7/-4 per level
                Assert.That((reagent.Intensity, reagent.Duration), Is.EqualTo((29, 12)));
                Assert.That(reagent.IntensityMod, Is.GreaterThan(FixedPoint2.Zero), "the property still emits its blend modifier");
            });

            var fuel = SEntMan.System<CMUFuelProfileSystem>();
            var pure = new Solution();
            pure.AddReagent(data.ID, FixedPoint2.New(100));
            Assert.That(fuel.TryCompute(pure, out var profile), Is.True);
            Assert.That((profile.Intensity, profile.Duration, profile.Radius),
                Is.EqualTo((reagent.Intensity, reagent.Duration, reagent.Radius)),
                "a pure tank of a generated fuel must burn at its own stats");

            var flamer = SEntMan.System<SharedRMCFlamerSystem>();
            var solutions = SEntMan.System<SharedSolutionContainerSystem>();
            var tank = SEntMan.SpawnEntity("RMCTankFlamer", map.GridCoords);
            var tankComp = SComp<RMCFlamerTankComponent>(tank);
            Assert.That(solutions.TryGetSolution(tank, tankComp.SolutionId, out var soln, out _), Is.True);
            solutions.RemoveAllSolution(soln!.Value);
            solutions.TryAddReagent(soln.Value, data.ID, FixedPoint2.New(100));
            Assert.That(flamer.TryGetEffectiveFuel((tank, tankComp), out var raw, out var effective), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(raw.Intensity, Is.EqualTo(reagent.Intensity));
                Assert.That(effective.Intensity, Is.EqualTo(Math.Min(tankComp.MaxIntensity, reagent.Intensity)));
                Assert.That(effective.Duration, Is.EqualTo(Math.Min(tankComp.MaxDuration, reagent.Duration)));
            });

            // Blending still moves the volume-weighted mean: hotter and shorter than plain UT.
            var blend = new Solution();
            blend.AddReagent("RMCNapalmUT", FixedPoint2.New(50));
            blend.AddReagent(data.ID, FixedPoint2.New(50));
            Assert.That(fuel.TryCompute(blend, out var blendProfile), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(blendProfile.Intensity, Is.EqualTo(30)); // (30 + 29) / 2 = 29.5, rounded to even
                Assert.That(blendProfile.Duration, Is.EqualTo(16)); // (20 + 12) / 2
            });

            // Non-fuel generated additives still shape a blend through their modifiers.
            var flowing = new GeneratedReagentData
            {
                ID = "CMUNapalmBalanceTest-Flowing",
                Name = "napalm balance flowing",
                GenTier = 2,
                Class = ReagentClass.Ultra,
                Effects = [],
                Recipe = [],
            };
            flowing.Effects["Flowing"] = 2;
            flowing.Recipe["RMCSilver"] = (1, false);
            flowing.Recipe["RMCPlatinum"] = (1, false);
            flowing.Recipe["CMUTearGas"] = (1, false);
            SEntMan.EventBus.RaiseEvent(EventSource.Local, new GenerateReagentEvent(flowing));
            var widened = new Solution();
            widened.AddReagent("RMCNapalmUT", FixedPoint2.New(100));
            widened.AddReagent(flowing.ID, FixedPoint2.New(100));
            Assert.That(fuel.TryCompute(widened, out var widenedProfile), Is.True);
            // UT 30 diluted to half by a non-fuel is 15; Flowing's negative intensityMod must still pull it lower.
            Assert.That(widenedProfile.Intensity, Is.LessThan(15), "non-fuel additives keep shaping a blend");
        });
    }

    /// <summary>
    /// The xeno burn tick divides by the fire's duration; below <see cref="ServerFlammableSystem.MinBurnTickDuration"/>
    /// it must stop growing, so a duration-2 fire burns a xeno exactly as hard as a duration-10 one.
    /// </summary>
    [Test]
    public async Task XenoBurnTickFloorsFireDuration()
    {
        Assert.Multiple(() =>
        {
            var floor = ServerFlammableSystem.MinBurnTickDuration;
            Assert.That(floor, Is.EqualTo(10));
            Assert.That(ServerFlammableSystem.XenoBurnTickScale(40, 20, 2),
                Is.EqualTo(ServerFlammableSystem.XenoBurnTickScale(40, 20, floor)).Within(1e-6));
            Assert.That(ServerFlammableSystem.XenoBurnTickScale(40, 20, 2), Is.EqualTo(48).Within(1e-4),
                "40 * (20 / 10 * 0.2 + 0.8)");
            // Stock fuels at or above the floor keep the RMC formula: Napalm UT 30 intensity, 20 duration, 20 stacks.
            Assert.That(ServerFlammableSystem.XenoBurnTickScale(30, 20, 20), Is.EqualTo(30).Within(1e-4));
        });

        var map = await Pair.CreateTestMap();
        EntityUid shortFire = default;
        EntityUid floorFire = default;
        await Server.WaitAssertion(() =>
        {
            SetOxygenAtmosphere(map.MapUid);
            shortFire = SpawnXeno(map.MapId, 0);
            floorFire = SpawnXeno(map.MapId, 2);
            IgniteXeno(shortFire, 40, 2, 10);
            IgniteXeno(floorFire, 40, ServerFlammableSystem.MinBurnTickDuration, 10);
            Server.System<ServerFlammableSystem>().RefreshUpdateSnapshot(); // include both xenos in the next update

            Assert.Multiple(() =>
            {
                Assert.That(SComp<OnFireComponent>(shortFire).Duration, Is.EqualTo(2));
                Assert.That(SComp<FlammableComponent>(shortFire).FireStacks,
                    Is.EqualTo(SComp<FlammableComponent>(floorFire).FireStacks));
            });
        });

        await Pair.RunTicksSync(1);

        await Server.WaitAssertion(() =>
        {
            var damageable = Server.System<DamageableSystem>();
            var shortDamage = damageable.GetTotalDamage(shortFire).Float();
            var floorDamage = damageable.GetTotalDamage(floorFire).Float();
            Assert.Multiple(() =>
            {
                Assert.That(floorDamage, Is.GreaterThan(0), "the xeno should take a burn tick");
                Assert.That(shortDamage, Is.EqualTo(floorDamage).Within(0.02f),
                    "a duration-2 fire must not out-damage a duration-10 fire of the same intensity and stacks");
            });
        });
    }

    private List<Entity<RMCFlamerChainComponent>> Chains()
    {
        var chains = new List<Entity<RMCFlamerChainComponent>>();
        var query = SEntMan.EntityQueryEnumerator<RMCFlamerChainComponent>();
        while (query.MoveNext(out var uid, out var chain))
            chains.Add((uid, chain));
        return chains;
    }

    private void DeleteChains()
    {
        foreach (var chain in Chains())
            SEntMan.DeleteEntity(chain.Owner);
    }

    private RMCFlamerTankComponent TankPrototype(string id)
    {
        Assert.That(SProtoMan.Index<EntityPrototype>(id).TryGetComponent<RMCFlamerTankComponent>(out var tank, SEntMan.ComponentFactory),
            Is.True, $"{id} should be a flamer tank");
        return tank!;
    }

    private EntityUid SpawnXeno(MapId mapId, float x)
    {
        var uid = SEntMan.SpawnEntity("CMXenoDrone", new MapCoordinates(new Vector2(x, 0), mapId));
        var flammable = SEntMan.GetComponent<FlammableComponent>(uid);
        flammable.FirestackFade = 0;
        flammable.NextUpdate = SGameTiming.CurTime;
        return uid;
    }

    private void IgniteXeno(EntityUid uid, int intensity, int duration, int stacks)
    {
        var flammable = SEntMan.GetComponent<FlammableComponent>(uid);
        Assert.That(Server.System<RMCFlammableSystem>().Ignite((uid, flammable), intensity, duration, stacks), Is.True);
        // Ignition adds `duration` stacks; equalise them so only the duration differs between the two xenos.
        Server.System<ServerFlammableSystem>().SetFireStacks(uid, stacks, flammable);
        flammable.NextUpdate = SGameTiming.CurTime;
    }

    private void SetOxygenAtmosphere(EntityUid mapUid)
    {
        var moles = new float[Atmospherics.AdjustedNumberOfGases];
        moles[(int) Gas.Oxygen] = 21.824779f;
        moles[(int) Gas.Nitrogen] = 82.10312f;
        Server.System<AtmosphereSystem>().SetMapAtmosphere(mapUid, false, new GasMixture(moles, Atmospherics.T20C));
    }
}
