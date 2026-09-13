using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Interaction;
using Content.Shared.Interaction;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.Interaction;

[TestFixture]
[TestOf(typeof(RotateToFaceSystem))]
public sealed class RotateToFaceMergeRegressionTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: RotateToFaceMergeCapped
          components:
          - type: MaxRotation
            set: 0
            deviation: 45

        - type: entity
          id: RotateToFaceMergeFree
        """;

    [Test]
    public async Task MaxRotationCapsBeforeStepWhileControlRemainsUnrestricted()
    {
        var map = await Pair.CreateTestMap();
        EntityUid capped = default;
        EntityUid free = default;
        NetEntity cappedNet = default;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var rotate = Server.System<RotateToFaceSystem>();
                var transform = Server.System<SharedTransformSystem>();
                capped = SEntMan.SpawnEntity("RotateToFaceMergeCapped", map.GridCoords);
                free = SEntMan.SpawnEntity("RotateToFaceMergeFree", map.GridCoords);
                cappedNet = SEntMan.GetNetEntity(capped);

                transform.SetWorldRotation(capped, Angle.FromDegrees(40));
                var completed = rotate.TryRotateTo(
                    capped,
                    Angle.FromDegrees(90),
                    frameTime: 1,
                    tolerance: Angle.FromDegrees(0.1),
                    rotationSpeed: Angle.FromDegrees(20).Theta);
                Assert.Multiple(() =>
                {
                    Assert.That(completed, Is.True,
                        "the 90-degree goal must clamp to +45 before applying the 20-degree rotation step");
                    AssertRotation(capped, 45);
                });

                transform.SetWorldRotation(capped, Angle.FromDegrees(-40));
                completed = rotate.TryRotateTo(
                    capped,
                    Angle.FromDegrees(-90),
                    frameTime: 1,
                    tolerance: Angle.FromDegrees(0.1),
                    rotationSpeed: Angle.FromDegrees(20).Theta);
                Assert.Multiple(() =>
                {
                    Assert.That(completed, Is.True);
                    AssertRotation(capped, -45);
                });

                transform.SetWorldRotation(capped, Angle.Zero);
                completed = rotate.TryRotateTo(
                    capped,
                    Angle.FromDegrees(30),
                    frameTime: 1,
                    tolerance: Angle.FromDegrees(0.1),
                    rotationSpeed: Angle.FromDegrees(20).Theta);
                Assert.Multiple(() =>
                {
                    Assert.That(completed, Is.False,
                        "an in-range goal retains the ordinary stepped-rotation behavior");
                    AssertRotation(capped, 20);
                });
                Assert.That(rotate.TryRotateTo(
                    capped,
                    Angle.FromDegrees(30),
                    frameTime: 1,
                    tolerance: Angle.FromDegrees(0.1),
                    rotationSpeed: Angle.FromDegrees(20).Theta), Is.True);
                AssertRotation(capped, 30);

                transform.SetWorldRotation(free, Angle.FromDegrees(40));
                completed = rotate.TryRotateTo(
                    free,
                    Angle.FromDegrees(90),
                    frameTime: 1,
                    tolerance: Angle.FromDegrees(0.1),
                    rotationSpeed: Angle.FromDegrees(20).Theta);
                Assert.Multiple(() =>
                {
                    Assert.That(completed, Is.False);
                    AssertRotation(free, 60,
                        "an entity without MaxRotation must retain the unrestricted upstream goal");
                });

                Server.System<RMCInteractionSystem>().SetMaxRotation(
                    capped,
                    Angle.FromDegrees(15),
                    Angle.FromDegrees(30));
                var max = SEntMan.GetComponent<MaxRotationComponent>(capped);
                Assert.Multiple(() =>
                {
                    Assert.That(max.Set.Degrees, Is.EqualTo(15).Within(0.001));
                    Assert.That(max.Deviation.Degrees, Is.EqualTo(30).Within(0.001));
                });
            });
            await Pair.RunTicksSync(3);

            await Client.WaitAssertion(() =>
            {
                var clientCapped = CEntMan.GetEntity(cappedNet);
                var max = CEntMan.GetComponent<MaxRotationComponent>(clientCapped);
                Assert.Multiple(() =>
                {
                    Assert.That(max.Set.Degrees, Is.EqualTo(15).Within(0.001));
                    Assert.That(max.Deviation.Degrees, Is.EqualTo(30).Within(0.001),
                        "the deployment-style SetMaxRotation update must dirty and replicate both limits");
                });
            });
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                if (free.Valid && SEntMan.EntityExists(free))
                    SEntMan.DeleteEntity(free);
                if (capped.Valid && SEntMan.EntityExists(capped))
                    SEntMan.DeleteEntity(capped);
            });
        }
    }

    private void AssertRotation(EntityUid entity, double expectedDegrees, string? because = null)
    {
        var rotation = Server.System<SharedTransformSystem>().GetWorldRotation(entity);
        Assert.That(rotation.Degrees, Is.EqualTo(expectedDegrees).Within(0.001), because);
    }
}
