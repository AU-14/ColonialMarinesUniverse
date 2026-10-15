#pragma warning disable RA0002 // Arrange cook-off chances directly to make the hull rule deterministic.
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._RMC14.Vehicle;
using Content.Shared._RMC14.Vehicle;
using Content.Shared._RMC14.Vehicle.Supply;
using Content.Shared.Damage.Components;
using Content.Shared.Interaction.Components;
using Content.Shared.Projectiles;
using Content.Shared.Trigger.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Weapons.ATGM;

/// <summary>
///     FLOW-08: one ATGM hit (direct hit plus its blast) takes a heavy chunk of a tank's hull but leaves a healthy
///     tank alive and repairable, so only the warhead's cook-off roll (0.2) decides whether that hit burns it; a
///     damaged tank is finished off. Other vehicles keep the one-hit catastrophic rule.
/// </summary>
[TestFixture]
public sealed class CMUATGMTankCookOffTest : GameTest
{
    private const string ProjectileId = "CMUProjectileATGM";
    private const string RocketId = "RMCProjectileRocket84mmAntiArmor";
    private const string ConsoleId = "VehicleSupplyConsole";
    private const string Loaded = "VehicleTankLoaded";

    // A combat-loaded tank: every slot the supply console can fill on a VehicleTank.
    private static readonly Dictionary<string, string> TankLoadout = new()
    {
        ["primary"] = "VehicleTankLTBCannon",
        ["secondary"] = "VehicleTankTowLauncher",
        ["standardtreads"] = "VehicleTankTreads",
        ["armor"] = "VehicleTankArmorBallistic",
        ["support"] = "VehicleTankWarningArray",
    };

    public override PoolSettings PoolSettings => PsDisconnected;

    public enum Approach
    {
        /// <summary>No shooter: the struck module is picked at random.</summary>
        None,
        /// <summary>From the hull's front (south at rotation 0): the frontal armour takes the hit.</summary>
        Front,
        /// <summary>From the side: the treads take the hit.</summary>
        Side,
    }

    private readonly record struct HitResult(float Max, float Start, float AfterHit, float Final, bool Destroyed,
        bool CookedOff)
    {
        /// <summary>Hull lost to the last strike, as a fraction of the maximum.</summary>
        public float LostFraction => (Start - Final) / Max;
    }

    /// <summary>
    ///     Spawns <paramref name="vehicleId"/> and strikes it <paramref name="hits"/> times, settling a second after
    ///     each strike. With <paramref name="startHull"/> below 1 the hull is first worn down to that fraction.
    ///     The result covers the last strike.
    /// </summary>
    private async Task<HitResult> Strike(string vehicleId, Approach approach, bool hit, bool blast, float chance,
        string projectileId = ProjectileId, int hits = 1, float startHull = 1f)
    {
        var loaded = vehicleId == Loaded;
        var protoId = loaded ? "VehicleTank" : vehicleId;
        var map = await Pair.CreateTestMap();
        EntityUid vehicle = default;
        await Server.WaitAssertion(() =>
        {
            var tiles = new List<(Vector2i, Tile)>();
            for (var x = -8; x <= 8; x++)
            {
                for (var y = -8; y <= 8; y++)
                {
                    tiles.Add((new Vector2i(x, y), map.Tile.Tile));
                }
            }

            Server.System<SharedMapSystem>().SetTiles(map.Grid.Owner, map.Grid.Comp, tiles);
            vehicle = SEntMan.SpawnEntity(protoId, map.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
            // Debug builds assert on a dynamic input mover that the blast shoves; a parked, undriven tank is the
            // case measured here anyway.
            SEntMan.EnsureComponent<BlockMovementComponent>(vehicle);
        });

        await RunTicksSync(5);

        var max = 0f;
        await Server.WaitAssertion(() =>
        {
            if (loaded)
            {
                Assert.That(SProtoMan.Index<EntityPrototype>(ConsoleId)
                    .TryComp<VehicleSupplyConsoleComponent>(out var console, SEntMan.ComponentFactory), Is.True);
                var entry = console!.Vehicles.Single(v => v.Vehicle.Id == protoId);
                Assert.That(Server.System<VehicleSupplySystem>().DebugApplyLoadoutForTest(vehicle, entry, TankLoadout),
                    Is.True);
            }

            var hull = SEntMan.GetComponent<HardpointIntegrityComponent>(vehicle);
            max = hull.MaxIntegrity;
            if (SEntMan.TryGetComponent(vehicle, out TankCookOffComponent? tank))
                tank.CriticalChance = chance;

            // Wear the modules down evenly (no multipliers), the way repeated chip damage leaves a tank.
            var hardpoints = Server.System<HardpointSystem>();
            for (var i = 0; i < 20 && hull.Integrity > max * startHull + 1f; i++)
            {
                hardpoints.DamageVehicleHull(vehicle, hull.Integrity - max * startHull);
            }
        });

        await RunTicksSync(5);

        var start = 0f;
        var afterHit = 0f;
        var frameHit = 0f;
        var frameTotal = 0f;
        var result = default(HitResult);
        for (var strike = 1; strike <= hits; strike++)
        {
            var frameBefore = 0f;
            await Server.WaitAssertion(() =>
            {
                var hull = SEntMan.GetComponent<HardpointIntegrityComponent>(vehicle);
                start = hull.Integrity;

                // Same tile as the hull's centre: the blast's peak intensity, the worst case for the struck tank.
                var projectile = SEntMan.SpawnEntity(projectileId, map.GridCoords.Offset(new Vector2(0.2f, 0.5f)));
                var projectileComp = SEntMan.GetComponent<ProjectileComponent>(projectile);
                SEntMan.GetComponent<TankCookOffProjectileComponent>(projectile).CriticalChance = chance;
                Vector2? shooterOffset = approach switch
                {
                    Approach.Front => new Vector2(0.5f, -6f),
                    Approach.Side => new Vector2(6.5f, 0.5f),
                    _ => null,
                };
                if (shooterOffset is { } offset)
                {
                    var shooter = SEntMan.SpawnEntity(null, map.GridCoords.Offset(offset));
                    Server.System<SharedProjectileSystem>().SetShooter(projectile, projectileComp, shooter);
                }

                var damageable = SEntMan.GetComponent<DamageableComponent>(vehicle);
                frameBefore = damageable.TotalDamage.Float();
                if (hit)
                {
                    Server.System<SharedProjectileSystem>().ProjectileCollide(
                        (projectile, projectileComp, SEntMan.GetComponent<PhysicsComponent>(projectile)),
                        vehicle);
                }

                afterHit = hull.Integrity;
                frameHit = damageable.TotalDamage.Float() - frameBefore;
                if (blast)
                    Assert.That(Server.System<TriggerSystem>().Trigger(projectile), Is.True);

                // The explosion is queued at the missile's position; its spent hull must not collide again next tick.
                SEntMan.DeleteEntity(projectile);
            });

            await RunSeconds(1);

            await Server.WaitAssertion(() =>
            {
                frameTotal = SEntMan.GetComponent<DamageableComponent>(vehicle).TotalDamage.Float() - frameBefore;
                var hull = SEntMan.GetComponent<HardpointIntegrityComponent>(vehicle);
                result = new HitResult(max, start, afterHit, hull.Integrity, hull.DestroyedBeyondRepair,
                    SEntMan.HasComponent<ActiveTankCookOffComponent>(vehicle));
            });

            // Frame damage is the frame's share of the hit (half while modules are intact), after hull armour.
            TestContext.Progress.WriteLine(
                $"FLOW-08 {projectileId} {vehicleId} {approach} hit={hit} blast={blast} chance={chance} " +
                $"strike {strike}/{hits}: max {result.Max}, start {result.Start}, frame dmg hit {frameHit:0.#} " +
                $"total {frameTotal:0.#}, after hit {result.AfterHit}, final {result.Final} " +
                $"({100f * result.LostFraction:0.#}% of max lost), destroyed {result.Destroyed}, " +
                $"cook-off {result.CookedOff}");
        }

        return result;
    }

    /// <summary>
    ///     With the cook-off roll forced to fail, a full ATGM hit must leave every tank alive and repairable but
    ///     heavily damaged: the direct hit is a catastrophic hit, which takes a hull chunk instead of wrecking the
    ///     tank, and the blast does not add a second chunk.
    /// </summary>
    [Test]
    public async Task OneHitLeavesTankDamagedButAlive(
        [Values("VehicleTank", Loaded, "VehicleTankPMC", "VehicleTankTWE", "VehicleSPPTank")] string vehicle,
        [Values(Approach.Front, Approach.Side)] Approach approach)
    {
        var result = await Strike(vehicle, approach, true, true, 0f);
        Assert.Multiple(() =>
        {
            Assert.That(result.Final, Is.GreaterThan(0f), "FLOW-08: one ATGM hit must not zero the hull.");
            Assert.That(result.Destroyed, Is.False, "FLOW-08: a tank is never destroyed beyond repair by one hit.");
            Assert.That(result.CookedOff, Is.False, "FLOW-08: only the warhead's roll may cook off a surviving tank.");
            Assert.That(result.LostFraction, Is.GreaterThanOrEqualTo(0.35f),
                "FLOW-08: an ATGM hit takes a heavy chunk of the hull.");
            Assert.That(result.LostFraction, Is.LessThanOrEqualTo(0.6f),
                "FLOW-08: the warhead's blast must not add a second chunk to its own hit.");
        });
    }

    /// <summary>A tank with less hull left than the chunk is finished by the hit, and a zeroed hull cooks off.</summary>
    [Test]
    public async Task DamagedTankIsFinishedByTheChunk([Values("VehicleTank", Loaded)] string vehicle)
    {
        var result = await Strike(vehicle, Approach.Front, true, false, 0f, startHull: 0.3f);
        Assert.Multiple(() =>
        {
            Assert.That(result.Start, Is.LessThan(result.Max * 0.4f), "The tank starts with less hull than a chunk.");
            Assert.That(result.Final, Is.EqualTo(0f), "FLOW-08: the chunk zeroes a hull smaller than itself.");
            Assert.That(result.CookedOff, Is.True, "A zeroed tank hull cooks off.");
            Assert.That(result.Destroyed, Is.False, "The chunk never marks a tank destroyed beyond repair.");
        });
    }

    /// <summary>Repeated ATGM hits kill a full tank within three hits (cook-off roll forced to fail).</summary>
    [Test]
    public async Task ThreeHitsKillTank([Values("VehicleTank", Loaded)] string vehicle)
    {
        var result = await Strike(vehicle, Approach.Front, true, true, 0f, hits: 3);
        Assert.Multiple(() =>
        {
            Assert.That(result.Final, Is.EqualTo(0f), "FLOW-08: three ATGM hits zero a tank's hull.");
            Assert.That(result.CookedOff, Is.True, "A zeroed tank hull cooks off.");
        });
    }

    /// <summary>
    ///     Scope: vehicles other than cook-off tanks (and the AEV, whose cook-off is disabled) are still destroyed
    ///     beyond repair by one hit at least as large as their whole hull.
    /// </summary>
    [Test]
    public async Task NonTankKeepsCatastrophicRule([Values("VehicleAPC", "VehicleAev")] string vehicle)
    {
        var result = await Strike(vehicle, Approach.Front, true, false, 0f);
        Assert.Multiple(() =>
        {
            Assert.That(result.Destroyed, Is.True, "A catastrophic hit still wrecks a non-tank vehicle.");
            Assert.That(result.Final, Is.EqualTo(0f));
        });
    }

    [Test]
    public async Task WonCookOffRollStartsCookOff()
    {
        var result = await Strike("VehicleTank", Approach.Front, true, true, 1f);
        Assert.That(result.CookedOff, Is.True, "A won roll on the direct hit cooks the tank off.");
    }

    /// <summary>Reference numbers for the balance report; not a gate.</summary>
    [Test, Explicit]
    public async Task Measure(
        [Values("VehicleTank", Loaded, "VehicleTankPMC", "VehicleTankTWE", "VehicleSPPTank", "VehicleAev",
            "VehicleAPC", "CMUVehicleAPCWolfpack")] string vehicle,
        [Values(Approach.None, Approach.Front, Approach.Side)] Approach approach,
        [Values(0, 1, 2)] int mode)
    {
        await Strike(vehicle, approach, mode != 1, mode != 0, 0f, hits: mode == 2 ? 3 : 1);
    }

    /// <summary>Reference numbers for the M5 anti-armour rocket, which the tank catastrophic-hit rule also covers.</summary>
    [Test, Explicit]
    public async Task MeasureRocket(
        [Values("VehicleTank", Loaded)] string vehicle,
        [Values(Approach.None, Approach.Front, Approach.Side)] Approach approach,
        [Values(0, 2)] int mode)
    {
        await Strike(vehicle, approach, true, mode != 0, 0f, RocketId, hits: mode == 2 ? 3 : 1);
    }
}
