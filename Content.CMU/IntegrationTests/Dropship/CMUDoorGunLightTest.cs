using Content.Client.CMU14.Dropship.AttachmentPoint;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Buckle;
using Content.Shared.CMU14.Dropship.AttachmentPoint;
using Content.Shared.CombatMode;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.CMU14.Dropship;

[TestFixture]
public sealed class CMUDoorGunLightTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [TestCase("CMUM2CDoorGunDropship", Direction.West)]
    [TestCase("CMUUTESCDoorGunDropship", Direction.North)]
    [TestCase("RMCML66DNestDropship", Direction.South)]
    [TestCase("CMUGAU19MDoorGunDropship", Direction.East)]
    public async Task BarrelLightFollowsReplicatedAimAndReturnsOnDismount(string prototype, Direction facing)
    {
        var map = await Pair.CreateTestMap();
        NetEntity gunNet = default;
        EntityUid user = default;
        await Server.WaitAssertion(() =>
        {
            var gun = SEntMan.SpawnEntity(prototype, map.GridCoords);
            user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            SEntMan.System<SharedTransformSystem>().SetLocalRotation(gun, facing.ToAngle());
            Assert.That(SEntMan.System<SharedBuckleSystem>().TryBuckle(user, user, gun), Is.True);
            SEntMan.System<SharedCombatModeSystem>().SetInCombatMode(user, true);
            var visual = SEntMan.GetComponent<CMUDoorGunVisualComponent>(gun);
            visual.Operator = user;
            visual.AimOffset = Angle.FromDegrees(45);
            SEntMan.Dirty(gun, visual);
            gunNet = SEntMan.GetNetEntity(gun);
        });

        await Pair.RunUntilSynced();
        await Client.WaitAssertion(() => AssertLight(45));

        await Server.WaitAssertion(() => SEntMan.System<SharedBuckleSystem>().Unbuckle(user, user));
        await Pair.RunUntilSynced();
        await Client.WaitAssertion(() =>
        {
            AssertLight(0);
            var gun = CEntMan.GetEntity(gunNet);
            var light = CEntMan.GetComponent<CMUDoorGunLightComponent>(gun).Light!.Value;
            CEntMan.RemoveComponent<CMUDoorGunVisualComponent>(gun);
            Assert.That(CEntMan.GetComponent<PointLightComponent>(light).Enabled, Is.False);
            Assert.That(CEntMan.HasComponent<CMUDoorGunLightComponent>(gun), Is.False);
        });

        void AssertLight(double degrees)
        {
            CEntMan.System<CMUDoorGunVisualSystem>().FrameUpdate(0);
            var gun = CEntMan.GetEntity(gunNet);
            var light = CEntMan.GetComponent<CMUDoorGunLightComponent>(gun).Light!.Value;
            var transforms = CEntMan.System<SharedTransformSystem>();
            var lamp = CEntMan.GetComponent<PointLightComponent>(light);
            var visual = CEntMan.GetComponent<CMUDoorGunVisualComponent>(gun);
            var expectedRotation = transforms.GetWorldRotation(gun) + Angle.FromDegrees(degrees);
            Assert.That(lamp.Enabled, Is.True);
            Assert.That(lamp.LightMask!.Value.Id, Is.EqualTo("ConeSingle"));
            Assert.That(CEntMan.GetComponent<TransformComponent>(light).ParentUid, Is.EqualTo(gun));
            Assert.That(Math.Abs(Angle.ShortestDistance(
                SharedPointLightSystem.GetMaskWorldRotation(lamp, transforms.GetWorldRotation(light)),
                expectedRotation).Degrees), Is.LessThan(0.001));
            var expectedPosition = transforms.GetWorldPosition(gun) + expectedRotation.ToWorldVec() * visual.BarrelLightDistance;
            Assert.That(System.Numerics.Vector2.Distance(transforms.GetWorldPosition(light), expectedPosition), Is.LessThan(0.001f));
        }
    }
}
