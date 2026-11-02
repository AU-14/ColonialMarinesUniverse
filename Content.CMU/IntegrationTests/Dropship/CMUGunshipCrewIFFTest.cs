using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Emplacements;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Visor;
using Content.Shared.Buckle;
using Content.Shared.CMU14.Dropship.AttachmentPoint;
using Content.Shared.Inventory;
using Content.Shared.Overlays;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.CMU14.Dropship;

[TestFixture]
public sealed class CMUGunshipCrewIFFTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [TestPrototypes]
    private const string Prototypes = """
- type: entity
  parent: ArmorHelmetM10
  id: CMUTestAdvancedCrewHelmet
  components:
  - type: IntegratedVisors
    visorsToAdd:
    - CMUVisorGunshipCrewAdvanced

- type: entity
  parent: ArmorHelmetM10
  id: CMUTestBasicCrewHelmet
  components:
  - type: IntegratedVisors
    visorsToAdd:
    - CMUVisorGunshipCrew

- type: entity
  id: CMUTestCrewIFFOtherTarget
  components:
  - type: Physics
    bodyType: Static
  - type: Fixtures
    fixtures:
      target:
        shape: !type:PhysShapeAabb
          bounds: "-0.35,-0.35,0.35,0.35"
        layer:
        - BulletImpassable
""";

    [TestCase(0, false)]
    [TestCase(1, true)]
    [TestCase(2, true)]
    public async Task AdvancedRequiresAircrewAndRemovesEffectsWhenTrainingIsLost(int level, bool allowed)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var skills = SEntMan.System<SkillsSystem>();
            skills.SetSkill(user, "CMUSkillAircrew", level);
            var helmet = SEntMan.SpawnEntity("CMUTestAdvancedCrewHelmet", map.GridCoords);
            Assert.That(SEntMan.System<InventorySystem>().TryEquip(user, helmet, "head", force: true), Is.True);
            Cycle(helmet, user);
            Assert.That(SEntMan.HasComponent<CMUAdvancedGunshipCrewVisorComponent>(helmet), Is.EqualTo(allowed));
            Assert.That(SEntMan.HasComponent<ShowHealthBarsComponent>(helmet), Is.EqualTo(allowed));
            Assert.That(SEntMan.HasComponent<ShowHealthIconsComponent>(helmet), Is.EqualTo(allowed));
            Assert.That(SEntMan.HasComponent<CMUGunshipCrewIFFComponent>(user), Is.EqualTo(allowed));
            if (!allowed)
                return;

            Assert.That(SEntMan.GetComponent<CMUGunshipCrewIFFComponent>(user).ToggleAction, Is.Not.Null);
            skills.SetSkill(user, "CMUSkillAircrew", 0);
            Assert.That(SEntMan.HasComponent<CMUAdvancedGunshipCrewVisorComponent>(helmet), Is.False);
            Assert.That(SEntMan.HasComponent<CMUGunshipCrewVisorComponent>(helmet), Is.False);
            Assert.That(SEntMan.HasComponent<ShowHealthBarsComponent>(helmet), Is.False);
            Assert.That(SEntMan.HasComponent<CMUGunshipCrewIFFComponent>(user), Is.False);
        });
    }

    [Test]
    public async Task BasicRemainsUsableWithoutTrainingAndDoesNotGrantMedicalOrIFF()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            SEntMan.System<SkillsSystem>().SetSkill(user, "CMUSkillAircrew", 0);
            var helmet = SEntMan.SpawnEntity("CMUTestBasicCrewHelmet", map.GridCoords);
            Assert.That(SEntMan.System<InventorySystem>().TryEquip(user, helmet, "head", force: true), Is.True);
            Cycle(helmet, user);
            Assert.That(SEntMan.HasComponent<CMUGunshipCrewVisorComponent>(helmet), Is.True);
            Assert.That(SEntMan.HasComponent<CMUAdvancedGunshipCrewVisorComponent>(helmet), Is.False);
            Assert.That(SEntMan.HasComponent<ShowHealthBarsComponent>(helmet), Is.False);
            Assert.That(SEntMan.HasComponent<CMUGunshipCrewIFFComponent>(user), Is.False);
        });
    }

    [Test]
    public async Task InterlockOnlyBlocksOwnShipAsFirstImpactAndCanBeToggled()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            SEntMan.EnsureComponent<DropshipComponent>(map.Grid.Owner);
            var coordinates = map.GridCoords.Offset(new Vector2(0.5f, 0.5f));
            var mount = SEntMan.SpawnEntity("CMUM2CDoorGunDropship", coordinates);
            var user = SEntMan.SpawnEntity("CMMobHuman", coordinates);
            var skills = SEntMan.System<SkillsSystem>();
            skills.SetSkill(user, "CMUSkillAircrew", 1);
            skills.SetSkill(user, "RMCSkillEngineer", 1);
            var helmet = SEntMan.SpawnEntity("CMUTestAdvancedCrewHelmet", coordinates);
            Assert.That(SEntMan.System<InventorySystem>().TryEquip(user, helmet, "head", force: true), Is.True);
            Cycle(helmet, user);
            SEntMan.System<SharedTransformSystem>().SetLocalRotation(mount, Direction.East.ToAngle());
            Assert.That(SEntMan.System<SharedBuckleSystem>().TryBuckle(user, user, mount), Is.True);
            var weapon = SEntMan.GetComponent<WeaponMountComponent>(mount).MountedEntity!.Value;
            var hull = SEntMan.SpawnEntity("WallSolid", coordinates.Offset(new Vector2(4, 0)));

            Assert.That(Shot().Cancelled, Is.True, "An unobstructed shot into the linked hull must be cancelled.");
            Toggle();
            Assert.That(Shot().Cancelled, Is.False, "The wearer can disable the interlock.");
            Toggle();
            Assert.That(Shot().Cancelled, Is.True);

            var other = SEntMan.SpawnEntity("CMUTestCrewIFFOtherTarget", coordinates.Offset(new Vector2(2, 0)));
            Assert.That(Shot().Cancelled, Is.False, "A loose target ahead of the hull is not the ship itself.");
            SEntMan.DeleteEntity(other);
            Assert.That(Shot().Cancelled, Is.True);

            SEntMan.DeleteEntity(hull);
            Assert.That(Shot().Cancelled, Is.False, "Clear fire outside the ship remains allowed.");
            var maps = SEntMan.System<SharedMapSystem>();
            var foreignGrid = maps.CreateGridEntity(map.MapId);
            maps.SetTile(foreignGrid, Vector2i.Zero, map.Tile.Tile);
            var transforms = SEntMan.System<SharedTransformSystem>();
            transforms.SetWorldPosition(foreignGrid, transforms.ToWorldPosition(coordinates.Offset(new Vector2(4, 0))));
            SEntMan.SpawnEntity("WallSolid", new EntityCoordinates(foreignGrid, Vector2.Zero));
            Assert.That(Shot().Cancelled, Is.False, "Another ship's hull is not protected by this interlock.");

            Cycle(helmet, user);
            Assert.That(SEntMan.HasComponent<CMUGunshipCrewIFFComponent>(user), Is.False);

            AttemptShootEvent Shot()
            {
                var origin = SEntMan.GetComponent<TransformComponent>(user).Coordinates;
                var shot = new AttemptShootEvent(user, null, origin, coordinates.Offset(new Vector2(8, 0)));
                SEntMan.EventBus.RaiseLocalEvent(weapon, ref shot);
                return shot;
            }

            void Toggle()
            {
                var toggle = new CMUToggleGunshipCrewIFFActionEvent { Performer = user };
                SEntMan.EventBus.RaiseLocalEvent(user, toggle);
                Assert.That(toggle.Handled, Is.True);
            }
        });
    }

    private void Cycle(EntityUid helmet, EntityUid user)
        => SEntMan.EventBus.RaiseLocalEvent(helmet, new CycleVisorActionEvent { Performer = user });
}
