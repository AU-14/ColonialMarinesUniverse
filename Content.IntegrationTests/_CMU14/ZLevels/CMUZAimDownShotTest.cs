#pragma warning disable RA0002 // Regression setup controls the shooter's view and gun cooldown.

using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.CMU14.ZLevels;

// the cursor-driven aim-down flag has to actually move the shot, with the hotkey gone
[TestFixture]
[TestOf(typeof(CMUZLevelShootingSystem))]
public sealed class CMUZAimDownShotTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  parent: BaseItem
  id: CMUZAimDownTestPistol
  components:
  - type: Gun
    selectedMode: SemiAuto
    availableModes: [SemiAuto]
  - type: BallisticAmmoProvider
    proto: CMCartridgePistol9mm
    capacity: 1

- type: entity
  parent: CMUZAimDownTestPistol
  id: CMUZAimDownTestRifle
  components:
  - type: Wieldable
";

    [TestCase("CMUZAimDownTestPistol", false, -1, TestName = "AimDownShootsBelow")]
    [TestCase("CMUZAimDownTestRifle", false, 0, TestName = "AimDownNeedsAWieldedGun")]
    [TestCase("CMUZAimDownTestPistol", true, 1, TestName = "LookUpBeatsAimDown")]
    public async Task AimDownPicksTheShotLevel(string gunProto, bool lookUp, int expectedOffset)
    {
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            var zLevels = Server.System<CMUZLevelsSystem>();
            var shooting = Server.System<CMUZLevelShootingSystem>();
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
                    // floor under the shooter, open air beside it to shoot down through
                    maps.SetTile(map, grid, new Vector2i(8, 8), floor);
                    if (i == 1)
                        maps.SetTile(map, grid, Vector2i.Zero, floor);
                    depths.Add(map, i);
                }

                Assert.That(zLevels.TryAddMapsIntoZNetwork(network, depths), Is.True);
                var origin = new EntityCoordinates(levels[1], new Vector2(0.5f));
                var shooter = SEntMan.SpawnEntity("MobHuman", origin);
                var gun = SEntMan.SpawnEntity(gunProto, origin);
                Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(shooter, gun), Is.True);
                SEntMan.EnsureComponent<CMUZLevelViewerComponent>(shooter).LookUp = lookUp;
                shooting.SetAimDown(shooter, true);

                Assert.That(shooting.IsShootDownEnabled(shooter), Is.False,
                    "Aiming with the cursor must not flip the manual shoot-down mode.");
                Assert.That(shooting.GetShotOffset(shooter), Is.EqualTo(expectedOffset));

                var gunComp = SEntMan.GetComponent<GunComponent>(gun);
                gunComp.NextFire = SGameTiming.CurTime;
                gunComp.ShootCoordinates = new EntityCoordinates(levels[1], new Vector2(3.5f, 0.5f));
                var projectiles = guns.AttemptShoot(shooter, (gun, gunComp));
                Assert.That(projectiles, Has.Count.EqualTo(1), "The shot must actually fire.");
                Assert.That(SEntMan.GetComponent<TransformComponent>(projectiles![0]).MapUid,
                    Is.EqualTo(levels[1 + expectedOffset]));

                shooting.SetAimDown(shooter, false);
                Assert.That(shooting.IsAimingDown(shooter), Is.False);
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
