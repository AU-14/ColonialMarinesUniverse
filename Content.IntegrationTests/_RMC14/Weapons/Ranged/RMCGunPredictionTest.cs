#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client._RMC14.Weapons.Ranged.Prediction;
using Content.Shared._RMC14.Weapons.Ranged.Prediction;
using Content.Shared.CombatMode;
using Content.Shared.Effects;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Client.GameObjects;
using Robust.Client.GameStates;
using Robust.Client.Physics;
using Robust.Client.Timing;
using Robust.Server.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using ClientGunSystem = Content.Client.Weapons.Ranged.Systems.GunSystem;
using ServerGunSystem = Content.Server.Weapons.Ranged.Systems.GunSystem;
using ServerPredictedHitReportLimiter = Content.Server._RMC14.Weapons.Ranged.Prediction.PredictedHitReportLimiter;

namespace Content.IntegrationTests._RMC14.Weapons.Ranged;

[TestFixture]
[TestOf(typeof(GunPredictionSystem))]
public sealed class RMCGunPredictionTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          parent: BaseItem
          id: RMCGunPredictionTestGun
          components:
          - type: Gun
            fireRate: 1
            projectileSpeed: 0.01
            resetOnHandSelected: false
            soundGunshot: null
            soundEmpty: null
          - type: BasicEntityAmmoProvider
            proto: RMCGunPredictionTestProjectile
            capacity: 2
            count: 2

        - type: entity
          parent: BaseBullet
          id: RMCGunPredictionTestProjectile
          components:
          - type: Projectile
            deleteOnCollide: false
          - type: TimedDespawn
            lifetime: 10
          - type: ProjectileIFF
            factions:
            - FactionMarine

        - type: entity
          parent: BaseBullet
          id: RMCGunPredictionRollbackProjectile

        - type: entity
          parent: BaseItem
          id: RMCGunPredictionPhysicalAmmoGun
          components:
          - type: Gun
            fireRate: 1
            projectileSpeed: 30
            resetOnHandSelected: false
            soundGunshot: null
            soundEmpty: null
          - type: BallisticAmmoProvider
            capacity: 1
            whitelist:
              components:
              - Ammo

        - type: entity
          parent: BaseBullet
          id: RMCGunPredictionPhysicalAmmoProjectile
          components:
          - type: Ammo
          - type: Projectile
            deleteOnCollide: false
            impactEffect: null
            soundHit: null

        - type: entity
          parent: RMCGunPredictionTestGun
          id: RMCGunPredictionImpactTestGun
          components:
          - type: Gun
            projectileSpeed: 30
          - type: BasicEntityAmmoProvider
            proto: RMCGunPredictionImpactTestProjectile

        - type: entity
          parent: RMCGunPredictionTestProjectile
          id: RMCGunPredictionImpactTestProjectile
          components:
          - type: Projectile
            impactEffect: RMCGunPredictionTestImpact

        - type: entity
          parent: RMCGunPredictionTestGun
          id: RMCGunPredictionReflectTestGun
          components:
          - type: BasicEntityAmmoProvider
            proto: RMCGunPredictionReflectTestProjectile
            capacity: 1
            count: 1

        - type: entity
          parent: BaseBullet
          id: RMCGunPredictionReflectTestProjectile
          components:
          - type: Projectile
            deleteOnCollide: false
            impactEffect: null
            soundHit: null
          - type: TimedDespawn
            lifetime: 10

        - type: entity
          parent: RMCGunPredictionHardTarget
          id: RMCGunPredictionReflectTarget
          components:
          - type: RMCReflective
            chance: 1
            angle: 0

        - type: entity
          id: RMCGunPredictionTestImpact

        - type: entity
          parent: MobHuman
          id: RMCGunPredictionFriendlyTarget
          components:
          - type: UserIFF
            factions:
            - FactionMarine

        - type: entity
          id: RMCGunPredictionSoftTarget
          components:
          - type: Physics
            bodyType: Static
          - type: Fixtures
            fixtures:
              sensor:
                shape: !type:PhysShapeCircle
                  radius: 1
                hard: false
                layer:
                - BulletImpassable
          - type: LagCompensation

        - type: entity
          id: RMCGunPredictionMixedTarget
          components:
          - type: Physics
            bodyType: Static
          - type: Fixtures
            fixtures:
              sensor:
                shape: !type:PhysShapeCircle
                  radius: 4
                hard: false
                layer:
                - BulletImpassable
              body:
                shape: !type:PhysShapeCircle
                  radius: 0.25
                hard: true
                layer:
                - BulletImpassable
          - type: LagCompensation

        - type: entity
          id: RMCGunPredictionHardTarget
          components:
          - type: Sprite
          - type: Damageable
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
          id: RMCGunPredictionStaticTarget
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

        - type: entity
          parent: RMCGunPredictionTestGun
          id: RMCGunPredictionTestIgnoredGun
          components:
          - type: GunIgnorePrediction

        - type: entity
          parent: BaseItem
          id: RMCGunPredictionPenetrationGun
          components:
          - type: Gun
            fireRate: 30
            projectileSpeed: 0.01
            resetOnHandSelected: false
            soundGunshot: null
            soundEmpty: null
          - type: BasicEntityAmmoProvider
            proto: RMCGunPredictionPenetrationProjectile
            capacity: 1
            count: 1

        - type: entity
          parent: BaseBullet
          id: RMCGunPredictionPenetrationProjectile
          components:
          - type: Projectile
            deleteOnCollide: true
            impactEffect: null
            soundHit: null
            damage:
              types:
                Structural: 25
            penetrationThreshold: 60
            penetrationDamageTypeRequirement:
            - Structural
          - type: TimedDespawn
            lifetime: 10

        - type: entity
          id: RMCGunPredictionPenetrationTarget
          components:
          - type: Sprite
          - type: Damageable
          - type: Injurable
            damageContainer: StructuralInorganic
          - type: Destructible
            thresholds:
            - trigger:
                !type:DamageTrigger
                damage: 20
              behaviors:
              - !type:DoActsBehavior
                acts: [ "Destruction" ]
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
        """;

    [Test]
    public async Task PredictedCollisionRollbackDoesNotInvalidateContacts()
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
        EntityUid clientProjectile = default;

        await server.WaitPost(() =>
        {
            var player = sEntMan.SpawnEntity("MobHuman", map.GridCoords);
            Assert.That(playerManager.SetAttachedEntity(serverSession, player), Is.True);
        });
        await pair.RunTicksSync(5);

        var cPlayer = client.Session?.AttachedEntity ??
            throw new AssertionException("The client must have an attached entity.");

        await client.WaitAssertion(() =>
        {
            Assert.That(cEntMan.HasComponent<PredictedPhysicsComponent>(cPlayer), Is.True);

            clientProjectile = cEntMan.SpawnEntity(
                "RMCGunPredictionRollbackProjectile",
                cEntMan.GetComponent<TransformComponent>(cPlayer).Coordinates);
            cEntMan.EnsureComponent<PredictedProjectileClientComponent>(clientProjectile);
            cEntMan.EnsureComponent<PredictedPhysicsComponent>(clientProjectile);
#pragma warning disable RA0002
            cEntMan.GetComponent<PhysicsComponent>(clientProjectile).Predict = true;
#pragma warning restore RA0002

            var gameState = (ClientGameStateManager) client.ResolveDependency<IClientGameStateManager>();
            Assert.That(gameState.PredictionNeedsResetting, Is.True);
            Assert.DoesNotThrow(gameState.ResetPredictedEntities);
            Assert.Multiple(() =>
            {
                Assert.That(cEntMan.EntityExists(clientProjectile), Is.True);
                Assert.That(cEntMan.IsQueuedForDeletion(clientProjectile), Is.False);
                Assert.That(cEntMan.GetComponent<ProjectileComponent>(clientProjectile).ProjectileSpent, Is.False);
            });
        });

        await pair.RunTicksSync(2);
        await client.WaitAssertion(() => Assert.That(cEntMan.EntityExists(clientProjectile), Is.False));

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PhysicalPredictedProjectileSurvivesCollisionRollback()
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
        EntityUid sGun = default;
        EntityUid sProjectile = default;
        EntityUid sTarget = default;

        await server.WaitPost(() =>
        {
            var player = sEntMan.SpawnEntity("MobHuman", map.GridCoords);
            Assert.That(playerManager.SetAttachedEntity(serverSession, player), Is.True);
            sGun = sEntMan.SpawnEntity("RMCGunPredictionPhysicalAmmoGun", map.GridCoords);
            sProjectile = sEntMan.SpawnEntity("RMCGunPredictionPhysicalAmmoProjectile", map.GridCoords);
            sTarget = sEntMan.SpawnEntity(
                "RMCGunPredictionHardTarget",
                map.GridCoords.Offset(Vector2.UnitX * 2));

            var gun = server.System<ServerGunSystem>();
            var provider = sEntMan.GetComponent<BallisticAmmoProviderComponent>(sGun);
            Assert.That(gun.TryBallisticInsert((sGun, provider), sProjectile, null, true), Is.True);
            Assert.That(server.System<SharedHandsSystem>().TryPickup(player, sGun), Is.True);
            server.System<SharedCombatModeSystem>().SetInCombatMode(player, true);
        });
        await pair.RunTicksSync(10);

        var cPlayer = client.Session?.AttachedEntity ??
            throw new AssertionException("The client must have an attached entity.");
        var cGun = cEntMan.GetEntity(sEntMan.GetNetEntity(sGun));
        var cProjectile = cEntMan.GetEntity(sEntMan.GetNetEntity(sProjectile));
        var cTarget = cEntMan.GetEntity(sEntMan.GetNetEntity(sTarget));
        List<EntityUid>? projectiles = null;

        await client.WaitPost(() =>
        {
            var target = cEntMan.GetComponent<TransformComponent>(cTarget).Coordinates;
            projectiles = client.System<GunPredictionSystem>().ShootRequested(
                cEntMan.GetNetEntity(cGun),
                cEntMan.GetNetCoordinates(target),
                cEntMan.GetNetEntity(cTarget),
                client.Session!);

            cEntMan.RaisePredictiveEvent(new RequestShootEvent
            {
                Gun = cEntMan.GetNetEntity(cGun),
                Coordinates = cEntMan.GetNetCoordinates(target),
                Target = cEntMan.GetNetEntity(cTarget),
                Shot = projectiles?.Select(projectile => projectile.Id).ToList(),
                LastRealTick = default,
            });
        });

        Assert.That(projectiles, Is.EqualTo(new[] { cProjectile }));
        await client.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(cEntMan.IsClientSide(cProjectile), Is.False);
                Assert.That(cEntMan.HasComponent<PredictedProjectileClientComponent>(cProjectile), Is.True);
            });
        });

        await client.WaitPost(() =>
        {
            var physics = cEntMan.GetComponent<PhysicsComponent>(cProjectile);
            var projectile = cEntMan.GetComponent<ProjectileComponent>(cProjectile);
            var expectedVelocity = physics.LinearVelocity;
            Assert.Multiple(() =>
            {
                Assert.That(expectedVelocity.LengthSquared(), Is.GreaterThan(0));
                Assert.That(physics.BodyStatus, Is.EqualTo(BodyStatus.InAir));
                Assert.That(projectile.Weapon, Is.EqualTo(cGun));
                Assert.That(projectile.Shooter, Is.EqualTo(cPlayer));
            });

            var gameState = (ClientGameStateManager) client.ResolveDependency<IClientGameStateManager>();
            Assert.That(gameState.PredictionNeedsResetting, Is.True);
            gameState.ResetPredictedEntities();

            physics = cEntMan.GetComponent<PhysicsComponent>(cProjectile);
            projectile = cEntMan.GetComponent<ProjectileComponent>(cProjectile);
            Assert.Multiple(() =>
            {
                Assert.That(physics.LinearVelocity, Is.EqualTo(Vector2.Zero));
                Assert.That(projectile.Weapon, Is.Null);
                Assert.That(projectile.Shooter, Is.Null);
            });

            var timing = client.ResolveDependency<IClientGameTiming>();
            using (timing.StartPastPredictionArea())
            {
                var target = cEntMan.GetComponent<TransformComponent>(cTarget).Coordinates;
                var replayedProjectiles = client.System<GunPredictionSystem>().ShootRequested(
                    cEntMan.GetNetEntity(cGun),
                    cEntMan.GetNetCoordinates(target),
                    cEntMan.GetNetEntity(cTarget),
                    client.Session!);
                Assert.That(replayedProjectiles, Is.Empty);
            }

            physics = cEntMan.GetComponent<PhysicsComponent>(cProjectile);
            projectile = cEntMan.GetComponent<ProjectileComponent>(cProjectile);
            Assert.Multiple(() =>
            {
                Assert.That(Vector2.Distance(physics.LinearVelocity, expectedVelocity), Is.LessThan(0.0001f));
                Assert.That(physics.BodyStatus, Is.EqualTo(BodyStatus.InAir));
                Assert.That(projectile.Weapon, Is.EqualTo(cGun));
                Assert.That(projectile.Shooter, Is.EqualTo(cPlayer));
            });
        });

        await client.WaitRunTicks(3);
        await client.WaitAssertion(() =>
        {
            Assert.That(
                cEntMan.GetComponent<ProjectileComponent>(cProjectile).ProjectileSpent,
                Is.True,
                "The physical projectile must collide locally before rollback.");
        });

        for (var i = 0; i < 5; i++)
        {
            await server.WaitRunTicks(1);
            await client.WaitRunTicks(1);
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ProjectileShotsArePredictedAndCorrelated()
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
        EntityUid sProjectile = default;
        Vector2 clientVelocity = default;
        Vector2 serverVelocity = default;

        await server.WaitPost(() =>
        {
            sPlayer = sEntMan.SpawnEntity("MobHuman", map.GridCoords);
            Assert.That(playerManager.SetAttachedEntity(serverSession, sPlayer), Is.True);
            sGun = sEntMan.SpawnEntity("RMCGunPredictionTestGun", map.GridCoords);

            var hands = server.System<SharedHandsSystem>();
            Assert.That(hands.TryPickup(sPlayer, sGun), Is.True);
            server.System<SharedCombatModeSystem>().SetInCombatMode(sPlayer, true);
        });
        await pair.RunTicksSync(5);

        var cPlayer = client.Session?.AttachedEntity ??
            throw new AssertionException("The client must have an attached entity.");
        var cGun = cEntMan.GetEntity(sEntMan.GetNetEntity(sGun));
        List<EntityUid>? projectiles = null;

        await client.WaitPost(() =>
        {
            var target = new EntityCoordinates(cPlayer, Vector2.UnitX * 10);
            projectiles = client.System<GunPredictionSystem>().ShootRequested(
                cEntMan.GetNetEntity(cGun),
                cEntMan.GetNetCoordinates(target),
                null,
                client.Session!);

            cEntMan.RaisePredictiveEvent(new RequestShootEvent
            {
                Gun = cEntMan.GetNetEntity(cGun),
                Coordinates = cEntMan.GetNetCoordinates(target),
                Shot = projectiles?.Select(projectile => projectile.Id).ToList(),
                LastRealTick = default,
            });
        });

        await pair.RunTicksSync(3);

        Assert.That(projectiles, Has.Count.EqualTo(1));
        var projectile = projectiles!.Single();
        Assert.Multiple(() =>
        {
            Assert.That(cEntMan.EntityExists(projectile), Is.True);
            Assert.That(cEntMan.IsClientSide(projectile), Is.True);
            Assert.That(cEntMan.HasComponent<PredictedProjectileClientComponent>(projectile), Is.True);
        });
        await client.WaitAssertion(() =>
        {
            clientVelocity = cEntMan.GetComponent<PhysicsComponent>(projectile).LinearVelocity;
        });

        await server.WaitAssertion(() =>
        {
            var ammo = sEntMan.GetComponent<BasicEntityAmmoProviderComponent>(sGun);
            Assert.That(ammo.Count, Is.EqualTo(1));
            var query = sEntMan.EntityQueryEnumerator<PredictedProjectileServerComponent>();
            Assert.That(query.MoveNext(out sProjectile, out var predicted), Is.True);
            Assert.That(predicted, Is.Not.Null);
            Assert.That(predicted!.ClientId, Is.EqualTo(projectile.Id));
            Assert.That(query.MoveNext(out _, out _), Is.False);
            Assert.That(sEntMan.GetNetEntity(sProjectile), Is.Not.EqualTo(NetEntity.Invalid));
            serverVelocity = sEntMan.GetComponent<PhysicsComponent>(sProjectile).LinearVelocity;
        });
        Assert.That(
            Vector2.Dot(Vector2.Normalize(clientVelocity), Vector2.Normalize(serverVelocity)),
            Is.GreaterThan(0.999999f));

        await server.WaitPost(() => sEntMan.DeleteEntity(sProjectile));
        await pair.RunTicksSync(3);
        await client.WaitAssertion(() => Assert.That(cEntMan.EntityExists(projectile), Is.False));

        EntityUid sIgnoredPlayer = default;
        EntityUid sIgnoredGun = default;
        await server.WaitPost(() =>
        {
            sIgnoredPlayer = sEntMan.SpawnEntity("MobHuman", map.GridCoords);
            Assert.That(playerManager.SetAttachedEntity(serverSession, sIgnoredPlayer), Is.True);
            sIgnoredGun = sEntMan.SpawnEntity("RMCGunPredictionTestIgnoredGun", map.GridCoords);
            Assert.That(server.System<SharedHandsSystem>().TryPickup(sIgnoredPlayer, sIgnoredGun), Is.True);
            server.System<SharedCombatModeSystem>().SetInCombatMode(sIgnoredPlayer, true);
        });
        await pair.RunTicksSync(5);

        var cIgnoredPlayer = client.Session?.AttachedEntity ??
            throw new AssertionException("The client must have an attached entity.");
        var cIgnoredGun = cEntMan.GetEntity(sEntMan.GetNetEntity(sIgnoredGun));
        List<EntityUid>? ignoredProjectiles = null;
        await client.WaitPost(() =>
        {
            var target = new EntityCoordinates(cIgnoredPlayer, Vector2.UnitX * 10);
            ignoredProjectiles = client.System<GunPredictionSystem>().ShootRequested(
                cEntMan.GetNetEntity(cIgnoredGun),
                cEntMan.GetNetCoordinates(target),
                null,
                client.Session!);

            cEntMan.RaisePredictiveEvent(new RequestShootEvent
            {
                Gun = cEntMan.GetNetEntity(cIgnoredGun),
                Coordinates = cEntMan.GetNetCoordinates(target),
                Shot = [int.MaxValue],
                LastRealTick = default,
            });
        });
        await pair.RunTicksSync(3);

        Assert.That(ignoredProjectiles, Is.Empty);
        await server.WaitAssertion(() =>
        {
            var ammo = sEntMan.GetComponent<BasicEntityAmmoProviderComponent>(sIgnoredGun);
            Assert.That(ammo.Count, Is.EqualTo(1));
            var predictions = sEntMan.EntityQueryEnumerator<PredictedProjectileServerComponent>();
            Assert.That(predictions.MoveNext(out _, out _), Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RemovingOlderAuthorityPreservesNewerCorrelation()
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
        const int clientProjectileId = int.MaxValue - 1;
        EntityUid serverPlayer = default;
        EntityUid serverGun = default;
        EntityUid oldAuthority = default;
        EntityUid newAuthority = default;
        EntityUid target = default;

        await server.WaitPost(() =>
        {
            serverPlayer = sEntMan.SpawnEntity("MobHuman", map.GridCoords);
            Assert.That(playerManager.SetAttachedEntity(serverSession, serverPlayer), Is.True);
            serverGun = sEntMan.SpawnEntity("RMCGunPredictionTestGun", map.GridCoords);
            Assert.That(server.System<SharedHandsSystem>().TryPickup(serverPlayer, serverGun), Is.True);
            server.System<SharedCombatModeSystem>().SetInCombatMode(serverPlayer, true);
        });
        await pair.RunTicksSync(5);

        var clientPlayer = client.Session?.AttachedEntity ??
            throw new AssertionException("The client must have an attached entity.");
        var clientGun = cEntMan.GetEntity(sEntMan.GetNetEntity(serverGun));

        async Task RequestShot()
        {
            await client.WaitPost(() =>
            {
                var coordinates = new EntityCoordinates(clientPlayer, Vector2.UnitX * 10);
                cEntMan.RaisePredictiveEvent(new RequestShootEvent
                {
                    Gun = cEntMan.GetNetEntity(clientGun),
                    Coordinates = cEntMan.GetNetCoordinates(coordinates),
                    Shot = [clientProjectileId],
                    LastRealTick = default,
                });
            });
            await pair.RunTicksSync(3);
        }

        await RequestShot();
        await server.WaitAssertion(() =>
        {
            var query = sEntMan.EntityQueryEnumerator<PredictedProjectileServerComponent>();
            Assert.That(query.MoveNext(out oldAuthority, out var prediction), Is.True);
            Assert.That(prediction!.ClientId, Is.EqualTo(clientProjectileId));
            Assert.That(query.MoveNext(out _, out _), Is.False);
        });

        await pair.RunTicksSync(65);
        await RequestShot();
        await server.WaitPost(() =>
        {
            var correlated = new List<EntityUid>();
            var query = sEntMan.EntityQueryEnumerator<PredictedProjectileServerComponent>();
            while (query.MoveNext(out var projectile, out var prediction))
            {
                if (prediction.ClientId == clientProjectileId)
                    correlated.Add(projectile);
            }

            Assert.That(correlated, Has.Count.EqualTo(2));
            newAuthority = correlated.Single(projectile => projectile != oldAuthority);
            target = sEntMan.SpawnEntity(
                "RMCGunPredictionHardTarget",
                sEntMan.GetComponent<TransformComponent>(newAuthority).Coordinates.Offset(Vector2.UnitX));
            sEntMan.DeleteEntity(oldAuthority);
        });
        await pair.RunTicksSync(5);

        var clientTarget = cEntMan.GetEntity(sEntMan.GetNetEntity(target));
        await client.WaitPost(() =>
        {
            var targetCoordinates = client.System<SharedTransformSystem>().GetMapCoordinates(clientTarget);
            client.System<GunPredictionSystem>().ReportPredictedHit(new PredictedProjectileHitEvent(
                clientProjectileId,
                [(cEntMan.GetNetEntity(clientTarget), targetCoordinates)]));
        });
        await pair.RunTicksSync(3);

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(sEntMan.EntityExists(oldAuthority), Is.False);
                Assert.That(sEntMan.EntityExists(newAuthority), Is.True);
                Assert.That(
                    sEntMan.GetComponent<PredictedProjectileServerComponent>(newAuthority).Hit,
                    Is.True);
                Assert.That(sEntMan.GetComponent<ProjectileComponent>(newAuthority).ProjectileSpent, Is.True);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PredictedImpactEffectIsDeduplicatedWhenPredictionArrivesFirst()
    {
        await PredictedImpactEffectIsDeduplicated(true);
    }

    [Test]
    public async Task PredictedImpactEffectIsDeduplicatedWhenAuthoritativeEventArrivesFirst()
    {
        await PredictedImpactEffectIsDeduplicated(false);
    }

    [Test]
    public async Task ReflectedPredictionHandsPresentationBackToAuthority()
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
            sGun = sEntMan.SpawnEntity("RMCGunPredictionReflectTestGun", map.GridCoords);
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
        await pair.RunTicksSync(3);

        Assert.That(predictedProjectiles, Has.Count.EqualTo(1));
        var predictedProjectile = predictedProjectiles!.Single();
        EntityUid serverProjectile = default;
        await server.WaitPost(() =>
        {
            var query = sEntMan.EntityQueryEnumerator<PredictedProjectileServerComponent>();
            Assert.That(query.MoveNext(out serverProjectile, out var prediction), Is.True);
            Assert.That(prediction!.ClientId, Is.EqualTo(predictedProjectile.Id));

            var reflector = sEntMan.SpawnEntity(
                "RMCGunPredictionReflectTarget",
                sEntMan.GetComponent<TransformComponent>(serverProjectile).Coordinates);
            var projectile = sEntMan.GetComponent<ProjectileComponent>(serverProjectile);
            var physics = sEntMan.GetComponent<PhysicsComponent>(serverProjectile);
            var originalVelocity = physics.LinearVelocity;
            var collided = server.System<Content.Server.Projectiles.ProjectileSystem>().ProjectileCollide(
                (serverProjectile, projectile, physics),
                reflector,
                true);

            Assert.Multiple(() =>
            {
                Assert.That(collided, Is.False);
                Assert.That(projectile.ProjectileSpent, Is.False);
                Assert.That(projectile.Shooter, Is.EqualTo(reflector));
                Assert.That(Vector2.Dot(originalVelocity, physics.LinearVelocity), Is.LessThan(0));
                Assert.That(prediction.Hit, Is.True);
            });
        });
        await pair.RunTicksSync(3);

        var authoritativeProjectile = cEntMan.GetEntity(sEntMan.GetNetEntity(serverProjectile));
        await client.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(cEntMan.EntityExists(predictedProjectile), Is.False);
                Assert.That(cEntMan.EntityExists(authoritativeProjectile), Is.True);
                Assert.That(cEntMan.IsClientSide(authoritativeProjectile), Is.False);
                Assert.That(cEntMan.GetComponent<SpriteComponent>(authoritativeProjectile).Visible, Is.True);
            });
        });

        await pair.CleanReturnAsync();
    }

    private async Task PredictedImpactEffectIsDeduplicated(bool predictionFirst)
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
            sGun = sEntMan.SpawnEntity("RMCGunPredictionImpactTestGun", map.GridCoords);
            Assert.That(server.System<SharedHandsSystem>().TryPickup(sPlayer, sGun), Is.True);
            server.System<SharedCombatModeSystem>().SetInCombatMode(sPlayer, true);
            sTarget = sEntMan.SpawnEntity(
                "RMCGunPredictionHardTarget",
                map.GridCoords.Offset(Vector2.UnitX * 2));
        });
        await pair.RunTicksSync(5);

        var cPlayer = client.Session?.AttachedEntity ??
            throw new AssertionException("Client player was not attached");
        var cGun = cEntMan.GetEntity(sEntMan.GetNetEntity(sGun));
        var cTarget = cEntMan.GetEntity(sEntMan.GetNetEntity(sTarget));
        List<EntityUid>? projectiles = null;

        int CountImpacts()
        {
            var impacts = 0;
            var query = cEntMan.EntityQueryEnumerator<MetaDataComponent>();
            while (query.MoveNext(out _, out var metadata))
            {
                if (!metadata.Deleted && metadata.EntityPrototype?.ID == "RMCGunPredictionTestImpact")
                    impacts++;
            }

            return impacts;
        }

        await client.WaitPost(() =>
        {
            var target = cEntMan.GetComponent<TransformComponent>(cTarget).Coordinates;
            projectiles = client.System<GunPredictionSystem>().ShootRequested(
                cEntMan.GetNetEntity(cGun),
                cEntMan.GetNetCoordinates(target),
                cEntMan.GetNetEntity(cTarget),
                client.Session!);

            cEntMan.RaisePredictiveEvent(new RequestShootEvent
            {
                Gun = cEntMan.GetNetEntity(cGun),
                Coordinates = cEntMan.GetNetCoordinates(target),
                Target = cEntMan.GetNetEntity(cTarget),
                Shot = projectiles?.Select(projectile => projectile.Id).ToList(),
                LastRealTick = default,
            });

            if (!predictionFirst)
            {
                var projectile = projectiles!.Single();
                var projectileComponent = cEntMan.GetComponent<ProjectileComponent>(projectile);
                cEntMan.EventBus.RaiseEvent(
                    EventSource.Network,
                    new ImpactEffectEvent(
                        "RMCGunPredictionTestImpact",
                        cEntMan.GetNetCoordinates(target),
                        cEntMan.GetNetEntity(cPlayer),
                        projectile.Id,
                        cEntMan.GetNetEntity(cTarget)));
                cEntMan.EventBus.RaiseEvent(
                    EventSource.Network,
                    new PredictedProjectileImpactFeedbackEvent(
                        projectile.Id,
                        cEntMan.GetNetEntity(cTarget),
                        cEntMan.GetNetCoordinates(target),
                        projectileComponent.Damage,
                        null,
                        projectileComponent.SoundHit,
                        false,
                        true,
                        projectileComponent.DeleteOnCollide,
                        // Keep the synthetic authority-first projectile active so its later local
                        // collision verifies target-specific feedback/effect deduplication.
                        false));
            }
        });

        Assert.That(projectiles, Has.Count.EqualTo(1));
        var clientProjectile = projectiles!.Single();
        if (predictionFirst)
        {
            await client.WaitRunTicks(10);
            await client.WaitAssertion(() =>
            {
                Assert.That(
                    cEntMan.GetComponent<PredictedProjectileClientComponent>(clientProjectile).Hit,
                    Is.True);
                Assert.That(cEntMan.HasComponent<ColorFlashEffectComponent>(cTarget), Is.True);
                Assert.That(CountImpacts(), Is.EqualTo(1));
            });

            await pair.RunTicksSync(10);
            EntityUid matchingProjectile = default;
            await server.WaitAssertion(() =>
            {
                var query = sEntMan.EntityQueryEnumerator<PredictedProjectileServerComponent>();
                while (query.MoveNext(out var projectile, out var predicted))
                {
                    if (predicted.ClientId == clientProjectile.Id)
                        matchingProjectile = projectile;
                }

                Assert.That(matchingProjectile, Is.Not.EqualTo(default(EntityUid)));
                Assert.That(
                    sEntMan.GetComponent<ProjectileComponent>(matchingProjectile).ProjectileSpent,
                    Is.True);
            });
            await client.WaitAssertion(() =>
            {
                var authoritativeProjectile = cEntMan.GetEntity(sEntMan.GetNetEntity(matchingProjectile));
                Assert.Multiple(() =>
                {
                    Assert.That(cEntMan.EntityExists(clientProjectile), Is.False);
                    Assert.That(cEntMan.EntityExists(authoritativeProjectile), Is.True);
                    Assert.That(cEntMan.GetComponent<SpriteComponent>(authoritativeProjectile).Visible, Is.True);
                    Assert.That(CountImpacts(), Is.EqualTo(1));
                });
            });
        }
        else
        {
            await client.WaitAssertion(() =>
            {
                Assert.That(
                    cEntMan.GetComponent<PredictedProjectileClientComponent>(clientProjectile).Hit,
                    Is.False);
                Assert.That(cEntMan.HasComponent<ColorFlashEffectComponent>(cTarget), Is.True);
                Assert.That(CountImpacts(), Is.EqualTo(1));
            });

            await client.WaitRunTicks(10);
            await client.WaitAssertion(() =>
            {
                var prediction = cEntMan.GetComponent<PredictedProjectileClientComponent>(clientProjectile);
                Assert.Multiple(() =>
                {
                    // Authority-first feedback records this target before the local
                    // copy reaches it, so the later contact must be ignored rather
                    // than reported/frozen a second time.
                    Assert.That(prediction.Hit, Is.False);
                    Assert.That(prediction.HitTargets, Does.Contain(cEntMan.GetNetEntity(cTarget)));
                });
                Assert.That(cEntMan.HasComponent<ColorFlashEffectComponent>(cTarget), Is.True);
                Assert.That(CountImpacts(), Is.EqualTo(1));
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RejectedPredictionReconcilesRecoilBeforeNextShot()
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
            sGun = sEntMan.SpawnEntity("RMCGunPredictionTestGun", map.GridCoords);
            Assert.That(server.System<SharedHandsSystem>().TryPickup(sPlayer, sGun), Is.True);
        });
        await pair.RunTicksSync(5);

        var cPlayer = client.Session?.AttachedEntity ??
            throw new AssertionException("The client must have an attached entity.");
        var cGun = cEntMan.GetEntity(sEntMan.GetNetEntity(sGun));
        List<EntityUid>? rejectedProjectiles = null;
        await client.WaitPost(() =>
        {
#pragma warning disable RA0002
            cEntMan.GetComponent<CombatModeComponent>(cPlayer).IsInCombatMode = true;
#pragma warning restore RA0002
            var target = new EntityCoordinates(cPlayer, Vector2.UnitX * 10);
            rejectedProjectiles = client.System<GunPredictionSystem>().ShootRequested(
                cEntMan.GetNetEntity(cGun),
                cEntMan.GetNetCoordinates(target),
                null,
                client.Session!);

            cEntMan.RaisePredictiveEvent(new RequestShootEvent
            {
                Gun = cEntMan.GetNetEntity(cGun),
                Coordinates = cEntMan.GetNetCoordinates(target),
                Shot = rejectedProjectiles?.Select(projectile => projectile.Id).ToList(),
                LastRealTick = default,
            });
        });
        await pair.RunTicksSync(5);

        Assert.That(rejectedProjectiles, Has.Count.EqualTo(1));
        Angle authoritativeAngle = default;
        TimeSpan authoritativeLastFire = default;
        await server.WaitAssertion(() =>
        {
            Assert.That(sEntMan.GetComponent<BasicEntityAmmoProviderComponent>(sGun).Count, Is.EqualTo(2));
            var predictions = sEntMan.EntityQueryEnumerator<PredictedProjectileServerComponent>();
            Assert.That(predictions.MoveNext(out _, out _), Is.False);
            var gun = sEntMan.GetComponent<GunComponent>(sGun);
            authoritativeAngle = gun.CurrentAngle;
            authoritativeLastFire = gun.LastFire;
        });

        await client.WaitAssertion(() =>
        {
            var clientGun = cEntMan.GetComponent<GunComponent>(cGun);
            Assert.Multiple(() =>
            {
                Assert.That(cEntMan.EntityExists(rejectedProjectiles!.Single()), Is.False);
                Assert.That(clientGun.CurrentAngle, Is.EqualTo(authoritativeAngle));
                Assert.That(clientGun.LastFire, Is.EqualTo(authoritativeLastFire));
            });
        });

        await server.WaitPost(() =>
        {
            server.System<SharedCombatModeSystem>().SetInCombatMode(sPlayer, true);
        });
        await pair.RunTicksSync(5);

        List<EntityUid>? acceptedProjectiles = null;
        Vector2 clientVelocity = default;
        await client.WaitPost(() =>
        {
            var target = new EntityCoordinates(cPlayer, Vector2.UnitX * 10);
            acceptedProjectiles = client.System<GunPredictionSystem>().ShootRequested(
                cEntMan.GetNetEntity(cGun),
                cEntMan.GetNetCoordinates(target),
                null,
                client.Session!);
            clientVelocity = cEntMan.GetComponent<PhysicsComponent>(acceptedProjectiles!.Single()).LinearVelocity;

            cEntMan.RaisePredictiveEvent(new RequestShootEvent
            {
                Gun = cEntMan.GetNetEntity(cGun),
                Coordinates = cEntMan.GetNetCoordinates(target),
                Shot = acceptedProjectiles?.Select(projectile => projectile.Id).ToList(),
                LastRealTick = default,
            });
        });
        await pair.RunTicksSync(3);

        Assert.That(acceptedProjectiles, Has.Count.EqualTo(1));
        var acceptedProjectile = acceptedProjectiles!.Single();
        Vector2 serverVelocity = default;
        await server.WaitAssertion(() =>
        {
            var query = sEntMan.EntityQueryEnumerator<PredictedProjectileServerComponent>();
            Assert.That(query.MoveNext(out var serverProjectile, out var predicted), Is.True);
            Assert.That(predicted!.ClientId, Is.EqualTo(acceptedProjectile.Id));
            Assert.That(query.MoveNext(out _, out _), Is.False);
            serverVelocity = sEntMan.GetComponent<PhysicsComponent>(serverProjectile).LinearVelocity;
        });
        Assert.That(
            Vector2.Dot(Vector2.Normalize(clientVelocity), Vector2.Normalize(serverVelocity)),
            Is.GreaterThan(0.999999f));

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PredictedHitsRespectMapsAndCollisionRules()
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
        var otherMap = await pair.CreateTestMap();
        EntityUid sPlayer = default;
        EntityUid sGun = default;

        await server.WaitPost(() =>
        {
            sPlayer = sEntMan.SpawnEntity("MobHuman", map.GridCoords);
            Assert.That(playerManager.SetAttachedEntity(serverSession, sPlayer), Is.True);
            sGun = sEntMan.SpawnEntity("RMCGunPredictionTestGun", map.GridCoords);
            Assert.That(server.System<SharedHandsSystem>().TryPickup(sPlayer, sGun), Is.True);
            server.System<SharedCombatModeSystem>().SetInCombatMode(sPlayer, true);
        });
        await pair.RunTicksSync(5);

        var cPlayer = client.Session?.AttachedEntity ??
            throw new AssertionException("The client must have an attached entity.");
        var cGun = cEntMan.GetEntity(sEntMan.GetNetEntity(sGun));
        List<EntityUid>? projectiles = null;
        await client.WaitPost(() =>
        {
            var target = new EntityCoordinates(cPlayer, Vector2.UnitX * 10);
            projectiles = client.System<GunPredictionSystem>().ShootRequested(
                cEntMan.GetNetEntity(cGun),
                cEntMan.GetNetCoordinates(target),
                null,
                client.Session!);

            cEntMan.RaisePredictiveEvent(new RequestShootEvent
            {
                Gun = cEntMan.GetNetEntity(cGun),
                Coordinates = cEntMan.GetNetCoordinates(target),
                Shot = projectiles?.Select(projectile => projectile.Id).ToList(),
                LastRealTick = default,
            });
        });
        await pair.RunTicksSync(3);

        Assert.That(projectiles, Has.Count.EqualTo(1));
        var clientProjectile = projectiles!.Single();
        EntityUid serverProjectile = default;
        EntityUid crossMapTarget = default;
        EntityUid friendlyTarget = default;
        NetEntity softTargetNet = default;
        NetEntity mixedTargetNet = default;
        NetEntity crossMapTargetNet = default;
        NetEntity friendlyTargetNet = default;
        MapCoordinates projectileCoordinates = default;
        MapCoordinates mixedTargetCoordinates = default;
        MapCoordinates crossMapCoordinates = default;
        await server.WaitPost(() =>
        {
            var query = sEntMan.EntityQueryEnumerator<PredictedProjectileServerComponent>();
            Assert.That(query.MoveNext(out serverProjectile, out var predicted), Is.True);
            Assert.That(predicted!.ClientId, Is.EqualTo(clientProjectile.Id));

            projectileCoordinates = server.System<SharedTransformSystem>().GetMapCoordinates(serverProjectile);
            var softTarget = sEntMan.SpawnEntity(
                "RMCGunPredictionSoftTarget",
                sEntMan.GetComponent<TransformComponent>(serverProjectile).Coordinates);
            softTargetNet = sEntMan.GetNetEntity(softTarget);
            var mixedTarget = sEntMan.SpawnEntity(
                "RMCGunPredictionMixedTarget",
                sEntMan.GetComponent<TransformComponent>(serverProjectile).Coordinates.Offset(Vector2.UnitX * 3));
            mixedTargetNet = sEntMan.GetNetEntity(mixedTarget);
            mixedTargetCoordinates = server.System<SharedTransformSystem>().GetMapCoordinates(mixedTarget);
            crossMapTarget = sEntMan.SpawnEntity("MobHuman", otherMap.GridCoords);
            crossMapTargetNet = sEntMan.GetNetEntity(crossMapTarget);
            crossMapCoordinates = server.System<SharedTransformSystem>().GetMapCoordinates(crossMapTarget);
            friendlyTarget = sEntMan.SpawnEntity(
                "RMCGunPredictionFriendlyTarget",
                sEntMan.GetComponent<TransformComponent>(serverProjectile).Coordinates);
            friendlyTargetNet = sEntMan.GetNetEntity(friendlyTarget);
        });
        await pair.RunTicksSync(2);

        await client.WaitAssertion(() =>
        {
            Assert.That(cEntMan.EntityExists(clientProjectile), Is.True);
            Assert.That(cEntMan.HasComponent<PhysicsComponent>(clientProjectile), Is.True);
            Assert.That(cEntMan.GetComponent<PredictedProjectileClientComponent>(clientProjectile).Hit, Is.False);
        });

        await client.WaitPost(() =>
        {
            client.System<GunPredictionSystem>().ReportPredictedHit(new PredictedProjectileHitEvent(
                clientProjectile.Id,
                [
                    (softTargetNet, projectileCoordinates),
                    (mixedTargetNet, mixedTargetCoordinates),
                    (crossMapTargetNet, crossMapCoordinates),
                    (friendlyTargetNet, projectileCoordinates),
                ]));
        });
        await pair.RunTicksSync(2);
        await server.WaitAssertion(() =>
        {
            var prediction = sEntMan.GetComponent<PredictedProjectileServerComponent>(serverProjectile);
            Assert.Multiple(() =>
            {
                Assert.That(sEntMan.EntityExists(serverProjectile), Is.True);
                Assert.That(prediction.Hit, Is.False);
                Assert.That(prediction.RejectionSent, Is.True);
            });
        });
        // The rejection is raised during the server update above; allow the
        // network event and the client's queued deletion to complete.
        await pair.RunTicksSync(2);
        await client.WaitAssertion(() =>
        {
            Assert.That(cEntMan.EntityExists(clientProjectile), Is.False);
        });

        // A rejected correlation is resolved and cannot submit another target.
        // Use a fresh shot to prove that anchored/static targets remain valid even
        // without lag-compensation history.
        await pair.RunTicksSync(65);
        List<EntityUid>? staticProjectiles = null;
        await client.WaitPost(() =>
        {
            var target = new EntityCoordinates(cPlayer, Vector2.UnitX * 10);
            var coordinates = cEntMan.GetNetCoordinates(target);
            staticProjectiles = client.System<GunPredictionSystem>().ShootRequested(
                cEntMan.GetNetEntity(cGun),
                coordinates,
                null,
                client.Session!);

            cEntMan.RaisePredictiveEvent(new RequestShootEvent
            {
                Gun = cEntMan.GetNetEntity(cGun),
                Coordinates = coordinates,
                Shot = staticProjectiles?.Select(projectile => projectile.Id).ToList(),
                LastRealTick = default,
            });
        });
        await pair.RunTicksSync(3);

        Assert.That(staticProjectiles, Has.Count.EqualTo(1));
        var staticClientProjectile = staticProjectiles!.Single();
        EntityUid staticServerProjectile = default;
        NetEntity hardTargetNet = default;
        MapCoordinates hardTargetCoordinates = default;
        await server.WaitPost(() =>
        {
            var query = sEntMan.EntityQueryEnumerator<PredictedProjectileServerComponent>();
            while (query.MoveNext(out var projectile, out var prediction))
            {
                if (prediction.ClientId == staticClientProjectile.Id)
                    staticServerProjectile = projectile;
            }

            Assert.That(staticServerProjectile, Is.Not.EqualTo(default(EntityUid)));
            var hardTarget = sEntMan.SpawnEntity(
                "RMCGunPredictionStaticTarget",
                sEntMan.GetComponent<TransformComponent>(staticServerProjectile).Coordinates.Offset(Vector2.UnitX));
            hardTargetNet = sEntMan.GetNetEntity(hardTarget);
            hardTargetCoordinates = server.System<SharedTransformSystem>().GetMapCoordinates(hardTarget);
        });
        await client.WaitPost(() =>
        {
            client.System<GunPredictionSystem>().ReportPredictedHit(new PredictedProjectileHitEvent(
                staticClientProjectile.Id,
                [(hardTargetNet, hardTargetCoordinates)]));
        });
        await pair.RunTicksSync(2);
        await server.WaitAssertion(() =>
        {
            var prediction = sEntMan.GetComponent<PredictedProjectileServerComponent>(staticServerProjectile);
            Assert.Multiple(() =>
            {
                Assert.That(sEntMan.EntityExists(staticServerProjectile), Is.True);
                Assert.That(prediction.Hit, Is.True);
                Assert.That(prediction.RejectionSent, Is.False);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PenetratingProjectileKeepsPredictedCopyAliveAcrossMultipleHits()
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
        EntityUid firstTarget = default;
        EntityUid secondTarget = default;

        await server.WaitPost(() =>
        {
            sPlayer = sEntMan.SpawnEntity("MobHuman", map.GridCoords);
            Assert.That(playerManager.SetAttachedEntity(serverSession, sPlayer), Is.True);
            sGun = sEntMan.SpawnEntity("RMCGunPredictionPenetrationGun", map.GridCoords);
            Assert.That(server.System<SharedHandsSystem>().TryPickup(sPlayer, sGun), Is.True);
            server.System<SharedCombatModeSystem>().SetInCombatMode(sPlayer, true);
            firstTarget = sEntMan.SpawnEntity(
                "RMCGunPredictionPenetrationTarget",
                map.GridCoords.Offset(Vector2.UnitX * 10));
            secondTarget = sEntMan.SpawnEntity(
                "RMCGunPredictionPenetrationTarget",
                map.GridCoords.Offset(Vector2.UnitX * 12));
        });
        await pair.RunTicksSync(5);

        var cPlayer = client.Session?.AttachedEntity ??
            throw new AssertionException("Client player was not attached");
        var cGun = cEntMan.GetEntity(sEntMan.GetNetEntity(sGun));
        EntityUid clientProjectile = default;

        await client.WaitPost(() =>
        {
            var target = new EntityCoordinates(cPlayer, Vector2.UnitX * 20);
            var coordinates = cEntMan.GetNetCoordinates(target);
            var shot = client.System<GunPredictionSystem>().ShootRequested(
                cEntMan.GetNetEntity(cGun),
                coordinates,
                null,
                client.Session!);
            Assert.That(shot, Has.Count.EqualTo(1));
            clientProjectile = shot!.Single();

            cEntMan.RaisePredictiveEvent(new RequestShootEvent
            {
                Gun = cEntMan.GetNetEntity(cGun),
                Coordinates = coordinates,
                Shot = [clientProjectile.Id],
                LastRealTick = default,
            });
        });

        await pair.RunTicksSync(3);

        EntityUid serverProjectile = default;
        await server.WaitAssertion(() =>
        {
            var query = sEntMan.EntityQueryEnumerator<PredictedProjectileServerComponent>();
            while (query.MoveNext(out var projectile, out var predicted))
            {
                if (predicted.ClientId == clientProjectile.Id)
                    serverProjectile = projectile;
            }

            Assert.That(serverProjectile, Is.Not.EqualTo(default(EntityUid)));
        });

        var clientFirstTarget = cEntMan.GetEntity(sEntMan.GetNetEntity(firstTarget));
        var clientSecondTarget = cEntMan.GetEntity(sEntMan.GetNetEntity(secondTarget));
        await client.WaitPost(() =>
        {
            var projectile = cEntMan.GetComponent<ProjectileComponent>(clientProjectile);
            var physics = cEntMan.GetComponent<PhysicsComponent>(clientProjectile);
            var projectileSystem = client.System<Content.Client.Projectiles.ProjectileSystem>();
            projectileSystem.ProjectileCollide((clientProjectile, projectile, physics), clientFirstTarget);
            Assert.That(projectile.ProjectileSpent, Is.False);
            projectileSystem.ProjectileCollide((clientProjectile, projectile, physics), clientSecondTarget);
            Assert.That(projectile.ProjectileSpent, Is.False);
        });

        await server.WaitPost(() =>
        {
            var projectile = sEntMan.GetComponent<ProjectileComponent>(serverProjectile);
            var physics = sEntMan.GetComponent<PhysicsComponent>(serverProjectile);
            var projectileSystem = server.System<Content.Server.Projectiles.ProjectileSystem>();
            var damageableSystem = server.System<Content.Shared.Damage.Systems.DamageableSystem>();
            projectileSystem.ProjectileCollide((serverProjectile, projectile, physics), firstTarget, true);
            Assert.That(projectile.ProjectileSpent, Is.False);
            Assert.That(damageableSystem.GetTotalDamage(firstTarget), Is.EqualTo(FixedPoint2.New(25)));
            Assert.That(sEntMan.IsQueuedForDeletion(firstTarget), Is.True);
            Assert.That(projectile.PenetrationAmount, Is.EqualTo(FixedPoint2.New(20)));
            projectileSystem.ProjectileCollide((serverProjectile, projectile, physics), secondTarget, true);
            Assert.That(projectile.ProjectileSpent, Is.False);
            Assert.That(damageableSystem.GetTotalDamage(secondTarget), Is.EqualTo(FixedPoint2.New(25)));
            Assert.That(sEntMan.IsQueuedForDeletion(secondTarget), Is.True);
            Assert.That(projectile.PenetrationAmount, Is.EqualTo(FixedPoint2.New(40)));
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(sEntMan.EntityExists(firstTarget), Is.False);
                Assert.That(sEntMan.EntityExists(secondTarget), Is.False);
            });

            var projectileComponent = sEntMan.GetComponent<ProjectileComponent>(serverProjectile);

            Assert.Multiple(() =>
            {
                Assert.That(sEntMan.EntityExists(serverProjectile), Is.True);
                Assert.That(sEntMan.IsQueuedForDeletion(serverProjectile), Is.False);
                Assert.That(projectileComponent.ProjectileSpent, Is.False);
                Assert.That(projectileComponent.PenetrationAmount, Is.EqualTo(FixedPoint2.New(40)));
            });
        });

        await client.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(cEntMan.EntityExists(clientProjectile), Is.True);
                Assert.That(cEntMan.IsQueuedForDeletion(clientProjectile), Is.False);
                Assert.That(cEntMan.GetComponent<ProjectileComponent>(clientProjectile).ProjectileSpent, Is.False);
                Assert.That(
                    cEntMan.GetComponent<PredictedProjectileClientComponent>(clientProjectile).Hit,
                    Is.False);
            });
        });

        await pair.CleanReturnAsync();
    }

    [TestCase(true, true, true, true, true)]
    [TestCase(false, true, true, true, false)]
    [TestCase(true, false, true, true, false)]
    [TestCase(true, true, false, true, false)]
    [TestCase(true, true, true, false, false)]
    public void PredictedCopyMatchingRequiresExactLocalEntity(
        bool localPlayer,
        bool exists,
        bool clientSide,
        bool predicted,
        bool expected)
    {
        Assert.That(
            GunPredictionSystem.IsMatchingPredictedProjectileCopy(localPlayer, exists, clientSide, predicted),
            Is.EqualTo(expected));
    }

    [TestCase(true, true, false, false, true)]
    [TestCase(true, false, true, false, true)]
    [TestCase(true, true, true, true, false)]
    [TestCase(false, true, true, false, false)]
    [TestCase(true, false, false, false, false)]
    public void AuthoritativeVisibilityUsesLiveCopiesAndCompletedPredictionTombstones(
        bool localPlayer,
        bool liveCopy,
        bool completed,
        bool rejected,
        bool expected)
    {
        Assert.That(
            GunPredictionSystem.ShouldHideAuthoritativeProjectile(
                localPlayer,
                liveCopy,
                completed,
                rejected),
            Is.EqualTo(expected));
    }

    [Test]
    public void PredictedHitReportLimitIsPerSessionAndDeduplicatesLiveRejections()
    {
        var limiter = new ServerPredictedHitReportLimiter(2);
        var firstPlayer = Guid.NewGuid();
        var secondPlayer = Guid.NewGuid();

        Assert.Multiple(() =>
        {
            Assert.That(limiter.TryAcquire(firstPlayer), Is.True);
            Assert.That(limiter.TryAcquire(firstPlayer), Is.True);
            Assert.That(limiter.TryAcquire(secondPlayer), Is.True);
            Assert.That(limiter.TryAcquire(firstPlayer), Is.False);
            Assert.That(limiter.TryAcquire(secondPlayer), Is.True);
            Assert.That(limiter.TryAcquire(secondPlayer), Is.False);
            Assert.That(limiter.ShouldRejectOverLimitReport(firstPlayer, 1, false), Is.False);
            Assert.That(limiter.ShouldRejectOverLimitReport(firstPlayer, 1, true), Is.True);
            Assert.That(limiter.ShouldRejectOverLimitReport(firstPlayer, 1, true), Is.False);
            Assert.That(limiter.ShouldRejectOverLimitReport(secondPlayer, 1, true), Is.True);
        });

        limiter.Reset();

        Assert.Multiple(() =>
        {
            Assert.That(limiter.TryAcquire(firstPlayer), Is.True);
            Assert.That(limiter.TryAcquire(secondPlayer), Is.True);
            Assert.That(limiter.ShouldRejectOverLimitReport(firstPlayer, 1, true), Is.True);
        });
    }

    [TestCase(true, SelectiveFire.SemiAuto, SelectiveFire.SemiAuto, false, true)]
    [TestCase(true, SelectiveFire.SemiAuto, SelectiveFire.SemiAuto | SelectiveFire.FullAuto, false, false)]
    [TestCase(true, SelectiveFire.FullAuto, SelectiveFire.FullAuto, false, false)]
    [TestCase(true, SelectiveFire.Burst, SelectiveFire.Burst, false, false)]
    [TestCase(true, SelectiveFire.SemiAuto, SelectiveFire.SemiAuto | SelectiveFire.Burst, false, true)]
    [TestCase(true, SelectiveFire.SemiAuto, SelectiveFire.SemiAuto, true, false)]
    [TestCase(false, SelectiveFire.SemiAuto, SelectiveFire.SemiAuto, false, false)]
    public void HeldFireRearmsSelectedSemiAutoWhenGunHasNoFullAutoMode(
        bool holdToFire,
        SelectiveFire selectedMode,
        SelectiveFire availableModes,
        bool clickToFire,
        bool expected)
    {
        Assert.That(
            ClientGunSystem.ShouldRearmSemiAuto(
                holdToFire,
                selectedMode,
                availableModes,
                clickToFire),
            Is.EqualTo(expected));
    }
}
