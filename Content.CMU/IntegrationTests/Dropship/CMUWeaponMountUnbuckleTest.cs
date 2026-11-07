using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Emplacements;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Interaction;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Physics;

namespace Content.IntegrationTests.CMU14.Dropship;

[TestFixture]
public sealed class CMUWeaponMountUnbuckleTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [TestPrototypes]
    private const string Prototypes = """
- type: entity
  id: CMUTestShortReachGunner
  parent: CMMobHuman
  components:
  - type: Buckle
    range: 0.01
""";

    [TestCase("CMUM2CDoorGunDropship")]
    [TestCase("RMCML66DNestMetal")]
    public async Task OperatorCanUseUnbuckleAlertWithoutReachingMount(string prototype)
    {
        var map = await Pair.CreateTestMap();
        EntityUid gun = default, user = default;
        await Server.WaitAssertion(() =>
        {
            gun = SEntMan.SpawnEntity(prototype, map.GridCoords);
            user = SEntMan.SpawnEntity("CMUTestShortReachGunner",
                SEntMan.GetComponent<TransformComponent>(gun).Coordinates);
            SEntMan.System<SharedTransformSystem>().SetLocalRotation(gun, Direction.West.ToAngle());
            Assert.That(SEntMan.System<SharedBuckleSystem>().TryBuckle(user, user, gun), Is.True);
        });

        await Pair.RunTicksSync(30);
        await Server.WaitAssertion(() =>
        {
            var buckle = SEntMan.GetComponent<BuckleComponent>(user);
            // The short-reach prototype reproduces an obstructed, offset door mount.
            // Use its center so overlapping mount fixtures cannot bypass the reach check.
            SEntMan.RemoveComponent<FixturesComponent>(gun);
            Assert.That(SEntMan.System<SharedInteractionSystem>()
                .InRangeUnobstructed(user, gun, buckle.Range), Is.False);

            var other = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(10, 0)));
            Assert.That(SEntMan.System<SharedBuckleSystem>().TryUnbuckle(user, other, popup: false), Is.False);

            var alert = new UnbuckleAlertEvent { User = user, AlertId = "Buckled" };
            SEntMan.EventBus.RaiseLocalEvent(user, alert);
            Assert.That(alert.Handled, Is.True);
            Assert.That(buckle.BuckledTo, Is.Null);
            Assert.That(SEntMan.GetComponent<WeaponMountComponent>(gun).User, Is.Null);
            Assert.That(SEntMan.HasComponent<WeaponControllerComponent>(user), Is.False);
        });
    }
}
