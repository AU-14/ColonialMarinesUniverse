#nullable enable

using System.Linq;
using System.Numerics;
using Content.Client._RMC14.Weapons.Ranged.Prediction;
using Content.Shared._RMC14.Weapons.Ranged.Prediction;
using Content.Shared.CombatMode;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Server.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests._RMC14.Weapons.Ranged;

[TestFixture]
[TestOf(typeof(GunPredictionSystem))]
public sealed class RMCXenoProjectileDamageTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          parent: BaseItem
          id: RMCXenoProjectileDamageTestGun
          components:
          - type: Gun
            fireRate: 1
            projectileSpeed: 0.01
            resetOnHandSelected: false
            soundGunshot: null
            soundEmpty: null
          - type: BasicEntityAmmoProvider
            proto: BulletRifle10x24mm
            capacity: 1
            count: 1
        """;

    [Test]
    public async Task PredictedProjectileHitDamagesXeno()
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
            sGun = sEntMan.SpawnEntity("RMCXenoProjectileDamageTestGun", map.GridCoords);
            Assert.That(server.System<SharedHandsSystem>().TryPickup(sPlayer, sGun), Is.True);
            server.System<SharedCombatModeSystem>().SetInCombatMode(sPlayer, true);
        });
        await pair.RunTicksSync(5);

        var cPlayer = client.Session?.AttachedEntity ??
            throw new AssertionException("The client must have an attached entity.");
        var cGun = cEntMan.GetEntity(sEntMan.GetNetEntity(sGun));
        EntityUid clientProjectile = default;
        await client.WaitPost(() =>
        {
            var target = new EntityCoordinates(cPlayer, Vector2.UnitX * 10);
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
        EntityUid xeno = default;
        NetEntity xenoNet = default;
        MapCoordinates xenoCoordinates = default;
        await server.WaitPost(() =>
        {
            var query = sEntMan.EntityQueryEnumerator<PredictedProjectileServerComponent>();
            while (query.MoveNext(out var projectile, out var prediction))
            {
                if (prediction.ClientId == clientProjectile.Id)
                    serverProjectile = projectile;
            }

            Assert.That(serverProjectile, Is.Not.EqualTo(default(EntityUid)));
            xeno = sEntMan.SpawnEntity(
                "CMXenoDrone",
                sEntMan.GetComponent<TransformComponent>(serverProjectile).Coordinates.Offset(Vector2.UnitX));
            xenoNet = sEntMan.GetNetEntity(xeno);
            xenoCoordinates = server.System<SharedTransformSystem>().GetMapCoordinates(xeno);
        });
        await pair.RunTicksSync(2);

        await client.WaitPost(() =>
        {
            client.System<GunPredictionSystem>().ReportPredictedHit(new PredictedProjectileHitEvent(
                clientProjectile.Id,
                [(xenoNet, xenoCoordinates)]));
        });
        await pair.RunTicksSync(2);

        await server.WaitAssertion(() =>
        {
            var damageable = server.System<DamageableSystem>();
            Assert.That(damageable.GetTotalDamage(xeno), Is.GreaterThan(FixedPoint2.Zero));
        });

        await pair.CleanReturnAsync();
    }
}
