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
public sealed class RMCRevolverPredictionRollbackTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          parent: BaseItem
          id: RMCRevolverPredictionRollbackGun
          components:
          - type: Gun
            fireRate: 30
            projectileSpeed: 0.01
            resetOnHandSelected: false
            soundGunshot: null
            soundEmpty: null
          - type: ContainerContainer
            containers:
              revolver-ammo: !type:Container
          - type: RevolverAmmoProvider
            proto: null
            capacity: 1
            chambers: [ null ]
            ammoSlots: [ null ]
            whitelist:
              components:
              - CartridgeAmmo
            soundEject: null
            soundInsert: null
            soundSpin: null

        - type: entity
          parent: BaseCartridge
          id: RMCRevolverPredictionRollbackCartridge
          components:
          - type: CartridgeAmmo
            proto: RMCRevolverPredictionRollbackProjectile
          - type: Appearance

        - type: entity
          parent: BaseBullet
          id: RMCRevolverPredictionRollbackProjectile
          components:
          - type: Projectile
            deleteOnCollide: false
            impactEffect: null
            soundHit: null
        """;

    [Test]
    public async Task NetworkedCartridgeSurvivesPredictionRollback()
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
        EntityUid serverGun = default;
        EntityUid serverCartridge = default;

        await server.WaitPost(() =>
        {
            var player = serverEntMan.SpawnEntity("MobHuman", map.GridCoords);
            Assert.That(playerManager.SetAttachedEntity(serverSession, player), Is.True);
            serverGun = serverEntMan.SpawnEntity("RMCRevolverPredictionRollbackGun", map.GridCoords);
            serverCartridge = serverEntMan.SpawnEntity("RMCRevolverPredictionRollbackCartridge", map.GridCoords);

            var gun = server.System<ServerGunSystem>();
            var provider = serverEntMan.GetComponent<RevolverAmmoProviderComponent>(serverGun);
            Assert.That(gun.TryRevolverInsert((serverGun, provider), serverCartridge, null), Is.True);
            Assert.That(server.System<SharedHandsSystem>().TryPickup(player, serverGun), Is.True);
            server.System<SharedCombatModeSystem>().SetInCombatMode(player, true);
        });
        await pair.RunTicksSync(5);

        var clientPlayer = client.Session?.AttachedEntity ??
            throw new AssertionException("The client must have an attached entity.");
        var clientGun = clientEntMan.GetEntity(serverEntMan.GetNetEntity(serverGun));
        var clientCartridge = clientEntMan.GetEntity(serverEntMan.GetNetEntity(serverCartridge));

        await client.WaitPost(() =>
        {
            var target = new EntityCoordinates(clientPlayer, Vector2.UnitX * 10);
            var coordinates = clientEntMan.GetNetCoordinates(target);
            var projectiles = client.System<GunPredictionSystem>().ShootRequested(
                clientEntMan.GetNetEntity(clientGun),
                coordinates,
                null,
                client.Session!);

            Assert.That(projectiles, Has.Count.EqualTo(1));
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
            Assert.Multiple(() =>
            {
                Assert.That(clientEntMan.EntityExists(clientCartridge), Is.True);
                Assert.That(clientEntMan.IsQueuedForDeletion(clientCartridge), Is.False);
                Assert.That(
                    clientEntMan.GetComponent<CartridgeAmmoComponent>(clientCartridge).Spent,
                    Is.True);
            });
        });

        await pair.CleanReturnAsync();
    }
}
