using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Emplacements;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared.Buckle;
using Content.Shared.CMU14.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.CMU14.Dropship;

[TestFixture]
public sealed class CMUM56DDoorGunIFFTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [TestPrototypes]
    private const string Prototypes = """
- type: entity
  parent: CMMobHuman
  id: CMUTestM56DIFFOperator
  components:
  - type: UserIFF
    factions: [ FactionMarine ]

- type: entity
  id: CMUTestM56DIFFTarget
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

- type: entity
  parent: CMUTestM56DIFFTarget
  id: CMUTestM56DIFFFriendly
  components:
  - type: UserIFF
    factions: [ FactionMarine ]
""";

    [TestCase("RMCML66DNestDropship", true)]
    [TestCase("RMCML66DNestMetal", false)]
    public async Task OnlyDoorGunUsesInterlockAndOperatorCanToggleIt(string prototype, bool doorGun)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var coordinates = map.GridCoords.Offset(new Vector2(0.5f, 0.5f));
            var mount = SEntMan.SpawnEntity(prototype, coordinates);
            var user = SEntMan.SpawnEntity("CMUTestM56DIFFOperator", coordinates);
            SEntMan.System<SharedTransformSystem>().SetLocalRotation(mount, Direction.East.ToAngle());
            var buckle = SEntMan.System<SharedBuckleSystem>();
            Assert.That(buckle.TryBuckle(user, user, mount), Is.True);
            var weapon = SEntMan.GetComponent<WeaponMountComponent>(mount).MountedEntity!.Value;
            Assert.That(SEntMan.GetComponent<GunIFFComponent>(weapon).Enabled, Is.EqualTo(!doorGun));
            Assert.That(SEntMan.HasComponent<CMUMountedIFFComponent>(weapon), Is.EqualTo(doorGun));
            Assert.That(SEntMan.HasComponent<CMUMountedIFFOperatorComponent>(user), Is.EqualTo(doorGun));

            var targetCoordinates = coordinates.Offset(new Vector2(4, 0));
            var friendly = SEntMan.SpawnEntity("CMUTestM56DIFFFriendly", targetCoordinates);
            Assert.That(Shot().Cancelled, Is.EqualTo(doorGun));
            if (!doorGun)
                return;

            Assert.That(SEntMan.GetComponent<CMUMountedIFFOperatorComponent>(user).ToggleAction, Is.Not.Null);
            Toggle();
            Assert.That(Shot().Cancelled, Is.False);
            Toggle();
            Assert.That(Shot().Cancelled, Is.True);

            SEntMan.DeleteEntity(friendly);
            SEntMan.SpawnEntity("CMUTestM56DIFFTarget", targetCoordinates);
            Assert.That(Shot().Cancelled, Is.False, "The interlock only protects friendly IFF targets.");

            Toggle();
            buckle.Unbuckle(user, user);
            Assert.That(SEntMan.HasComponent<CMUMountedIFFOperatorComponent>(user), Is.False);
            var next = SEntMan.SpawnEntity("CMUTestM56DIFFOperator", coordinates);
            Assert.That(buckle.TryBuckle(next, next, mount), Is.True);
            Assert.That(SEntMan.GetComponent<CMUMountedIFFComponent>(weapon).Enabled, Is.False,
                "Changing operators must preserve the gun's selected IFF mode.");
            Assert.That(SEntMan.GetComponent<CMUMountedIFFOperatorComponent>(next).ToggleAction, Is.Not.Null);
            SEntMan.DeleteEntity(weapon);
            Assert.That(SEntMan.HasComponent<CMUMountedIFFOperatorComponent>(next), Is.False);

            AttemptShootEvent Shot()
            {
                var origin = SEntMan.GetComponent<TransformComponent>(user).Coordinates;
                var shot = new AttemptShootEvent(user, null, origin, coordinates.Offset(new Vector2(8, 0)));
                SEntMan.EventBus.RaiseLocalEvent(weapon, ref shot);
                return shot;
            }

            void Toggle()
            {
                var toggle = new CMUToggleMountedIFFActionEvent { Performer = user };
                SEntMan.EventBus.RaiseLocalEvent(user, toggle);
                Assert.That(toggle.Handled, Is.True);
            }
        });
    }
}
