#nullable enable

using System.Linq;
using System.Numerics;
using Content.Client._RMC14.Weapons.Ranged.Prediction;
using Content.Shared._RMC14.Weapons.Ranged.Prediction;
using Content.Shared.FixedPoint;
using Content.Shared.Projectiles;
using Robust.Server.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests._RMC14.Weapons.Ranged;

[TestFixture]
[TestOf(typeof(GunPredictionSystem))]
public sealed class RMCPhysicalProjectileStateTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          parent: BaseBullet
          id: RMCPhysicalProjectileStateProjectile
          components:
          - type: Projectile
            deleteOnCollide: false
            impactEffect: null
            soundHit: null
            damage:
              types:
                Structural: 10
            penetrationThreshold: 100

        - type: entity
          id: RMCPhysicalProjectileStateTarget
        """;

    [Test]
    public async Task NetworkedPhysicalProjectileStateAndPenetrationFeedbackReconcile()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });
        var server = pair.Server;
        var client = pair.Client;
        var serverEntMan = server.EntMan;
        var clientEntMan = client.EntMan;
        var playerManager = server.ResolveDependency<IPlayerManager>();
        var serverSession = playerManager.Sessions.Single();
        var map = await pair.CreateTestMap();
        EntityUid serverProjectile = default;
        EntityUid serverTarget = default;

        await server.WaitPost(() =>
        {
            var player = serverEntMan.SpawnEntity("MobHuman", map.GridCoords);
            Assert.That(playerManager.SetAttachedEntity(serverSession, player), Is.True);
            serverProjectile = serverEntMan.SpawnEntity("RMCPhysicalProjectileStateProjectile", map.GridCoords);
            serverTarget = serverEntMan.SpawnEntity("RMCPhysicalProjectileStateTarget", map.GridCoords);
        });
        await pair.RunTicksSync(5);

        var projectileNetEntity = serverEntMan.GetNetEntity(serverProjectile);
        var targetNetEntity = serverEntMan.GetNetEntity(serverTarget);
        var clientProjectile = clientEntMan.GetEntity(projectileNetEntity);
        var clientTarget = clientEntMan.GetEntity(targetNetEntity);

        await server.WaitPost(() =>
        {
            var projectile = serverEntMan.GetComponent<ProjectileComponent>(serverProjectile);
            projectile.Damage *= 2;
            projectile.ProjectileSpent = true;
            projectile.PenetrationAmount = FixedPoint2.New(25);
            serverEntMan.Dirty(serverProjectile, projectile);
        });
        await pair.RunTicksSync(3);

        await client.WaitAssertion(() =>
        {
            var projectile = clientEntMan.GetComponent<ProjectileComponent>(clientProjectile);
            Assert.Multiple(() =>
            {
                Assert.That(clientEntMan.IsClientSide(clientProjectile), Is.False);
                Assert.That(projectile.Damage.GetTotal(), Is.EqualTo(FixedPoint2.New(20)));
                Assert.That(projectile.ProjectileSpent, Is.True);
                Assert.That(projectile.PenetrationAmount, Is.EqualTo(FixedPoint2.New(25)));
            });
        });

        await client.WaitPost(() =>
        {
            var predicted = clientEntMan.EnsureComponent<PredictedProjectileClientComponent>(clientProjectile);
            var projectile = clientEntMan.GetComponent<ProjectileComponent>(clientProjectile);
            var physics = clientEntMan.GetComponent<PhysicsComponent>(clientProjectile);
            var expectedVelocity = new Vector2(6, 2);

            predicted.Hit = true;
            predicted.PendingPenetrationBodyType = BodyType.Dynamic;
            predicted.PendingPenetrationVelocity = expectedVelocity;
            projectile.ProjectileSpent = true;
            client.System<SharedPhysicsSystem>().SetLinearVelocity(clientProjectile, Vector2.Zero, body: physics);
            client.System<SharedPhysicsSystem>().SetBodyType(clientProjectile, BodyType.Static, body: physics);

            clientEntMan.EventBus.RaiseEvent(
                EventSource.Network,
                new PredictedProjectileImpactFeedbackEvent(
                    clientProjectile.Id,
                    targetNetEntity,
                    clientEntMan.GetNetCoordinates(clientEntMan.GetComponent<TransformComponent>(clientTarget).Coordinates),
                    projectile.Damage,
                    null,
                    null,
                    false,
                    false,
                    false,
                    false));

            Assert.Multiple(() =>
            {
                Assert.That(predicted.Hit, Is.False);
                Assert.That(predicted.HitTargets, Does.Contain(targetNetEntity));
                Assert.That(predicted.PendingPenetrationBodyType, Is.Null);
                Assert.That(predicted.PendingPenetrationVelocity, Is.Null);
                Assert.That(projectile.ProjectileSpent, Is.False);
                Assert.That(physics.BodyType, Is.EqualTo(BodyType.Dynamic));
                Assert.That(physics.LinearVelocity, Is.EqualTo(expectedVelocity));
            });
        });

        await pair.CleanReturnAsync();
    }
}
