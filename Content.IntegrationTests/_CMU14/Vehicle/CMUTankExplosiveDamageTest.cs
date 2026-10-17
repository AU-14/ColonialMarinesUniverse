#pragma warning disable RA0002 // Arrange cook-off chances and multipliers directly to make the outcomes deterministic.
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Server._RMC14.Vehicle;
using Content.Server.Explosion.EntitySystems;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared._RMC14.Projectiles;
using Content.Shared._RMC14.Vehicle;
using Content.Shared._RMC14.Vehicle.Supply;
using Content.Shared.Damage;
using Content.Shared.Explosion.Components;
using Content.Shared.Interaction.Components;
using Content.Shared.Projectiles;
using Content.Shared.Trigger.Components.Triggers;
using Content.Shared.Trigger.Systems;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Vehicle;

/// <summary>
///     FLOW-09: every kind of explosive does a share of a tank's hull that fits its role, and only dedicated
///     anti-armour direct hits (by their own chance), a zeroed hull or an orbital bombardment cook a tank off.
///     Bands are for one use against a full, combat-loaded tank; a full stock tank is only checked for survival.
/// </summary>
[TestFixture]
public sealed class CMUTankExplosiveDamageTest : GameTest
{
    private const string ConsoleId = "VehicleSupplyConsole";
    private const string TankId = "VehicleTank";

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

    public enum Family
    {
        /// <summary>Grenades, 40mm, small charges: about 1-5% of a loaded hull.</summary>
        Small,
        /// <summary>Heavy explosives, mortars, HE rockets and shells, IEDs: about 5-20%.</summary>
        Heavy,
        /// <summary>Dedicated anti-armour direct hits: about 30-45%.</summary>
        AntiTank,
        /// <summary>The LTB HESH shell: an anti-armour round at about a fifth of the hull.</summary>
        Hesh,
        /// <summary>CAS rockets and bombs: about 15-25%.</summary>
        Cas,
        /// <summary>Orbital bombardment: catastrophic, always cooks a tank off.</summary>
        Orbital,
    }

    public enum Delivery
    {
        /// <summary>A direct hit by the projectile, then its own detonation where it struck.</summary>
        Projectile,
        /// <summary>The explosive entity triggered on the tank.</summary>
        Entity,
        /// <summary>An explosion queued on the tank with the prototype's (or the given) parameters.</summary>
        Queue,
    }

    public sealed record Case(string Name, Family Family, Delivery Delivery, string? Proto,
        string? Type = null, float Total = 0, float Slope = 0, float Max = 0)
    {
        public override string ToString() => Name;
    }

    public static readonly Case[] Cases =
    [
        new("HEDP grenade", Family.Small, Delivery.Entity, "CMGrenadeHighExplosive"),
        new("M12 blast grenade", Family.Small, Delivery.Entity, "RMCGrenadeBlastM12"),
        new("Throwable IED", Family.Small, Delivery.Entity, "RMCGrenadeIED"),
        new("20mm HE grenade", Family.Small, Delivery.Entity, "RMCGrenade20mmHE"),
        new("Pipe bomb", Family.Small, Delivery.Entity, "CMUPipeBomb"),
        new("HEAP grenade", Family.Heavy, Delivery.Entity, "CMUGrenadeHEAP"),
        new("IED", Family.Heavy, Delivery.Entity, "AU14IED"),
        new("IED vest", Family.Heavy, Delivery.Queue, "AU14IEDVest"),
        new("Bangalore", Family.Heavy, Delivery.Entity, "AU14Bangalore"),
        new("C4", Family.Heavy, Delivery.Entity, "RMCExplosivePlastic"),
        new("Mortar HE", Family.Heavy, Delivery.Queue, "RMCMortarShellHE"),
        new("Mortar incendiary", Family.Heavy, Delivery.Queue, "RMCMortarShellIncendiary"),
        new("AU14 HEATMP mortar", Family.Heavy, Delivery.Queue, "AU14MortarShellHEATMP"),
        new("84mm HE rocket", Family.Heavy, Delivery.Projectile, "RMCProjectileRocket84mm"),
        new("70mm HE rocket", Family.Heavy, Delivery.Projectile, "CMUProjectileRocket70mmHE"),
        new("RPG thermobaric", Family.Heavy, Delivery.Projectile, "RuMCProjectileRocketRPG_Termobaric"),
        new("LTB HE", Family.Heavy, Delivery.Projectile, "CMUProjectileLTBCannonHE"),
        new("LTB napalm", Family.Heavy, Delivery.Projectile, "CMUProjectileLTBCannonNapalm"),
        // No impact blast at all: a flechette direct hit (Piercing, x0.1 on the tank frame) and shrapnel. An
        // anti-personnel round, so it scratches a tank like a grenade rather than a heavy charge.
        new("LTB canister", Family.Small, Delivery.Projectile, "CMUProjectileLTBCannonCanister"),
        new("84mm AP rocket", Family.AntiTank, Delivery.Projectile, "RMCProjectileRocket84mmAntiArmor"),
        new("70mm AP rocket", Family.AntiTank, Delivery.Projectile, "CMUProjectileRocket70mmAntiArmor"),
        new("ATGM", Family.AntiTank, Delivery.Projectile, "CMUProjectileATGM"),
        new("Vehicle TOW", Family.AntiTank, Delivery.Projectile, "VehicleProjectileRocket84mmAntiArmor"),
        new("LTB APFSDS", Family.AntiTank, Delivery.Projectile, "CMUProjectileLTBCannonAPFSDS"),
        new("LTB HEAT", Family.AntiTank, Delivery.Projectile, "CMUProjectileLTBCannonHEAT"),
        new("LTB HESH", Family.Hesh, Delivery.Projectile, "RMCProjectileLTBCannon"),
        new("Widowmaker", Family.Cas, Delivery.Queue, "RMCDropshipAttachmentAmmoRocketWidowmaker"),
        new("Keeper", Family.Cas, Delivery.Queue, "RMCDropshipAttachmentAmmoRocketKeeper"),
        new("Mini-Mike", Family.Cas, Delivery.Queue, "RMCDropshipAttachmentAmmoRocketMiniMike"),
        new("Mk102 JDAM", Family.Cas, Delivery.Queue, "AU14DropshipAttachmentAmmoMk102JDAM"),
        new("Orbital bombardment", Family.Orbital, Delivery.Queue, null, "RMCOBXenoTunnel", 5000, 1500, 200),
    ];

    private static IEnumerable NonOrbitalCases => Cases.Where(c => c.Family != Family.Orbital);

    private static IEnumerable AntiTankCases =>
        Cases.Where(c => c.Family is Family.AntiTank or Family.Hesh);

    private static IEnumerable BlastCases =>
        Cases.Where(c => c.Delivery != Delivery.Projectile && c.Family != Family.Orbital);

    private readonly record struct Outcome(float Max, float Start, float Final, bool Destroyed, bool CookedOff,
        float Chance)
    {
        /// <summary>Hull lost to the explosive, as a fraction of the maximum.</summary>
        public float Lost => (Start - Final) / Max;
    }

    private async Task<(TestMapData Map, EntityUid Tank)> SpawnTank(bool loaded, float chance, float startHull,
        bool removeProfile = false)
    {
        var map = await Pair.CreateTestMap();
        EntityUid vehicle = default;
        await Server.WaitAssertion(() =>
        {
            map.Grid.Comp.CanSplit = false;
            var tiles = new List<(Vector2i, Tile)>();
            for (var x = -8; x <= 8; x++)
            {
                for (var y = -8; y <= 8; y++)
                {
                    tiles.Add((new Vector2i(x, y), map.Tile.Tile));
                }
            }

            Server.System<SharedMapSystem>().SetTiles(map.Grid.Owner, map.Grid.Comp, tiles);
            vehicle = SEntMan.SpawnEntity(TankId, map.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
            // Debug builds assert on a dynamic input mover that a blast shoves; a parked tank is the case here.
            SEntMan.EnsureComponent<BlockMovementComponent>(vehicle);
            if (removeProfile)
                SEntMan.RemoveComponent<CMUTankBlastProfileComponent>(vehicle);
        });

        await RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            if (loaded)
            {
                Assert.That(SProtoMan.Index<EntityPrototype>(ConsoleId)
                    .TryComp<VehicleSupplyConsoleComponent>(out var console, SEntMan.ComponentFactory), Is.True);
                var entry = console!.Vehicles.Single(v => v.Vehicle.Id == TankId);
                Assert.That(Server.System<VehicleSupplySystem>().DebugApplyLoadoutForTest(vehicle, entry, TankLoadout),
                    Is.True);
            }

            SEntMan.GetComponent<TankCookOffComponent>(vehicle).CriticalChance = chance;

            // Wear the modules down evenly (no multipliers), the way repeated chip damage leaves a tank.
            var hull = SEntMan.GetComponent<HardpointIntegrityComponent>(vehicle);
            var max = hull.MaxIntegrity;
            var hardpoints = Server.System<HardpointSystem>();
            for (var i = 0; i < 20 && hull.Integrity > max * startHull + 1f; i++)
            {
                hardpoints.DamageVehicleHull(vehicle, hull.Integrity - max * startHull);
            }
        });

        await RunTicksSync(5);
        return (map, vehicle);
    }

    /// <summary>Waits until the explosion system has nothing queued or in progress.</summary>
    private async Task DrainExplosions()
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var queue = typeof(ExplosionSystem).GetField("_explosionQueue", flags)!;
        var active = typeof(ExplosionSystem).GetField("_activeExplosion", flags)!;
        var preparing = typeof(ExplosionSystem).GetField("_preparingExplosion", flags)!;
        for (var i = 0; i < 60; i++)
        {
            var busy = false;
            await Server.WaitAssertion(() =>
            {
                var system = Server.System<ExplosionSystem>();
                busy = ((ICollection) queue.GetValue(system)!).Count > 0 ||
                       active.GetValue(system) != null ||
                       preparing.GetValue(system) != null;
            });
            if (!busy)
                return;

            await RunTicksSync(15);
        }

        Assert.Fail("Explosions did not finish processing.");
    }

    /// <summary>
    ///     Uses <paramref name="explosive"/> once on a VehicleTank (stock or combat-loaded) and reports the hull lost.
    ///     The tank's cook-off chance, and an anti-armour round's own chance, are set to <paramref name="chance"/>.
    /// </summary>
    private async Task<Outcome> Use(Case explosive, bool loaded, float chance = 0f, float startHull = 1f,
        bool hit = true, bool blast = true)
    {
        var (map, vehicle) = await SpawnTank(loaded, chance, startHull);

        var start = 0f;
        var roundChance = chance;
        await Server.WaitAssertion(() =>
        {
            var hull = SEntMan.GetComponent<HardpointIntegrityComponent>(vehicle);
            start = hull.Integrity;
            var centre = map.GridCoords.Offset(new Vector2(0.5f, 0.5f));
            switch (explosive.Delivery)
            {
                case Delivery.Projectile:
                {
                    // Same tile as the hull's centre, approaching from the front (south at rotation 0).
                    var projectile = SEntMan.SpawnEntity(explosive.Proto, map.GridCoords.Offset(new Vector2(0.2f, 0.5f)));
                    var projectileComp = SEntMan.GetComponent<ProjectileComponent>(projectile);
                    if (SEntMan.TryGetComponent(projectile, out TankCookOffProjectileComponent? round))
                    {
                        if (round.HullFraction is > 0f)
                            round.CriticalChance = chance;
                        roundChance = round.CriticalChance ?? chance;
                    }

                    var shooter = SEntMan.SpawnEntity(null, map.GridCoords.Offset(new Vector2(0.5f, -6f)));
                    Server.System<SharedProjectileSystem>().SetShooter(projectile, projectileComp, shooter);
                    if (hit)
                    {
                        Server.System<SharedProjectileSystem>().ProjectileCollide(
                            (projectile, projectileComp, SEntMan.GetComponent<PhysicsComponent>(projectile)),
                            vehicle);
                    }

                    if (!blast)
                    {
                        SEntMan.DeleteEntity(projectile);
                        break;
                    }

                    // As TriggerOnCollide would on impact. The spent round is left for its own DeleteOnTrigger, the
                    // natural path, but must not collide with the hull again while it waits.
                    if (SEntMan.HasComponent<TriggerOnCollideComponent>(projectile))
                        Assert.That(Server.System<TriggerSystem>().Trigger(projectile), Is.True, explosive.Name);
                    Server.System<SharedPhysicsSystem>().SetCanCollide(projectile, false);
                    break;
                }
                case Delivery.Entity:
                {
                    var entity = SEntMan.SpawnEntity(explosive.Proto, centre);
                    Assert.That(Server.System<TriggerSystem>().Trigger(entity), Is.True, explosive.Name);
                    break;
                }
                case Delivery.Queue:
                {
                    var (type, total, slope, max) = GetQueuedParameters(explosive);
                    Server.System<ExplosionSystem>().QueueExplosion(
                        Server.System<SharedTransformSystem>().ToMapCoordinates(centre),
                        type, total, slope, max, cause: null, maxTileBreak: 0, canCreateVacuum: false);
                    break;
                }
            }
        });

        await RunSeconds(1);
        await DrainExplosions();

        var outcome = default(Outcome);
        await Server.WaitAssertion(() =>
        {
            var hull = SEntMan.GetComponent<HardpointIntegrityComponent>(vehicle);
            outcome = new Outcome(hull.MaxIntegrity, start, hull.Integrity, hull.DestroyedBeyondRepair,
                SEntMan.HasComponent<ActiveTankCookOffComponent>(vehicle), roundChance);
        });

        TestContext.Progress.WriteLine(
            $"FLOW-09 {explosive.Name} ({explosive.Family}) on {(loaded ? "loaded" : "stock")} tank: max {outcome.Max:0}, " +
            $"start {outcome.Start:0}, final {outcome.Final:0}, lost {100f * outcome.Lost:0.0}%, " +
            $"destroyed {outcome.Destroyed}, cook-off {outcome.CookedOff}");
        return outcome;
    }

    private (string Type, float Total, float Slope, float Max) GetQueuedParameters(Case explosive)
    {
        if (explosive.Proto is not { } id)
            return (explosive.Type!, explosive.Total, explosive.Slope, explosive.Max);

        var proto = SProtoMan.Index<EntityPrototype>(id);
        var factory = SEntMan.ComponentFactory;
        if (proto.TryComp<ExplosiveComponent>(out var explosiveComp, factory))
        {
            return (explosiveComp.ExplosionType.Id, explosiveComp.TotalIntensity, explosiveComp.IntensitySlope,
                explosiveComp.MaxIntensity);
        }

        Assert.That(proto.TryComp<DropshipAmmoComponent>(out var ammo, factory), Is.True, id);
        var blast = ammo!.Explosion!;
        return (blast.Type.Id, blast.Total, blast.Slope, blast.Max);
    }

    /// <summary>
    ///     One use of each explosive (cook-off chance forced to 0) lands in its family's band on a combat-loaded
    ///     tank, and never cooks it off.
    /// </summary>
    [Test]
    public async Task LoadedTankBand([ValueSource(nameof(NonOrbitalCases))] Case explosive)
    {
        var result = await Use(explosive, loaded: true);
        var (low, high) = explosive.Family switch
        {
            Family.Small => (0.008f, 0.06f),
            Family.Heavy => (0.04f, 0.21f),
            Family.AntiTank => (0.28f, 0.47f),
            Family.Hesh => (0.15f, 0.26f),
            Family.Cas => (0.13f, 0.26f),
            _ => (0f, 1f),
        };

        Assert.Multiple(() =>
        {
            Assert.That(result.Lost, Is.InRange(low, high),
                $"FLOW-09: {explosive.Name} takes {100f * result.Lost:0.0}% of a loaded tank's hull.");
            Assert.That(result.Final, Is.GreaterThan(0f), "FLOW-09: one use never zeroes a full tank.");
            Assert.That(result.CookedOff, Is.False, "FLOW-09: with the roll forced to fail, nothing cooks off.");
            Assert.That(result.Destroyed, Is.False);
        });
    }

    /// <summary>
    ///     A full stock tank (cook-off roll forced to fail) survives one use of anything but an orbital bombardment,
    ///     and only anti-armour direct hits take 40% or more of it.
    /// </summary>
    [Test]
    public async Task StockTankSurvives([ValueSource(nameof(NonOrbitalCases))] Case explosive)
    {
        var result = await Use(explosive, loaded: false);
        Assert.Multiple(() =>
        {
            Assert.That(result.Final, Is.GreaterThan(0f), $"FLOW-09: {explosive.Name} must not zero a full stock tank.");
            Assert.That(result.CookedOff, Is.False);
            Assert.That(result.Destroyed, Is.False);
            if (explosive.Family is Family.AntiTank or Family.Hesh)
            {
                Assert.That(result.Lost, Is.GreaterThanOrEqualTo(0.15f), "An anti-armour hit takes a real chunk.");
            }
            else
            {
                Assert.That(result.Lost, Is.LessThan(0.4f),
                    $"FLOW-09: only anti-armour hits and OB take 40% of a stock tank; {explosive.Name} took " +
                    $"{100f * result.Lost:0.0}%.");
                Assert.That(result.Lost, Is.GreaterThan(0f), $"FLOW-09: {explosive.Name} must damage a stock tank.");
            }
        });
    }

    /// <summary>An orbital bombardment still cooks off any tank.</summary>
    [Test]
    public async Task OrbitalBombardmentCooksOff([Values(false, true)] bool loaded)
    {
        var result = await Use(Cases.Single(c => c.Family == Family.Orbital), loaded);
        Assert.That(result.CookedOff, Is.True, "An OB is catastrophic for a tank.");
    }

    /// <summary>A won roll on an anti-armour round's direct hit cooks the tank off.</summary>
    [Test]
    public async Task AntiTankDirectHitCanCookOff([ValueSource(nameof(AntiTankCases))] Case explosive)
    {
        var result = await Use(explosive, loaded: true, chance: 1f);
        Assert.That(result.CookedOff, Is.True, $"{explosive.Name}: a won roll on the direct hit cooks the tank off.");
    }

    /// <summary>An anti-armour round's own blast, without its direct hit, never rolls a cook-off.</summary>
    [Test]
    public async Task AntiTankBlastAloneNeverCooksOff([ValueSource(nameof(AntiTankCases))] Case explosive)
    {
        var result = await Use(explosive, loaded: true, chance: 1f, hit: false);
        Assert.Multiple(() =>
        {
            Assert.That(result.CookedOff, Is.False, $"{explosive.Name}: a blast never rolls a cook-off.");
            Assert.That(result.Lost, Is.LessThan(0.2f), $"{explosive.Name}: a blast never takes the round's chunk.");
        });
    }

    /// <summary>Blasts never roll a cook-off, even on a tank whose own chance is certain.</summary>
    [Test]
    public async Task BlastsNeverCookOff([ValueSource(nameof(BlastCases))] Case explosive)
    {
        var result = await Use(explosive, loaded: true, chance: 1f);
        Assert.That(result.CookedOff, Is.False, $"{explosive.Name}: a blast never rolls a cook-off.");
    }

    /// <summary>A blast that zeroes a worn-down hull cooks the tank off.</summary>
    [Test]
    public async Task ZeroedHullCooksOff()
    {
        var result = await Use(Cases.Single(c => c.Name == "IED"), loaded: false, startHull: 0.05f);
        Assert.Multiple(() =>
        {
            Assert.That(result.Final, Is.EqualTo(0f), "The blast zeroes a nearly dead hull.");
            Assert.That(result.CookedOff, Is.True, "A zeroed tank hull cooks off.");
        });
    }

    /// <summary>
    ///     A blast far beyond anything in the game (a non-orbital type, so the cap applies) removes exactly the blast
    ///     cap of a tank's hull, never zeroes it and never cooks it off, even with a certain cook-off roll.
    /// </summary>
    [Test]
    public async Task HugeBlastIsCapped([Values("CMUIED", "CMUCASMunition")] string type,
        [Values(false, true)] bool loaded)
    {
        var huge = new Case($"Huge {type}", Family.Heavy, Delivery.Queue, null, type, 20000, 50, 2000);
        var result = await Use(huge, loaded, chance: 1f);
        var cap = 0f;
        await Server.WaitAssertion(() =>
        {
            Assert.That(SProtoMan.Index<EntityPrototype>(TankId)
                .TryComp<CMUTankBlastProfileComponent>(out var profile, SEntMan.ComponentFactory), Is.True);
            cap = profile!.MaxBlastHullFraction;
        });

        Assert.Multiple(() =>
        {
            Assert.That(cap, Is.EqualTo(0.3f).Within(0.0001f));
            Assert.That(result.Lost, Is.EqualTo(cap).Within(0.01f),
                $"FLOW-09: one blast removes at most {cap:P0} of the hull; this took {100f * result.Lost:0.0}%.");
            Assert.That(result.Final, Is.GreaterThan(0f));
            Assert.That(result.CookedOff, Is.False, "A capped blast never cooks a tank off.");
            Assert.That(result.Destroyed, Is.False);
        });
    }

    /// <summary>
    ///     An anti-armour round takes its hull chunk once per tank: a repeat contact with the same tank (a
    ///     re-collision) adds nothing, but a second, different tank it goes on to strike (a penetrating round) takes
    ///     the full chunk of its own.
    /// </summary>
    [Test]
    public async Task AntiTankRoundChunksEachTankOnce()
    {
        var (map, first) = await SpawnTank(true, 0f, 1f);
        var (_, second) = await SpawnTank(true, 0f, 1f);

        float Lost(EntityUid tank)
        {
            var hull = SEntMan.GetComponent<HardpointIntegrityComponent>(tank);
            return (hull.MaxIntegrity - hull.Integrity) / hull.MaxIntegrity;
        }

        // All in one tick: the round is only queued for deletion by its hits, and the hull is updated synchronously.
        await Server.WaitAssertion(() =>
        {
            var projectile = SEntMan.SpawnEntity("CMUProjectileATGM", map.GridCoords.Offset(new Vector2(0.2f, 0.5f)));
            var round = SEntMan.GetComponent<TankCookOffProjectileComponent>(projectile);
            round.CriticalChance = 0f;
            var fraction = round.HullFraction!.Value;
            var comp = SEntMan.GetComponent<ProjectileComponent>(projectile);
            comp.DeleteOnCollide = false;
            var projectiles = Server.System<SharedProjectileSystem>();
            projectiles.SetShooter(projectile, comp,
                SEntMan.SpawnEntity(null, map.GridCoords.Offset(new Vector2(0.5f, -6f))));

            void Hit(EntityUid tank)
            {
                // As a penetrating round: still live after its previous hit.
                comp.ProjectileSpent = false;
                projectiles.ProjectileCollide((projectile, comp, SEntMan.GetComponent<PhysicsComponent>(projectile)),
                    tank);
            }

            Hit(first);
            var firstLost = Lost(first);
            Hit(first);
            var firstAgain = Lost(first);
            Hit(second);
            var secondLost = Lost(second);
            SEntMan.DeleteEntity(projectile);

            Assert.Multiple(() =>
            {
                Assert.That(firstLost, Is.EqualTo(fraction).Within(0.01f), "The first hit takes the round's chunk.");
                Assert.That(firstAgain, Is.EqualTo(firstLost).Within(0.001f),
                    "A repeat contact with the same tank takes nothing more.");
                Assert.That(secondLost, Is.EqualTo(fraction).Within(0.01f),
                    "A different tank the round strikes next takes the full chunk.");
            });
        });
    }

    /// <summary>
    ///     Fires an 84mm HE rocket (direct-hit damage removed, so only its blast counts) into a tank and lets physics
    ///     detonate it: TriggerOnCollide, ExplodeOnTrigger and a queued DeleteOnTrigger, the projectile still existing
    ///     when its explosion is processed. Returns the hull fraction lost.
    /// </summary>
    private async Task<float> NaturalRocketBlast(float rocketMultiplier, bool removeProfile)
    {
        var (map, vehicle) = await SpawnTank(true, 0f, 1f, removeProfile);
        EntityUid rocket = default;
        var start = 0f;
        var max = 0f;
        await Server.WaitAssertion(() =>
        {
            var hull = SEntMan.GetComponent<HardpointIntegrityComponent>(vehicle);
            start = hull.Integrity;
            max = hull.MaxIntegrity;
            rocket = SEntMan.SpawnEntity("RMCProjectileRocket84mm", map.GridCoords.Offset(new Vector2(-3f, 0.5f)));
            SEntMan.GetComponent<ProjectileComponent>(rocket).Damage = new DamageSpecifier();
            SEntMan.GetComponent<VehicleDamageMultiplierComponent>(rocket).Multiplier = rocketMultiplier;
            SEntMan.GetComponent<RMCProjectileAccuracyComponent>(rocket).ForceHit = true;
            // As a gun fires it: in the air, so floor friction does not stop it short of the hull.
            var physics = Server.System<SharedPhysicsSystem>();
            physics.SetBodyStatus(rocket, SEntMan.GetComponent<PhysicsComponent>(rocket), BodyStatus.InAir);
            physics.SetLinearVelocity(rocket, new Vector2(15f, 0f));
        });

        await RunSeconds(1);
        await DrainExplosions();

        var lost = 0f;
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(rocket), Is.True, "The rocket struck the tank and detonated.");
            lost = (start - SEntMan.GetComponent<HardpointIntegrityComponent>(vehicle).Integrity) / max;
        });
        TestContext.Progress.WriteLine(
            $"FLOW-09 natural 84mm HE blast, rocket multiplier {rocketMultiplier}, profile {!removeProfile}: " +
            $"lost {100f * lost:0.00}%");
        return lost;
    }

    /// <summary>
    ///     FLOW-09 origin leak: a blast names the exploding entity as its damage origin, and the hull read that
    ///     entity's vehicle damage multiplier (meant for its direct hit). A rocket detonating naturally on a tank is
    ///     gone before its blast lands, but a grenade that survives until its blast is processed (the HEAP) scaled its
    ///     blast by its multiplier. On a tank the blast profile alone decides blast damage.
    /// </summary>
    [Test]
    public async Task SourceVehicleMultiplierDoesNotScaleTankBlast()
    {
        var rocketWith = await NaturalRocketBlast(8f, false);
        var rocketWithout = await NaturalRocketBlast(1f, false);
        var heapWith = await HeapBlast(6f, false);
        var heapWithout = await HeapBlast(1f, false);
        Assert.Multiple(() =>
        {
            Assert.That(rocketWithout, Is.GreaterThan(0.01f), "The rocket's blast damages the tank.");
            Assert.That(rocketWith, Is.EqualTo(rocketWithout).Within(0.005f),
                "FLOW-09: a naturally detonating rocket's multiplier must not scale its blast on a tank.");
            Assert.That(heapWithout, Is.GreaterThan(0.01f), "The HEAP blast damages the tank.");
            Assert.That(heapWith, Is.EqualTo(heapWithout).Within(0.005f),
                "FLOW-09: a surviving grenade's multiplier must not scale its blast on a tank.");
        });
    }

    /// <summary>Triggers an HEAP grenade on a tank with its vehicle multiplier set, and reports the hull fraction lost.</summary>
    private async Task<float> HeapBlast(float multiplier, bool removeProfile)
    {
        var (map, vehicle) = await SpawnTank(true, 0f, 1f, removeProfile);
        var start = 0f;
        var max = 0f;
        await Server.WaitAssertion(() =>
        {
            var hull = SEntMan.GetComponent<HardpointIntegrityComponent>(vehicle);
            start = hull.Integrity;
            max = hull.MaxIntegrity;
            var grenade = SEntMan.SpawnEntity("CMUGrenadeHEAP", map.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
            SEntMan.GetComponent<VehicleDamageMultiplierComponent>(grenade).Multiplier = multiplier;
            Assert.That(Server.System<TriggerSystem>().Trigger(grenade), Is.True);
        });

        await RunSeconds(1);
        await DrainExplosions();

        var lost = 0f;
        await Server.WaitAssertion(() =>
        {
            lost = (start - SEntMan.GetComponent<HardpointIntegrityComponent>(vehicle).Integrity) / max;
        });
        return lost;
    }

    /// <summary>
    ///     How the exploding entity's own vehicle multiplier reaches its blast on a tank without a blast profile (the
    ///     behaviour before FLOW-09) and with one; not a gate.
    /// </summary>
    [Test, Explicit]
    public async Task MeasureOriginLeak()
    {
        var leaked = await NaturalRocketBlast(8f, true);
        var plain = await NaturalRocketBlast(1f, true);
        TestContext.Progress.WriteLine($"FLOW-09 origin leak, natural 84mm HE, no profile: x8 {leaked:P2} vs x1 {plain:P2}");
        var heapLeaked = await HeapBlast(6f, true);
        var heapPlain = await HeapBlast(1f, true);
        TestContext.Progress.WriteLine($"FLOW-09 origin leak, HEAP grenade, no profile: x6 {heapLeaked:P2} vs x1 {heapPlain:P2}");
        var heapProfile = await HeapBlast(6f, false);
        var heapProfilePlain = await HeapBlast(1f, false);
        TestContext.Progress.WriteLine($"FLOW-09 origin leak, HEAP grenade, profile: x6 {heapProfile:P2} vs x1 {heapProfilePlain:P2}");
    }

    /// <summary>The full balance table; not a gate.</summary>
    [Test, Explicit]
    public async Task Measure([ValueSource(nameof(Cases))] Case explosive, [Values(false, true)] bool loaded)
    {
        var result = await Use(explosive, loaded);
        TestContext.Progress.WriteLine(
            $"FLOW-09 TABLE | {explosive.Name} | {explosive.Family} | {(loaded ? "loaded" : "stock")} | " +
            $"{100f * result.Lost:0.0}% | chance {result.Chance:0.00} | cooked {result.CookedOff}");
    }

    /// <summary>Every anti-armour round states its hull chunk, so none silently falls back to the generic rule.</summary>
    [Test]
    public async Task EveryCookOffRoundHasHullFraction()
    {
        await Server.WaitAssertion(() =>
        {
            var factory = SEntMan.ComponentFactory;
            Assert.Multiple(() =>
            {
                foreach (var proto in SProtoMan.EnumeratePrototypes<EntityPrototype>())
                {
                    if (proto.Abstract || !proto.TryComp<TankCookOffProjectileComponent>(out var round, factory))
                        continue;

                    Assert.That(round.HullFraction, Is.Not.Null, $"{proto.ID} must set hullFraction (0 opts out).");
                    Assert.That(round.HullFraction, Is.InRange(0f, 0.6f), proto.ID);
                    if (round.HullFraction > 0f)
                        Assert.That(round.CriticalChance, Is.Not.Null, $"{proto.ID} must set its cook-off chance.");
                }
            });
        });
    }

    /// <summary>The EMBLR anti-armour cartridge fires its own 70mm projectile.</summary>
    [Test]
    public async Task Emblr70mmAntiArmourFiresItsOwnRocket()
    {
        await Server.WaitAssertion(() =>
        {
            var proto = SProtoMan.Index<EntityPrototype>("CMURocket70mmAntiArmor");
            Assert.That(proto.TryComp<CartridgeAmmoComponent>(out var cartridge, SEntMan.ComponentFactory), Is.True);
            Assert.That(cartridge!.Prototype.Id, Is.EqualTo("CMUProjectileRocket70mmAntiArmor"));
            Assert.That(SProtoMan.Index<EntityPrototype>("CMUProjectileRocket70mmAntiArmor")
                .TryComp<TriggerOnCollideComponent>(out _, SEntMan.ComponentFactory), Is.True,
                "The 70mm AP rocket detonates on impact.");
        });
    }

    /// <summary>Every tank that can cook off scales blasts through a blast profile.</summary>
    [Test]
    public async Task EveryTankHasBlastProfile()
    {
        await Server.WaitAssertion(() =>
        {
            var factory = SEntMan.ComponentFactory;
            var tanks = 0;
            Assert.Multiple(() =>
            {
                foreach (var proto in SProtoMan.EnumeratePrototypes<EntityPrototype>())
                {
                    if (proto.Abstract || !proto.TryComp<TankCookOffComponent>(out var tank, factory) || !tank.Enabled)
                        continue;

                    tanks++;
                    Assert.That(proto.TryComp<CMUTankBlastProfileComponent>(out _, factory), Is.True,
                        $"{proto.ID} can cook off but has no CMUTankBlastProfile.");
                }
            });
            Assert.That(tanks, Is.GreaterThanOrEqualTo(4));
        });
    }
}
