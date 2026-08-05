#nullable enable

using System.Linq;
using System.Numerics;
using Content.Client._RMC14.Weapons.Ranged.Prediction;
using Content.Shared.CombatMode;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Server.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using ServerGunSystem = Content.Server.Weapons.Ranged.Systems.GunSystem;

namespace Content.IntegrationTests._RMC14.Weapons.Ranged;

[TestFixture]
[TestOf(typeof(GunPredictionSystem))]
public sealed class RMCPhysicalAmmoPredictionRollbackTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          parent: BaseItem
          id: RMCPhysicalAmmoPredictionRollbackGun
          components:
          - type: Gun
            fireRate: 30
            projectileSpeed: 10
            resetOnHandSelected: false
            soundGunshot: null
            soundEmpty: null
          - type: BallisticAmmoProvider
            capacity: 1
            whitelist:
              components:
              - Ammo
          - type: ContainerContainer
            containers:
              ballistic-ammo: !type:Container
        """;

    [Test]
    public async Task NetworkedThrownAmmoSurvivesPredictionRollback()
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
        EntityUid serverAmmo = default;
        EntityUid serverGun = default;

        await server.WaitPost(() =>
        {
            var player = serverEntMan.SpawnEntity("MobHuman", map.GridCoords);
            Assert.That(playerManager.SetAttachedEntity(serverSession, player), Is.True);
            serverGun = serverEntMan.SpawnEntity("RMCPhysicalAmmoPredictionRollbackGun", map.GridCoords);
            serverAmmo = serverEntMan.SpawnEntity("BulletFoam", map.GridCoords);

            var gun = server.System<ServerGunSystem>();
            var provider = serverEntMan.GetComponent<BallisticAmmoProviderComponent>(serverGun);
            Assert.That(gun.TryBallisticInsert((serverGun, provider), serverAmmo, null, true), Is.True);
            Assert.That(server.System<SharedHandsSystem>().TryPickup(player, serverGun), Is.True);
            server.System<SharedCombatModeSystem>().SetInCombatMode(player, true);
        });
        await pair.RunTicksSync(5);

        var clientPlayer = client.Session?.AttachedEntity ??
            throw new AssertionException("The client must have an attached entity.");
        var clientGun = clientEntMan.GetEntity(serverEntMan.GetNetEntity(serverGun));
        var ammoNetEntity = serverEntMan.GetNetEntity(serverAmmo);
        var clientAmmo = clientEntMan.GetEntity(ammoNetEntity);

        await client.WaitPost(() =>
        {
            var target = new EntityCoordinates(clientPlayer, Vector2.UnitX * 10);
            var coordinates = clientEntMan.GetNetCoordinates(target);
            var projectiles = client.System<GunPredictionSystem>().ShootRequested(
                clientEntMan.GetNetEntity(clientGun),
                coordinates,
                null,
                client.Session!);

            Assert.Multiple(() =>
            {
                Assert.That(projectiles, Has.Count.Zero);
                Assert.That(
                    clientEntMan.EntityExists(clientAmmo),
                    Is.True,
                    "Prediction must not hard-delete server-owned physical ammunition.");
            });

            clientEntMan.RaisePredictiveEvent(new RequestShootEvent
            {
                Gun = clientEntMan.GetNetEntity(clientGun),
                Coordinates = coordinates,
                Shot = projectiles?.Select(projectile => projectile.Id).ToList(),
                LastRealTick = default,
            });
        });

        await client.WaitRunTicks(1);
        for (var i = 0; i < 5; i++)
        {
            await server.WaitRunTicks(1);
            await client.WaitRunTicks(1);
        }

        await client.WaitAssertion(() =>
        {
            var authoritativeAmmo = clientEntMan.GetEntity(ammoNetEntity);
            Assert.Multiple(() =>
            {
                Assert.That(clientEntMan.EntityExists(authoritativeAmmo), Is.True);
                Assert.That(clientEntMan.HasComponent<AmmoComponent>(authoritativeAmmo), Is.False);
            });
        });

        await pair.CleanReturnAsync();
    }
}
