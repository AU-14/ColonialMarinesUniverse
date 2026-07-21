#nullable enable

using System.Numerics;
using Content.Client._RMC14.Weapons.Ranged.Prediction;
using Content.Shared.CombatMode;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Server.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using ServerGunSystem = Content.Server.Weapons.Ranged.Systems.GunSystem;

namespace Content.IntegrationTests._RMC14.Weapons.Ranged;

[TestFixture]
[TestOf(typeof(Content.Client.Weapons.Ranged.Systems.GunSystem))]
public sealed class RMCDeleteOnSpawnCartridgePredictionTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          parent: BaseItem
          id: RMCDeleteOnSpawnCartridgePredictionGun
          components:
          - type: Gun
            fireRate: 1
            projectileSpeed: 0.01
            resetOnHandSelected: false
            soundGunshot: null
            soundEmpty: null
          - type: BallisticAmmoProvider
            capacity: 1
            whitelist:
              tags:
              - Cartridge

        - type: entity
          parent: BaseCartridge
          id: RMCDeleteOnSpawnCartridgePredictionAmmo
          components:
          - type: CartridgeAmmo
            proto: RMCDeleteOnSpawnCartridgePredictionProjectile
            deleteOnSpawn: true
          - type: Appearance

        - type: entity
          parent: BaseBullet
          id: RMCDeleteOnSpawnCartridgePredictionProjectile
          components:
          - type: Projectile
            deleteOnCollide: false
            impactEffect: null
            soundHit: null
        """;

    [Test]
    public async Task DeleteOnSpawnCartridgeDoesNotAppearEjectedDuringPrediction()
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
            serverGun = serverEntMan.SpawnEntity("RMCDeleteOnSpawnCartridgePredictionGun", map.GridCoords);
            serverCartridge = serverEntMan.SpawnEntity("RMCDeleteOnSpawnCartridgePredictionAmmo", map.GridCoords);

            var gun = server.System<ServerGunSystem>();
            var provider = serverEntMan.GetComponent<BallisticAmmoProviderComponent>(serverGun);
            Assert.That(gun.TryBallisticInsert((serverGun, provider), serverCartridge, null, true), Is.True);
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
            var projectiles = client.System<GunPredictionSystem>().ShootRequested(
                clientEntMan.GetNetEntity(clientGun),
                clientEntMan.GetNetCoordinates(target),
                null,
                client.Session!);

            Assert.Multiple(() =>
            {
                Assert.That(projectiles, Has.Count.EqualTo(1));
                Assert.That(
                    clientEntMan.IsQueuedForDeletion(clientCartridge),
                    Is.True,
                    "A cartridge deleted by the server must be hidden by client prediction.");
            });
        });

        await client.WaitRunTicks(1);
        await client.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(clientEntMan.EntityExists(clientCartridge), Is.True);
                Assert.That(
                    clientEntMan.GetComponent<TransformComponent>(clientCartridge).MapID,
                    Is.EqualTo(MapId.Nullspace));
            });
        });

        await pair.CleanReturnAsync();
    }
}
