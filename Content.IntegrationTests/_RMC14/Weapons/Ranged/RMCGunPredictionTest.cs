#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client._RMC14.Weapons.Ranged.Prediction;
using Content.Shared._RMC14.Weapons.Ranged.Prediction;
using Content.Shared.CombatMode;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Server.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Components;

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
          parent: RMCGunPredictionTestGun
          id: RMCGunPredictionTestIgnoredGun
          components:
          - type: GunIgnorePrediction
        """;

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
                [(softTargetNet, projectileCoordinates)]));
        });
        await pair.RunTicksSync(2);
        await server.WaitAssertion(() =>
        {
            Assert.That(sEntMan.EntityExists(serverProjectile), Is.True);
            Assert.That(sEntMan.GetComponent<PredictedProjectileServerComponent>(serverProjectile).Hit, Is.False);
        });

        await client.WaitPost(() =>
        {
            client.System<GunPredictionSystem>().ReportPredictedHit(new PredictedProjectileHitEvent(
                clientProjectile.Id,
                [(mixedTargetNet, mixedTargetCoordinates)]));
        });
        await pair.RunTicksSync(2);
        await server.WaitAssertion(() =>
        {
            Assert.That(sEntMan.EntityExists(serverProjectile), Is.True);
            Assert.That(sEntMan.GetComponent<PredictedProjectileServerComponent>(serverProjectile).Hit, Is.False);
        });

        await client.WaitPost(() =>
        {
            client.System<GunPredictionSystem>().ReportPredictedHit(new PredictedProjectileHitEvent(
                clientProjectile.Id,
                [(crossMapTargetNet, crossMapCoordinates)]));
        });
        await pair.RunTicksSync(2);
        await server.WaitAssertion(() =>
        {
            Assert.That(sEntMan.EntityExists(serverProjectile), Is.True);
            Assert.That(sEntMan.GetComponent<PredictedProjectileServerComponent>(serverProjectile).Hit, Is.False);
        });

        await client.WaitPost(() =>
        {
            client.System<GunPredictionSystem>().ReportPredictedHit(new PredictedProjectileHitEvent(
                clientProjectile.Id,
                [(friendlyTargetNet, projectileCoordinates)]));
        });
        await pair.RunTicksSync(2);
        await server.WaitAssertion(() =>
        {
            Assert.That(sEntMan.EntityExists(serverProjectile), Is.True);
            Assert.That(sEntMan.GetComponent<PredictedProjectileServerComponent>(serverProjectile).Hit, Is.False);
        });

        NetEntity hardTargetNet = default;
        MapCoordinates hardTargetCoordinates = default;
        await server.WaitPost(() =>
        {
            var hardTarget = sEntMan.SpawnEntity(
                "RMCGunPredictionHardTarget",
                sEntMan.GetComponent<TransformComponent>(serverProjectile).Coordinates.Offset(Vector2.UnitX));
            hardTargetNet = sEntMan.GetNetEntity(hardTarget);
            hardTargetCoordinates = server.System<SharedTransformSystem>().GetMapCoordinates(hardTarget);
        });
        await client.WaitPost(() =>
        {
            client.System<GunPredictionSystem>().ReportPredictedHit(new PredictedProjectileHitEvent(
                clientProjectile.Id,
                [(hardTargetNet, hardTargetCoordinates)]));
        });
        await pair.RunTicksSync(2);
        await server.WaitAssertion(() =>
        {
            Assert.That(sEntMan.EntityExists(serverProjectile), Is.True);
            Assert.That(sEntMan.GetComponent<PredictedProjectileServerComponent>(serverProjectile).Hit, Is.True);
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
}
