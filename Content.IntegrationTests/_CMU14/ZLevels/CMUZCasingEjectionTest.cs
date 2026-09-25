#pragma warning disable RA0002 // Regression setup controls the shooter's view and gun cooldown.

using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared.CMU14.Weapons.Ranged;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.CMU14.ZLevels;

[TestFixture]
[TestOf(typeof(SharedGunSystem))]
public sealed class CMUZCasingEjectionTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  parent: BaseItem
  id: CMUZCasingTestGun
  components:
  - type: Gun
    selectedMode: SemiAuto
    availableModes: [SemiAuto]
  - type: BallisticAmmoProvider
    proto: CMCartridgePistol9mm
    capacity: 1
";

    [TestCase(-1)]
    [TestCase(0)]
    [TestCase(1)]
    public async Task CasingsStayWithGunWhileProjectilesReachAimedLevel(int shotOffset)
    {
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            var zLevels = Server.System<CMUZLevelsSystem>();
            var transform = Server.System<SharedTransformSystem>();
            var guns = Server.System<SharedGunSystem>();
            var tiles = Server.ResolveDependency<ITileDefinitionManager>();
            var floor = new Tile(tiles["Plating"].TileId);
            var network = zLevels.CreateZNetwork();
            var levels = new EntityUid[3];

            try
            {
                var depths = new Dictionary<EntityUid, int>();
                for (var i = 0; i < levels.Length; i++)
                {
                    var map = maps.CreateMap(runMapInit: true);
                    levels[i] = map;
                    var grid = SEntMan.EnsureComponent<MapGridComponent>(map);
                    // Keep a floor beneath the shooter, with an opening beside it for downward fire.
                    maps.SetTile(map, grid, new Vector2i(8, 8), floor);
                    if (i == 1)
                        maps.SetTile(map, grid, Vector2i.Zero, floor);
                    depths.Add(map, i);
                }

                Assert.That(zLevels.TryAddMapsIntoZNetwork(network, depths), Is.True);
                var origin = new EntityCoordinates(levels[1], new Vector2(0.5f));
                var shooter = SEntMan.SpawnEntity("MobHuman", origin);
                var gun = SEntMan.SpawnEntity("CMUZCasingTestGun", origin);
                Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(shooter, gun), Is.True);
                SEntMan.EnsureComponent<CMUZLevelViewerComponent>(shooter).LookUp = shotOffset > 0;
                Server.System<CMUZLevelShootingSystem>().SetShootDown(shooter, shotOffset < 0);

                var provider = SEntMan.GetComponent<BallisticAmmoProviderComponent>(gun);
                Assert.That(provider.UnspawnedCount, Is.EqualTo(1),
                    "Exercise lazy cartridge creation, which used the projectile's target level.");

                var gunComp = SEntMan.GetComponent<GunComponent>(gun);
                gunComp.NextFire = SGameTiming.CurTime;
                gunComp.ShootCoordinates = new EntityCoordinates(levels[1], new Vector2(3.5f, 0.5f));
                var projectiles = guns.AttemptShoot(shooter, (gun, gunComp));
                Assert.That(projectiles, Has.Count.EqualTo(1), "The shot must actually fire.");
                Assert.That(SEntMan.GetComponent<TransformComponent>(projectiles![0]).MapUid,
                    Is.EqualTo(levels[1 + shotOffset]), "Only the bullet moves to the aimed level.");

                var casings = SEntMan.EntityQueryEnumerator<CMUSpentCasingComponent, TransformComponent>();
                var casingCount = 0;
                while (casings.MoveNext(out var casing, out _, out var casingTransform))
                {
                    if (casingTransform.MapUid != levels[0] &&
                        casingTransform.MapUid != levels[1] &&
                        casingTransform.MapUid != levels[2])
                        continue;

                    casingCount++;
                    Assert.Multiple(() =>
                    {
                        Assert.That(SEntMan.GetComponent<CartridgeAmmoComponent>(casing).Spent, Is.True);
                        Assert.That(casingTransform.MapUid, Is.EqualTo(levels[1]),
                            "The casing must not spawn overhead and fall onto the shooter.");
                        Assert.That(Vector2.Distance(transform.GetWorldPosition(casing), origin.Position),
                            Is.LessThan(0.5f), "The casing must eject beside the physical gun.");
                    });
                }

                Assert.That(casingCount, Is.EqualTo(1));
                Assert.That(provider.Count, Is.Zero);
            }
            finally
            {
                foreach (var level in levels)
                {
                    if (level.Valid && !SEntMan.Deleted(level))
                        SEntMan.DeleteEntity(level);
                }

                SEntMan.DeleteEntity(network);
            }
        });
    }
}
