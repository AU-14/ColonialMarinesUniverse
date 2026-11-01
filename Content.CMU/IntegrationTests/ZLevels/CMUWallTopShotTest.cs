#pragma warning disable RA0002 // Control the gun cooldown for a single shot.
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.CMU14.ZLevels;

public sealed class CMUWallTopShotTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          parent: BaseItem
          id: CMUWallTopTestPistol
          components:
          - type: Gun
            selectedMode: SemiAuto
            availableModes: [SemiAuto]
          - type: BallisticAmmoProvider
            proto: CMCartridgePistol9mm
            capacity: 1
        """;

    [Test]
    public async Task DownwardShotClearsSupportingWallButKeepsSeparateObstructions()
    {
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            var zLevels = Server.System<CMUZLevelsSystem>();
            var shooting = Server.System<CMUZLevelShootingSystem>();
            var transform = Server.System<SharedTransformSystem>();
            var tiles = Server.ResolveDependency<ITileDefinitionManager>();
            var floor = new Tile(tiles["Plating"].TileId);
            var network = zLevels.CreateZNetwork();
            var lower = maps.CreateMap(runMapInit: true);
            var upper = maps.CreateMap(runMapInit: true);
            try
            {
                var lowerGrid = SEntMan.EnsureComponent<MapGridComponent>(lower);
                var upperGrid = SEntMan.EnsureComponent<MapGridComponent>(upper);
                for (var x = 0; x <= 4; x++)
                    maps.SetTile(lower, lowerGrid, new Vector2i(x, 0), floor);
                maps.SetTile(upper, upperGrid, new Vector2i(8, 8), floor);
                Assert.That(zLevels.TryAddMapsIntoZNetwork(network, new() { [lower] = 0, [upper] = 1 }), Is.True);
                foreach (var x in new[] { 0, 1, 3 })
                    SEntMan.SpawnEntity("CMWallMetal", new EntityCoordinates(lower, new Vector2(x + 0.5f, 0.5f)));
                for (var x = 0; x <= 4; x++)
                    maps.SetTile(upper, upperGrid, new Vector2i(x, 0), Tile.Empty);

                var from = new EntityCoordinates(upper, new Vector2(0.5f));
                var to = new EntityCoordinates(upper, new Vector2(4.5f, 0.5f));
                var shooter = SEntMan.SpawnEntity("MobHuman", from);
                var gun = SEntMan.SpawnEntity("CMUWallTopTestPistol", from);
                Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(shooter, gun), Is.True);
                shooting.SetAimDown(shooter, true);

                Assert.That(shooting.TryAdjustShotCoordinates(shooter, from, to, out var adjusted, out _), Is.True);
                var spawn = transform.ToMapCoordinates(adjusted);
                Assert.That(spawn.Position.X, Is.GreaterThan(2).And.LessThan(3),
                    "The shot must clear the connected wall top, but spawn before the separate target wall.");
                Assert.That(shooting.TryAdjustShotMapCoordinates(shooter, transform.ToMapCoordinates(from),
                    transform.ToMapCoordinates(to), out var mapSpawn, out _), Is.True);
                Assert.That(mapSpawn, Is.EqualTo(spawn), "Both firing APIs must respect the same supporting walls.");

                var gunComp = SEntMan.GetComponent<GunComponent>(gun);
                gunComp.NextFire = SGameTiming.CurTime;
                gunComp.ShootCoordinates = to;
                var projectiles = Server.System<SharedGunSystem>().AttemptShoot(shooter, (gun, gunComp));
                Assert.That(projectiles, Has.Count.EqualTo(1));
                Assert.That(transform.GetMapCoordinates(projectiles![0]), Is.EqualTo(spawn));
            }
            finally
            {
                SEntMan.DeleteEntity(upper);
                SEntMan.DeleteEntity(lower);
                SEntMan.DeleteEntity(network);
            }
        });
    }
}
