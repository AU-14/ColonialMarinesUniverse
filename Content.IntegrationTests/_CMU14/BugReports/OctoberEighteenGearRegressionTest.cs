#pragma warning disable RA0002 // setup pokes gun cooldown/aim and the shooter's view directly

using System.Collections.Generic;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared._RMC14.Inventory;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests.CMU14.BugReports;

[TestFixture]
public sealed class OctoberEighteenGearRegressionTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  parent: BaseItem
  id: CMUOct18TestLauncher
  components:
  - type: Gun
    projectileSpeed: 20
    selectedMode: SemiAuto
    availableModes: [SemiAuto]
  - type: BallisticAmmoProvider
    capacity: 1
    whitelist:
      components:
      - Ammo
  - type: ShootAtFixedPoint
    shootArcProj: true
    maxFixedRange: 7

- type: entity
  parent: BaseItem
  id: CMUOct18TestShell
  components:
  - type: Ammo
";

    // max range fires the trigger with no key and the scatter payload only listened for "timer",
    // so the round got deleted with no flare ever coming out
    [Test]
    public async Task FlareRoundDropsALitFlareAtMaxRange()
    {
        var map = await Pair.CreateTestMap();
        EntityUid bullet = default;
        await Server.WaitPost(() =>
        {
            bullet = SEntMan.SpawnEntity("RMCFlareBullet", map.GridCoords);
            var ev = new ProjectileFixedDistanceStopEvent();
            SEntMan.EventBus.RaiseLocalEvent(bullet, ref ev);
        });

        await Pair.RunTicksSync(10);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(bullet), Is.True, "the round should be used up");

            var flares = 0;
            var query = SEntMan.EntityQueryEnumerator<MetaDataComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out var meta, out var xform))
            {
                if (meta.EntityPrototype?.ID != "CMFlare" || xform.MapID != map.MapId)
                    continue;

                flares++;
                SEntMan.DeleteEntity(uid);
            }

            Assert.That(flares, Is.EqualTo(1), "a flare should land where the round stopped");
        });
    }

    // the grenade hit a wall, then got teleported to the clicked spot behind it at fly end
    [Test]
    public async Task LaunchedGrenadeIsNotSnappedThroughAWall()
    {
        var map = await Pair.CreateTestMap();
        EntityUid gun = default, shell = default, wall = default;
        await Server.WaitPost(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            var floor = new Tile(Server.ResolveDependency<ITileDefinitionManager>()["CMFloorSteel"].TileId);
            for (var x = -1; x <= 7; x++)
            for (var y = -1; y <= 1; y++)
                maps.SetTile(map.Grid, new Vector2i(x, y), floor);

            wall = SEntMan.SpawnEntity("WallSolid", maps.GridTileToLocal(map.Grid, map.Grid, new Vector2i(3, 0)));
            gun = SEntMan.SpawnEntity("CMUOct18TestLauncher", maps.GridTileToLocal(map.Grid, map.Grid, Vector2i.Zero));
            shell = SEntMan.SpawnEntity("CMUOct18TestShell", maps.GridTileToLocal(map.Grid, map.Grid, Vector2i.Zero));

            var guns = SEntMan.System<SharedGunSystem>();
            guns.RefreshModifiers(gun);
            SEntMan.GetComponent<GunComponent>(gun).ShootCoordinates =
                maps.GridTileToLocal(map.Grid, map.Grid, new Vector2i(5, 0));
            SEntMan.EventBus.RaiseLocalEvent(gun, new AmmoShotEvent { FiredProjectiles = new List<EntityUid> { shell } });
            Assert.That(SEntMan.HasComponent<ProjectileFixedDistanceComponent>(shell), Is.True);

            // where it ends up after bouncing off the wall
            SEntMan.System<SharedTransformSystem>().SetCoordinates(shell,
                maps.GridTileToLocal(map.Grid, map.Grid, new Vector2i(1, 0)));
            SEntMan.System<SharedPhysicsSystem>().SetLinearVelocity(shell, Vector2.Zero);
        });

        await Pair.RunTicksSync(30);

        await Server.WaitAssertion(() =>
        {
            var transform = SEntMan.System<SharedTransformSystem>();
            Assert.That(SEntMan.HasComponent<ProjectileFixedDistanceComponent>(shell), Is.False, "flight should be over");
            Assert.That(transform.GetWorldPosition(shell).X, Is.LessThan(transform.GetWorldPosition(wall).X),
                "the grenade has to stay on the shooter's side of the wall");

            SEntMan.DeleteEntity(shell);
            SEntMan.DeleteEntity(gun);
            SEntMan.DeleteEntity(wall);
        });
    }

    // flashlights have a 0 damage MeleeWeapon so the holster keys pulled them off the belt like a weapon
    [Test]
    public async Task FlashlightsAreNotUnholsteredAsWeapons()
    {
        await Server.WaitAssertion(() =>
        {
            var flashlight = SEntMan.SpawnEntity("RMCFlashlight", MapCoordinates.Nullspace);
            var knife = SEntMan.SpawnEntity("RMCM5Bayonet", MapCoordinates.Nullspace);

            var flashlightEv = new IsUnholsterableEvent();
            SEntMan.EventBus.RaiseLocalEvent(flashlight, ref flashlightEv);
            var knifeEv = new IsUnholsterableEvent();
            SEntMan.EventBus.RaiseLocalEvent(knife, ref knifeEv);

            Assert.Multiple(() =>
            {
                Assert.That(flashlightEv.Unholsterable, Is.False);
                Assert.That(knifeEv.Unholsterable, Is.True);
            });

            SEntMan.DeleteEntity(flashlight);
            SEntMan.DeleteEntity(knife);
        });
    }

    // secondary started at the belt, so with empty suit storage it drew the same pistol primary would
    [Test]
    public async Task SecondaryUnholsterSkipsWhatPrimaryWouldDraw()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var inventory = SEntMan.System<InventorySystem>();
            var human = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var belt = SEntMan.SpawnEntity("RMCBeltHolsterPistol", map.GridCoords);
            var boots = SEntMan.SpawnEntity("CMBootsBlackFilled", map.GridCoords);
            var pistol = SEntMan.SpawnEntity("RMCWeaponPistolB92FS", map.GridCoords);

            Assert.That(SEntMan.System<SharedContainerSystem>()
                .Insert(pistol, SEntMan.GetComponent<StorageComponent>(belt).Container), Is.True);
            Assert.That(inventory.TryEquip(human, belt, "belt", silent: true, force: true), Is.True);
            Assert.That(inventory.TryEquip(human, boots, "shoes", silent: true, force: true), Is.True);

            SEntMan.System<SharedCMInventorySystem>().OnHolsterSecondary(human);

            var held = SEntMan.System<SharedHandsSystem>().GetActiveItem(human);
            Assert.That(held, Is.Not.Null, "secondary should draw something");
            Assert.That(SEntMan.GetComponent<MetaDataComponent>(held!.Value).EntityPrototype?.ID,
                Is.EqualTo("RMCM5Bayonet"), "the boot knife, the pistol is primary's");

            SEntMan.DeleteEntity(human);
        });
    }

    // grenades come out of the launcher on your own level, so a shot aimed up just lobbed across your own floor
    [Test]
    public async Task LauncherGrenadeAimedUpLeavesOnTheLevelAbove()
    {
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            var zLevels = Server.System<CMUZLevelsSystem>();
            var guns = Server.System<SharedGunSystem>();
            var floor = new Tile(Server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId);
            var network = zLevels.CreateZNetwork();
            var levels = new EntityUid[3];

            try
            {
                var depths = new Dictionary<EntityUid, int>();
                for (var i = 0; i < levels.Length; i++)
                {
                    var level = maps.CreateMap(runMapInit: true);
                    levels[i] = level;
                    var grid = SEntMan.EnsureComponent<MapGridComponent>(level);
                    // floor under the shooter, the level above stays open over them
                    maps.SetTile(level, grid, new Vector2i(8, 8), floor);
                    if (i == 1)
                        maps.SetTile(level, grid, Vector2i.Zero, floor);
                    depths.Add(level, i);
                }

                Assert.That(zLevels.TryAddMapsIntoZNetwork(network, depths), Is.True);
                var origin = new EntityCoordinates(levels[1], new Vector2(0.5f));
                var shooter = SEntMan.SpawnEntity("MobHuman", origin);
                var gun = SEntMan.SpawnEntity("CMUOct18TestLauncher", origin);
                var shell = SEntMan.SpawnEntity("CMUOct18TestShell", origin);
                Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(shooter, gun), Is.True);
                Assert.That(guns.TryBallisticInsert((gun, SEntMan.GetComponent<BallisticAmmoProviderComponent>(gun)), shell, null, true), Is.True);
                SEntMan.EnsureComponent<CMUZLevelViewerComponent>(shooter).LookUp = true;

                guns.RefreshModifiers(gun);
                var gunComp = SEntMan.GetComponent<GunComponent>(gun);
                gunComp.NextFire = SGameTiming.CurTime;
                gunComp.ShootCoordinates = new EntityCoordinates(levels[1], new Vector2(3.5f, 0.5f));
                var projectiles = guns.AttemptShoot(shooter, (gun, gunComp));

                Assert.That(projectiles, Has.Count.EqualTo(1), "the launcher has to fire");
                Assert.That(projectiles![0], Is.EqualTo(shell));
                Assert.That(SEntMan.GetComponent<TransformComponent>(shell).MapUid, Is.EqualTo(levels[2]),
                    "the grenade leaves from the level it was aimed at");
                Assert.That(SEntMan.TryGetComponent(shell, out ProjectileFixedDistanceComponent? fixedDistance), Is.True,
                    "the launcher's fixed range has to apply across levels too");
                Assert.That(fixedDistance!.TargetCoordinates?.MapId,
                    Is.EqualTo(SEntMan.GetComponent<TransformComponent>(levels[2]).MapID));
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

    // a small box that took any small item was a free pocket expander
    [Test]
    public async Task RegulationTapeBoxOnlyTakesTapes()
    {
        await Server.WaitAssertion(() =>
        {
            var storage = SEntMan.System<SharedStorageSystem>();
            var box = SEntMan.SpawnEntity("RMCBoxRegulationTapesEmpty", MapCoordinates.Nullspace);
            var tape = SEntMan.SpawnEntity("RMCUniversalRecorderTape", MapCoordinates.Nullspace);
            var flashlight = SEntMan.SpawnEntity("RMCFlashlight", MapCoordinates.Nullspace);

            Assert.Multiple(() =>
            {
                Assert.That(storage.CanInsert(box, tape, out _, ignoreLocation: true), Is.True);
                Assert.That(storage.CanInsert(box, flashlight, out _, ignoreLocation: true), Is.False);
            });

            SEntMan.DeleteEntity(flashlight);
            SEntMan.DeleteEntity(tape);
            SEntMan.DeleteEntity(box);
        });
    }
}
