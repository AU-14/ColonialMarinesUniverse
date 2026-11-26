using Content.IntegrationTests.Fixtures;
using Content.Shared.Alert;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Injuries.Shrapnel;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Medical;

[TestFixture]
public sealed class CMUMedicalAlertTest : GameTest
{
    [Test]
    public async Task ShrapnelAlertTracksRemainingAttachedFragments()
    {
        var map = await Pair.CreateTestMap();
        EntityUid patient = default, left = default, right = default;
        await Server.WaitAssertion(() =>
        {
            patient = SEntMan.SpawnAtPosition("CMMobHuman", map.GridCoords);
            var anatomy = Server.System<CMUMedicalBodyIndexSystem>();
            Assert.That(anatomy.TryGetBodyPart(patient, new(BodyPartType.Arm, BodyPartSymmetry.Left), out left), Is.True);
            Assert.That(anatomy.TryGetBodyPart(patient, new(BodyPartType.Arm, BodyPartSymmetry.Right), out right), Is.True);
            var fragments = Server.System<SharedCMUShrapnelSystem>();
            Assert.That(fragments.AddShrapnel(left, 1, 12f), Is.True);
            Assert.That(fragments.AddShrapnel(right, 1, 12f), Is.True);
        });
        await RunTicksSync(2);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.System<SharedCMUShrapnelSystem>().TryClearShrapnel(right), Is.True);
        });
        await RunTicksSync(2);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.System<AlertsSystem>().IsShowingAlert(patient, "CMUEmbeddedShrapnel"), Is.True);
            Assert.That(Server.System<DetachableOrganSystem>().Detach(left), Is.Not.Null);
        });
        await RunTicksSync(2);
        await Server.WaitAssertion(() =>
            Assert.That(Server.System<AlertsSystem>().IsShowingAlert(patient, "CMUEmbeddedShrapnel"), Is.False));
    }
}
