#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client._RMC14.Weapons.Ranged.Prediction;
using Content.Shared._RMC14.Weapons.Ranged.Prediction;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.CombatMode;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Client.GameObjects;
using Robust.Client.GameStates;
using Robust.Server.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests._RMC14.Weapons.Ranged;

[TestFixture]
[TestOf(typeof(GunPredictionSystem))]
public sealed class RMCGunResponsivenessTest
{
    private const string BurstProjectilePrototype = "RMCGunResponsivenessBurstProjectile";
    private const string SprayVaporPrototype = "RMCGunResponsivenessSprayVapor";

    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          parent: BaseItem
          id: RMCGunResponsivenessCloseGun
          components:
          - type: Gun
            fireRate: 30
            projectileSpeed: 30
            resetOnHandSelected: false
            soundGunshot: null
            soundEmpty: null
          - type: BasicEntityAmmoProvider
            proto: RMCGunResponsivenessCloseProjectile
            capacity: 1
            count: 1

        - type: entity
          parent: BaseBullet
          id: RMCGunResponsivenessCloseProjectile
          components:
          - type: Projectile
            deleteOnCollide: true
            impactEffect: null
            soundHit: null
          - type: TimedDespawn
            lifetime: 10

        - type: entity
          parent: BaseItem
          id: RMCGunResponsivenessBurstGun
          components:
          - type: Gun
            fireRate: 30
            burstFireRate: 15
            shotsPerBurst: 3
            selectedMode: Burst
            availableModes:
            - Burst
            projectileSpeed: 0.01
            resetOnHandSelected: false
            soundGunshot: null
            soundEmpty: null
          - type: BasicEntityAmmoProvider
            proto: RMCGunResponsivenessBurstProjectile
            capacity: 3
            count: 3

        - type: entity
          parent: BaseBullet
          id: RMCGunResponsivenessBurstProjectile
          components:
          - type: Projectile
            deleteOnCollide: false
            impactEffect: null
            soundHit: null
          - type: TimedDespawn
            lifetime: 10

        - type: entity
          id: RMCGunResponsivenessCloseTarget
          components:
          - type: Physics
            bodyType: Static
          - type: Fixtures
            fixtures:
              body:
                shape: !type:PhysShapeCircle
                  radius: 0.5
                hard: true
                layer:
                - BulletImpassable
          - type: LagCompensation

        - type: entity
          parent: MobHuman
          id: RMCGunResponsivenessClumsyPlayer
          components:
          - type: Clumsy
            clumsyDefaultCheck: 1
            gunShootFailStunTime: 0

        - type: entity
          parent: BaseItem
          id: RMCGunResponsivenessFlamer
          components:
          - type: Gun
            fireRate: 1
            resetOnHandSelected: false
            soundGunshot: null
            soundEmpty: null
          - type: SolutionContainerManager
            solutions:
              rmc_flamer_tank:
                maxVol: 10
                reagents:
                - ReagentId: RMCNapalmUT
                  Quantity: 10
          - type: RMCFlamerAmmoProvider
            costPer: 1
          - type: RMCFlamerTank
            maxRange: 5

        - type: entity
          abstract: true
          parent: BaseItem
          id: RMCGunResponsivenessSprayBase
          components:
          - type: Gun
            fireRate: 1
            resetOnHandSelected: false
            soundGunshot: null
            soundEmpty: null
          - type: RMCSprayAmmoProvider
            cost: 5
            spawn: RMCGunResponsivenessSprayVapor
            hitUser: false
          - type: Spray
            solution: spray
            transferAmount: 2
            sprayedPrototype: RMCGunResponsivenessSprayVapor
            vaporAmount: 3
            vaporSpread: 0
            sprayDistance: 5
            sprayVelocity: 5
            pushbackAmount: 0
            spraySound:
              path: /Audio/Effects/extinguish.ogg

        - type: entity
          parent: RMCGunResponsivenessSprayBase
          id: RMCGunResponsivenessSprayGun
          components:
          - type: SolutionContainerManager
            solutions:
              spray:
                maxVol: 20
                reagents:
                - ReagentId: Water
                  Quantity: 20

        - type: entity
          parent: RMCGunResponsivenessSprayBase
          id: RMCGunResponsivenessEmptySprayGun
          components:
          - type: SolutionContainerManager
            solutions:
              spray:
                maxVol: 20
                reagents:
                - ReagentId: Water
                  Quantity: 4

        - type: entity
          parent: Vapor
          id: RMCGunResponsivenessSprayVapor
        """;

    [Test]
    public async Task AuthoritativeProjectileStaysHiddenWhenPredictedCopyAlreadyHit()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });
        var server = pair.Server;
        var client = pair.Client;
        var sEntMan = server.EntMan;
        var cEntMan = client.EntMan;
        var playerManager = server.ResolveDependency<IPlayerManager>();
        var serverSession = playerManager.Sessions.Single();
        var map = await pair.CreateTestMap();
        EntityUid sPlayer = default;
        EntityUid sGun = default;
        EntityUid sTarget = default;

        await server.WaitPost(() =>
        {
            sPlayer = sEntMan.SpawnEntity("MobHuman", map.GridCoords);
            Assert.That(playerManager.SetAttachedEntity(serverSession, sPlayer), Is.True);
            sGun = sEntMan.SpawnEntity("RMCGunResponsivenessCloseGun", map.GridCoords);
            Assert.That(server.System<SharedHandsSystem>().TryPickup(sPlayer, sGun), Is.True);
            server.System<SharedCombatModeSystem>().SetInCombatMode(sPlayer, true);
            sTarget = sEntMan.SpawnEntity(
                "RMCGunResponsivenessCloseTarget",
                map.GridCoords.Offset(Vector2.UnitX * 2));
        });
        await pair.RunTicksSync(5);

        var cPlayer = client.Session?.AttachedEntity ??
            throw new AssertionException("The client must have an attached entity.");
        var cGun = cEntMan.GetEntity(sEntMan.GetNetEntity(sGun));
        var cTarget = cEntMan.GetEntity(sEntMan.GetNetEntity(sTarget));
        List<EntityUid>? predictedProjectiles = null;

        await client.WaitPost(() =>
        {
            var target = cEntMan.GetComponent<TransformComponent>(cTarget).Coordinates;
            var targetCoordinates = cEntMan.GetNetCoordinates(target);
            var targetEntity = cEntMan.GetNetEntity(cTarget);
            predictedProjectiles = client.System<GunPredictionSystem>().ShootRequested(
                cEntMan.GetNetEntity(cGun),
                targetCoordinates,
                targetEntity,
                client.Session!);

            Assert.That(predictedProjectiles, Has.Count.EqualTo(1));
            cEntMan.RaisePredictiveEvent(new RequestShootEvent
            {
                Gun = cEntMan.GetNetEntity(cGun),
                Coordinates = targetCoordinates,
                Target = targetEntity,
                Shot = [predictedProjectiles!.Single().Id],
                LastRealTick = default,
            });
        });

        Assert.That(predictedProjectiles, Has.Count.EqualTo(1));
        var predictedProjectile = predictedProjectiles!.Single();
        await client.WaitAssertion(() =>
        {
            Assert.That(cEntMan.IsClientSide(predictedProjectile), Is.True);
            Assert.That(cEntMan.HasComponent<PredictedProjectileClientComponent>(predictedProjectile), Is.True);
        });

        // Simulate enough one-way latency for the local projectile to finish while its earlier request is in flight.
        await client.WaitRunTicks(10);
        await client.WaitAssertion(() => Assert.That(cEntMan.EntityExists(predictedProjectile), Is.False));

        await pair.RunTicksSync(3);

        EntityUid serverProjectile = default;
        await server.WaitAssertion(() =>
        {
            var query = sEntMan.EntityQueryEnumerator<PredictedProjectileServerComponent>();
            Assert.That(query.MoveNext(out serverProjectile, out var prediction), Is.True);
            Assert.That(prediction!.ClientId, Is.EqualTo(predictedProjectile.Id));
            Assert.That(query.MoveNext(out _, out _), Is.False);
        });

        var authoritativeProjectile = cEntMan.GetEntity(sEntMan.GetNetEntity(serverProjectile));
        await client.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(cEntMan.EntityExists(authoritativeProjectile), Is.True);
                Assert.That(cEntMan.IsClientSide(authoritativeProjectile), Is.False);
                Assert.That(
                    cEntMan.GetComponent<PredictedProjectileServerComponent>(authoritativeProjectile).ClientId,
                    Is.EqualTo(predictedProjectile.Id));
                Assert.That(cEntMan.GetComponent<SpriteComponent>(authoritativeProjectile).Visible, Is.False);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ServerCancelledShotStillReservesPredictedSpreadSequence()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });
        var server = pair.Server;
        var client = pair.Client;
        var sEntMan = server.EntMan;
        var cEntMan = client.EntMan;
        var playerManager = server.ResolveDependency<IPlayerManager>();
        var serverSession = playerManager.Sessions.Single();
        var map = await pair.CreateTestMap();
        EntityUid sPlayer = default;
        EntityUid sGun = default;

        await server.WaitPost(() =>
        {
            sPlayer = sEntMan.SpawnEntity("RMCGunResponsivenessClumsyPlayer", map.GridCoords);
            Assert.That(playerManager.SetAttachedEntity(serverSession, sPlayer), Is.True);
            sGun = sEntMan.SpawnEntity("RMCGunResponsivenessCloseGun", map.GridCoords);
            Assert.That(server.System<SharedHandsSystem>().TryPickup(sPlayer, sGun), Is.True);
            server.System<SharedCombatModeSystem>().SetInCombatMode(sPlayer, true);
        });
        await pair.RunTicksSync(5);

        var cPlayer = client.Session?.AttachedEntity ??
            throw new AssertionException("The client must have an attached entity.");
        var cGun = cEntMan.GetEntity(sEntMan.GetNetEntity(sGun));
        List<EntityUid>? predictedProjectiles = null;
        await client.WaitPost(() =>
        {
            var target = new EntityCoordinates(cPlayer, Vector2.UnitX * 100);
            var targetCoordinates = cEntMan.GetNetCoordinates(target);
            predictedProjectiles = client.System<GunPredictionSystem>().ShootRequested(
                cEntMan.GetNetEntity(cGun),
                targetCoordinates,
                null,
                client.Session!);

            cEntMan.RaisePredictiveEvent(new RequestShootEvent
            {
                Gun = cEntMan.GetNetEntity(cGun),
                Coordinates = targetCoordinates,
                Shot = predictedProjectiles?.Select(projectile => projectile.Id).ToList(),
                LastRealTick = default,
            });
        });
        await pair.RunTicksSync(5);

        Assert.That(predictedProjectiles, Has.Count.EqualTo(1));
        await server.WaitAssertion(() =>
        {
            var predictions = sEntMan.EntityQueryEnumerator<PredictedProjectileServerComponent>();
            Assert.Multiple(() =>
            {
                Assert.That(sEntMan.GetComponent<BasicEntityAmmoProviderComponent>(sGun).Count, Is.Zero);
                Assert.That(sEntMan.GetComponent<GunComponent>(sGun).SpreadSequence, Is.EqualTo(1));
                Assert.That(predictions.MoveNext(out _, out _), Is.False);
            });
        });
        await client.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(cEntMan.EntityExists(predictedProjectiles!.Single()), Is.False);
                Assert.That(cEntMan.GetComponent<GunComponent>(cGun).SpreadSequence, Is.EqualTo(1));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FlamerFuelDebitSurvivesPredictionReplayExactlyOnce()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });
        var server = pair.Server;
        var client = pair.Client;
        var sEntMan = server.EntMan;
        var cEntMan = client.EntMan;
        var playerManager = server.ResolveDependency<IPlayerManager>();
        var serverSession = playerManager.Sessions.Single();
        var map = await pair.CreateTestMap();
        EntityUid sPlayer = default;
        EntityUid sGun = default;

        await server.WaitPost(() =>
        {
            sPlayer = sEntMan.SpawnEntity("MobHuman", map.GridCoords);
            Assert.That(playerManager.SetAttachedEntity(serverSession, sPlayer), Is.True);
            sGun = sEntMan.SpawnEntity("RMCGunResponsivenessFlamer", map.GridCoords);
            Assert.That(server.System<SharedHandsSystem>().TryPickup(sPlayer, sGun), Is.True);
            server.System<SharedCombatModeSystem>().SetInCombatMode(sPlayer, true);
        });
        await pair.SyncTicks();
        await pair.RunTicksSync(25);

        var stateManager = client.ResolveDependency<IClientGameStateManager>();
        Assert.That(stateManager.GetApplicableStateCount(), Is.EqualTo(stateManager.TargetBufferSize));
        await client.WaitRunTicks(1);

        var cPlayer = client.Session?.AttachedEntity ??
            throw new AssertionException("The client must have an attached entity.");
        var cGun = cEntMan.GetEntity(sEntMan.GetNetEntity(sGun));
        var initialFuel = GetFuel(sEntMan, sGun);
        Assert.That(initialFuel, Is.EqualTo(FixedPoint2.New(10)));

        await client.WaitPost(() =>
        {
            var target = new EntityCoordinates(cPlayer, Vector2.UnitX * 2);
            var targetCoordinates = cEntMan.GetNetCoordinates(target);
            var predictedProjectiles = client.System<GunPredictionSystem>().ShootRequested(
                cEntMan.GetNetEntity(cGun),
                targetCoordinates,
                null,
                client.Session!);

            Assert.That(predictedProjectiles, Is.Empty);
            cEntMan.RaisePredictiveEvent(new RequestShootEvent
            {
                Gun = cEntMan.GetNetEntity(cGun),
                Coordinates = targetCoordinates,
                Shot = [],
                LastRealTick = default,
            });
        });

        var predictedFuel = GetFuel(cEntMan, cGun);
        Assert.That(predictedFuel, Is.LessThan(initialFuel));
        for (var i = 0; i < 2; i++)
        {
            await server.WaitRunTicks(1);
            Assert.That(GetFuel(sEntMan, sGun), Is.EqualTo(initialFuel));

            await client.WaitRunTicks(1);
            Assert.That(GetFuel(cEntMan, cGun), Is.EqualTo(predictedFuel));
        }

        await server.WaitRunTicks(1);
        Assert.That(GetFuel(sEntMan, sGun), Is.EqualTo(predictedFuel));
        await client.WaitRunTicks(1);
        Assert.That(GetFuel(cEntMan, cGun), Is.EqualTo(predictedFuel));

        await pair.RunTicksSync(3);
        Assert.Multiple(() =>
        {
            Assert.That(GetFuel(sEntMan, sGun), Is.EqualTo(predictedFuel));
            Assert.That(GetFuel(cEntMan, cGun), Is.EqualTo(predictedFuel));
        });

        await pair.CleanReturnAsync();

        static FixedPoint2 GetFuel(IEntityManager entityManager, EntityUid gun)
        {
            var solutions = entityManager.System<SharedSolutionContainerSystem>();
            Assert.That(solutions.TryGetSolution(gun, "rmc_flamer_tank", out _, out var solution), Is.True);
            return solution!.Volume;
        }
    }

    [Test]
    public async Task SprayFuelDebitMatchesAuthorityThroughPredictionReplay()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });
        var server = pair.Server;
        var client = pair.Client;
        var sEntMan = server.EntMan;
        var cEntMan = client.EntMan;
        var playerManager = server.ResolveDependency<IPlayerManager>();
        var serverSession = playerManager.Sessions.Single();
        var map = await pair.CreateTestMap();
        EntityUid sPlayer = default;
        EntityUid sGun = default;

        await server.WaitPost(() =>
        {
            sPlayer = sEntMan.SpawnEntity("MobHuman", map.GridCoords);
            Assert.That(playerManager.SetAttachedEntity(serverSession, sPlayer), Is.True);
            sGun = sEntMan.SpawnEntity("RMCGunResponsivenessSprayGun", map.GridCoords);
            Assert.That(server.System<SharedHandsSystem>().TryPickup(sPlayer, sGun), Is.True);
            server.System<SharedCombatModeSystem>().SetInCombatMode(sPlayer, true);
        });
        await pair.SyncTicks();
        await pair.RunTicksSync(25);

        var stateManager = client.ResolveDependency<IClientGameStateManager>();
        Assert.That(stateManager.GetApplicableStateCount(), Is.EqualTo(stateManager.TargetBufferSize));
        await client.WaitRunTicks(1);

        var cPlayer = client.Session?.AttachedEntity ??
            throw new AssertionException("The client must have an attached entity.");
        var cGun = cEntMan.GetEntity(sEntMan.GetNetEntity(sGun));
        var initialFuel = GetSprayFuel(sEntMan, sGun);
        Assert.Multiple(() =>
        {
            Assert.That(initialFuel, Is.EqualTo(FixedPoint2.New(20)));
            Assert.That(server.System<Content.Server.Weapons.Ranged.Systems.GunSystem>().GetAmmoCount(sGun),
                Is.EqualTo(4));
            Assert.That(client.System<Content.Client.Weapons.Ranged.Systems.GunSystem>().GetAmmoCount(cGun),
                Is.EqualTo(4));
        });

        await client.WaitPost(() =>
        {
            var target = new EntityCoordinates(cPlayer, Vector2.UnitX * 2);
            var targetCoordinates = cEntMan.GetNetCoordinates(target);
            var predictedProjectiles = client.System<GunPredictionSystem>().ShootRequested(
                cEntMan.GetNetEntity(cGun),
                targetCoordinates,
                null,
                client.Session!);

            Assert.That(predictedProjectiles, Is.Empty);
            cEntMan.RaisePredictiveEvent(new RequestShootEvent
            {
                Gun = cEntMan.GetNetEntity(cGun),
                Coordinates = targetCoordinates,
                Shot = [],
                LastRealTick = default,
            });
        });

        var predictedFuel = GetSprayFuel(cEntMan, cGun);
        Assert.Multiple(() =>
        {
            Assert.That(predictedFuel, Is.EqualTo(FixedPoint2.New(15)));
            Assert.That(client.System<Content.Client.Weapons.Ranged.Systems.GunSystem>().GetAmmoCount(cGun),
                Is.EqualTo(3));
            Assert.That(cEntMan.GetComponent<GunComponent>(cGun).SpreadSequence, Is.EqualTo(1));
        });

        for (var i = 0; i < 2; i++)
        {
            await server.WaitRunTicks(1);
            Assert.That(GetSprayFuel(sEntMan, sGun), Is.EqualTo(initialFuel));

            await client.WaitRunTicks(1);
            Assert.That(GetSprayFuel(cEntMan, cGun), Is.EqualTo(predictedFuel));
        }

        await server.WaitRunTicks(1);
        await client.WaitRunTicks(1);
        Assert.Multiple(() =>
        {
            Assert.That(GetSprayFuel(sEntMan, sGun), Is.EqualTo(predictedFuel));
            Assert.That(GetSprayFuel(cEntMan, cGun), Is.EqualTo(predictedFuel));
            Assert.That(server.System<Content.Server.Weapons.Ranged.Systems.GunSystem>().GetAmmoCount(sGun),
                Is.EqualTo(3));
            Assert.That(client.System<Content.Client.Weapons.Ranged.Systems.GunSystem>().GetAmmoCount(cGun),
                Is.EqualTo(3));
            Assert.That(sEntMan.GetComponent<GunComponent>(sGun).SpreadSequence, Is.EqualTo(1));
            Assert.That(cEntMan.GetComponent<GunComponent>(cGun).SpreadSequence, Is.EqualTo(1));
            Assert.That(CountPrototype(sEntMan, SprayVaporPrototype), Is.EqualTo(3));
        });

        await pair.RunTicksSync(3);
        Assert.Multiple(() =>
        {
            Assert.That(GetSprayFuel(sEntMan, sGun), Is.EqualTo(predictedFuel));
            Assert.That(GetSprayFuel(cEntMan, cGun), Is.EqualTo(predictedFuel));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SprayWithLessThanOneCostDoesNotShoot()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });
        var server = pair.Server;
        var client = pair.Client;
        var sEntMan = server.EntMan;
        var cEntMan = client.EntMan;
        var playerManager = server.ResolveDependency<IPlayerManager>();
        var serverSession = playerManager.Sessions.Single();
        var map = await pair.CreateTestMap();
        EntityUid sPlayer = default;
        EntityUid sGun = default;

        await server.WaitPost(() =>
        {
            sPlayer = sEntMan.SpawnEntity("MobHuman", map.GridCoords);
            Assert.That(playerManager.SetAttachedEntity(serverSession, sPlayer), Is.True);
            sGun = sEntMan.SpawnEntity("RMCGunResponsivenessEmptySprayGun", map.GridCoords);
            Assert.That(server.System<SharedHandsSystem>().TryPickup(sPlayer, sGun), Is.True);
            server.System<SharedCombatModeSystem>().SetInCombatMode(sPlayer, true);
        });
        await pair.RunTicksSync(5);

        var cPlayer = client.Session?.AttachedEntity ??
            throw new AssertionException("The client must have an attached entity.");
        var cGun = cEntMan.GetEntity(sEntMan.GetNetEntity(sGun));
        var initialFuel = GetSprayFuel(sEntMan, sGun);
        Assert.Multiple(() =>
        {
            Assert.That(initialFuel, Is.EqualTo(FixedPoint2.New(4)));
            Assert.That(server.System<Content.Server.Weapons.Ranged.Systems.GunSystem>().GetAmmoCount(sGun),
                Is.Zero);
            Assert.That(server.System<Content.Server.Weapons.Ranged.Systems.GunSystem>().GetAmmoCapacity(sGun),
                Is.EqualTo(4));
            Assert.That(client.System<Content.Client.Weapons.Ranged.Systems.GunSystem>().GetAmmoCount(cGun),
                Is.Zero);
            Assert.That(client.System<Content.Client.Weapons.Ranged.Systems.GunSystem>().GetAmmoCapacity(cGun),
                Is.EqualTo(4));
        });

        await client.WaitPost(() =>
        {
            var target = new EntityCoordinates(cPlayer, Vector2.UnitX * 2);
            var targetCoordinates = cEntMan.GetNetCoordinates(target);
            var predictedProjectiles = client.System<GunPredictionSystem>().ShootRequested(
                cEntMan.GetNetEntity(cGun),
                targetCoordinates,
                null,
                client.Session!);

            Assert.That(predictedProjectiles, Is.Null);
            cEntMan.RaisePredictiveEvent(new RequestShootEvent
            {
                Gun = cEntMan.GetNetEntity(cGun),
                Coordinates = targetCoordinates,
                Shot = [],
                LastRealTick = default,
            });
        });

        await pair.RunTicksSync(5);
        Assert.Multiple(() =>
        {
            Assert.That(GetSprayFuel(sEntMan, sGun), Is.EqualTo(initialFuel));
            Assert.That(GetSprayFuel(cEntMan, cGun), Is.EqualTo(initialFuel));
            Assert.That(sEntMan.GetComponent<GunComponent>(sGun).SpreadSequence, Is.Zero);
            Assert.That(cEntMan.GetComponent<GunComponent>(cGun).SpreadSequence, Is.Zero);
            Assert.That(sEntMan.GetComponent<GunComponent>(sGun).ShotCounter, Is.Zero);
            Assert.That(cEntMan.GetComponent<GunComponent>(cGun).ShotCounter, Is.Zero);
            Assert.That(CountPrototype(sEntMan, SprayVaporPrototype), Is.Zero);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ThreePredictedRequestsCreateExactlyThreeCorrelatedProjectiles()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });
        var server = pair.Server;
        var client = pair.Client;
        var sEntMan = server.EntMan;
        var cEntMan = client.EntMan;
        var playerManager = server.ResolveDependency<IPlayerManager>();
        var serverSession = playerManager.Sessions.Single();
        var map = await pair.CreateTestMap();
        EntityUid sPlayer = default;
        EntityUid sGun = default;

        await server.WaitPost(() =>
        {
            sPlayer = sEntMan.SpawnEntity("MobHuman", map.GridCoords);
            Assert.That(playerManager.SetAttachedEntity(serverSession, sPlayer), Is.True);
            sGun = sEntMan.SpawnEntity("RMCGunResponsivenessBurstGun", map.GridCoords);
            Assert.That(server.System<SharedHandsSystem>().TryPickup(sPlayer, sGun), Is.True);
            server.System<SharedCombatModeSystem>().SetInCombatMode(sPlayer, true);
        });
        await pair.RunTicksSync(5);

        var cPlayer = client.Session?.AttachedEntity ??
            throw new AssertionException("The client must have an attached entity.");
        var cGun = cEntMan.GetEntity(sEntMan.GetNetEntity(sGun));
        var predictedProjectiles = new List<EntityUid>(3);
        var serverDirections = new Dictionary<int, Vector2>();

        for (var i = 0; i < 3; i++)
        {
            await client.WaitPost(() =>
            {
                var target = new EntityCoordinates(cPlayer, Vector2.UnitX * 100);
                var netTarget = cEntMan.GetNetCoordinates(target);
                var shot = client.System<GunPredictionSystem>().ShootRequested(
                    cEntMan.GetNetEntity(cGun),
                    netTarget,
                    null,
                    client.Session!,
                    continuous: false);

                Assert.That(shot, Has.Count.EqualTo(1));
                var projectile = shot!.Single();
                predictedProjectiles.Add(projectile);
                cEntMan.RaisePredictiveEvent(new RequestShootEvent
                {
                    Gun = cEntMan.GetNetEntity(cGun),
                    Coordinates = netTarget,
                    Shot = [projectile.Id],
                    Continuous = false,
                    LastRealTick = default,
                });
            });

            await pair.RunTicksSync(3);
        }

        Assert.That(predictedProjectiles.Select(projectile => projectile.Id), Is.Unique);
        await server.WaitAssertion(() =>
        {
            var allProjectiles = new List<EntityUid>();
            serverDirections.Clear();
            var query = sEntMan.EntityQueryEnumerator<ProjectileComponent, MetaDataComponent>();
            while (query.MoveNext(out var projectile, out _, out var metadata))
            {
                if (!metadata.Deleted && metadata.EntityPrototype?.ID == BurstProjectilePrototype)
                {
                    allProjectiles.Add(projectile);
                    var prediction = sEntMan.GetComponent<PredictedProjectileServerComponent>(projectile);
                    serverDirections[prediction.ClientId] =
                        sEntMan.GetComponent<TransformComponent>(projectile).LocalRotation.ToVec();
                }
            }

            var correlations = allProjectiles
                .Select(projectile => sEntMan.TryGetComponent<PredictedProjectileServerComponent>(
                    projectile,
                    out var prediction)
                    ? prediction.ClientId
                    : (int?) null)
                .ToList();
            Assert.Multiple(() =>
            {
                Assert.That(sEntMan.GetComponent<BasicEntityAmmoProviderComponent>(sGun).Count, Is.Zero);
                Assert.That(sEntMan.GetComponent<GunComponent>(sGun).SpreadSequence, Is.EqualTo(3));
                Assert.That(allProjectiles, Has.Count.EqualTo(3));
                Assert.That(correlations, Has.All.Not.Null);
                Assert.That(correlations.Select(id => id!.Value), Is.EquivalentTo(
                    predictedProjectiles.Select(projectile => projectile.Id)));
            });
        });

        await client.WaitAssertion(() =>
        {
            Assert.That(cEntMan.GetComponent<GunComponent>(cGun).SpreadSequence, Is.EqualTo(3));
            Assert.That(serverDirections, Has.Count.EqualTo(3));
            foreach (var projectile in predictedProjectiles)
            {
                Assert.That(serverDirections, Does.ContainKey(projectile.Id));
                var clientDirection = cEntMan.GetComponent<TransformComponent>(projectile).LocalRotation.ToVec();
                Assert.That(
                    Vector2.Distance(clientDirection, serverDirections[projectile.Id]),
                    Is.LessThan(0.0001f),
                    $"Spread diverged for predicted projectile {projectile.Id}.");
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MissingBurstRequestsUseServerFallback()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });
        var server = pair.Server;
        var client = pair.Client;
        var sEntMan = server.EntMan;
        var cEntMan = client.EntMan;
        var playerManager = server.ResolveDependency<IPlayerManager>();
        var serverSession = playerManager.Sessions.Single();
        var map = await pair.CreateTestMap();
        EntityUid sPlayer = default;
        EntityUid sGun = default;

        await server.WaitPost(() =>
        {
            sPlayer = sEntMan.SpawnEntity("MobHuman", map.GridCoords);
            Assert.That(playerManager.SetAttachedEntity(serverSession, sPlayer), Is.True);
            sGun = sEntMan.SpawnEntity("RMCGunResponsivenessBurstGun", map.GridCoords);
            Assert.That(server.System<SharedHandsSystem>().TryPickup(sPlayer, sGun), Is.True);
            server.System<SharedCombatModeSystem>().SetInCombatMode(sPlayer, true);
        });
        await pair.RunTicksSync(5);

        var cPlayer = client.Session?.AttachedEntity ??
            throw new AssertionException("The client must have an attached entity.");
        var cGun = cEntMan.GetEntity(sEntMan.GetNetEntity(sGun));
        EntityUid predictedProjectile = default;

        await client.WaitPost(() =>
        {
            var target = new EntityCoordinates(cPlayer, Vector2.UnitX * 100);
            var netTarget = cEntMan.GetNetCoordinates(target);
            var shot = client.System<GunPredictionSystem>().ShootRequested(
                cEntMan.GetNetEntity(cGun),
                netTarget,
                null,
                client.Session!,
                continuous: false);

            Assert.That(shot, Has.Count.EqualTo(1));
            predictedProjectile = shot!.Single();
            cEntMan.RaisePredictiveEvent(new RequestShootEvent
            {
                Gun = cEntMan.GetNetEntity(cGun),
                Coordinates = netTarget,
                Shot = [predictedProjectile.Id],
                Continuous = false,
                LastRealTick = default,
            });
        });

        // Let the first request reach the server, then stop ticking the client to simulate
        // the remaining burst requests being lost or the predicting client stalling.
        await server.WaitRunTicks(2);
        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(sEntMan.GetComponent<BasicEntityAmmoProviderComponent>(sGun).Count, Is.EqualTo(2));
                Assert.That(sEntMan.GetComponent<GunComponent>(sGun).BurstActivated, Is.True);
            });
        });

        await server.WaitRunTicks(90);
        await server.WaitAssertion(() =>
        {
            var allProjectiles = new List<EntityUid>();
            var query = sEntMan.EntityQueryEnumerator<ProjectileComponent, MetaDataComponent>();
            while (query.MoveNext(out var projectile, out _, out var metadata))
            {
                if (!metadata.Deleted && metadata.EntityPrototype?.ID == BurstProjectilePrototype)
                    allProjectiles.Add(projectile);
            }

            var correlations = allProjectiles
                .Select(projectile => sEntMan.TryGetComponent<PredictedProjectileServerComponent>(
                    projectile,
                    out var prediction)
                    ? prediction.ClientId
                    : (int?) null)
                .ToList();
            Assert.Multiple(() =>
            {
                Assert.That(sEntMan.GetComponent<BasicEntityAmmoProviderComponent>(sGun).Count, Is.Zero);
                Assert.That(sEntMan.GetComponent<GunComponent>(sGun).BurstActivated, Is.False);
                Assert.That(allProjectiles, Has.Count.EqualTo(3));
                Assert.That(correlations.Count(id => id == predictedProjectile.Id), Is.EqualTo(1));
                Assert.That(correlations.Count(id => id == null), Is.EqualTo(2));
            });
        });

        await pair.CleanReturnAsync();
    }

    private static FixedPoint2 GetSprayFuel(IEntityManager entityManager, EntityUid gun)
    {
        var solutions = entityManager.System<SharedSolutionContainerSystem>();
        Assert.That(solutions.TryGetSolution(gun, "spray", out _, out var solution), Is.True);
        return solution!.Volume;
    }

    private static int CountPrototype(IEntityManager entityManager, string prototype)
    {
        var count = 0;
        var query = entityManager.EntityQueryEnumerator<MetaDataComponent>();
        while (query.MoveNext(out _, out var metadata))
        {
            if (!metadata.Deleted && metadata.EntityPrototype?.ID == prototype)
                count++;
        }

        return count;
    }
}
