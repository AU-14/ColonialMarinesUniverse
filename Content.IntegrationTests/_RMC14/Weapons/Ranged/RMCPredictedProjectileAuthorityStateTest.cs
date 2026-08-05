#nullable enable

using System.Linq;
using Content.Client._RMC14.Weapons.Ranged.Prediction;
using Content.Shared._RMC14.Weapons.Ranged.Prediction;
using Robust.Client.GameObjects;
using Robust.Server.Player;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests._RMC14.Weapons.Ranged;

[TestFixture]
[TestOf(typeof(GunPredictionSystem))]
public sealed class RMCPredictedProjectileAuthorityStateTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          parent: BaseBullet
          id: RMCPredictedProjectileAuthorityStateProjectile
          components:
          - type: Projectile
            deleteOnCollide: false
          - type: TimedDespawn
            lifetime: 10
        """;

    [Test]
    public async Task CorrelationAddedAfterEntityStateHidesAuthority()
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
        EntityUid serverPlayer = default;
        EntityUid serverProjectile = default;

        await server.WaitPost(() =>
        {
            serverPlayer = serverEntMan.SpawnEntity("MobHuman", map.GridCoords);
            Assert.That(playerManager.SetAttachedEntity(serverSession, serverPlayer), Is.True);
            serverProjectile = serverEntMan.SpawnEntity(
                "RMCPredictedProjectileAuthorityStateProjectile",
                map.GridCoords);
        });
        await pair.RunTicksSync(5);

        var clientPlayer = client.Session?.AttachedEntity ??
            throw new AssertionException("The client must have an attached entity.");
        var clientAuthority = clientEntMan.GetEntity(serverEntMan.GetNetEntity(serverProjectile));
        EntityUid clientProjectile = default;

        await client.WaitPost(() =>
        {
            clientProjectile = clientEntMan.SpawnEntity(
                "RMCPredictedProjectileAuthorityStateProjectile",
                clientEntMan.GetComponent<TransformComponent>(clientPlayer).Coordinates);
            clientEntMan.EnsureComponent<PredictedProjectileClientComponent>(clientProjectile);

            Assert.Multiple(() =>
            {
                Assert.That(clientEntMan.IsClientSide(clientProjectile), Is.True);
                Assert.That(clientEntMan.GetComponent<SpriteComponent>(clientAuthority).Visible, Is.True);
            });
        });

        await server.WaitPost(() =>
        {
#pragma warning disable RA0002
            var correlation = new PredictedProjectileServerComponent
            {
                Shooter = serverSession,
                ClientId = clientProjectile.Id,
                ClientEnt = serverPlayer,
            };
#pragma warning restore RA0002
            serverEntMan.AddComponent(serverProjectile, correlation);
            serverEntMan.Dirty(serverProjectile, correlation);
        });
        await pair.RunTicksSync(3);

        await client.WaitAssertion(() =>
        {
            var correlation = clientEntMan.GetComponent<PredictedProjectileServerComponent>(clientAuthority);
            Assert.Multiple(() =>
            {
                Assert.That(correlation.ClientId, Is.EqualTo(clientProjectile.Id));
                Assert.That(correlation.ClientEnt, Is.EqualTo(clientPlayer));
                Assert.That(clientEntMan.GetComponent<SpriteComponent>(clientAuthority).Visible, Is.False);
            });
        });

        await pair.CleanReturnAsync();
    }
}
