using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Body.Part;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts.Events;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Yautja;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Yautja;

// CMU14 Test: Yautja sever resistance and autonomous limb regeneration.
[TestFixture]
public sealed class YautjaLimbRegenerationTest : GameTest
{
    [Test]
    public async Task NonSurgicalSeverNeedsTwoAttemptsAndRegrows()
    {
        EntityUid hunter = default;
        EntityUid arm = default;
        EntityUid map = default;
        var index = SEntMan.System<CMUMedicalBodyIndexSystem>();

        await Server.WaitIdleAsync();
        await Server.WaitAssertion(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            map = maps.CreateMap(out var mapId, runMapInit: true);
            maps.SetPaused(map, false);
            hunter = SEntMan.SpawnEntity("CMUMobYautja", new MapCoordinates(Vector2.Zero, mapId));
            SEntMan.GetComponent<YautjaLimbRegenerationComponent>(hunter).RegrowDelay = TimeSpan.FromSeconds(0.1);
            Assert.That(index.TryGetBodyPart(hunter,
                new CMUMedicalBodyPartKey(BodyPartType.Arm, BodyPartSymmetry.Left),
                out arm), Is.True);

            var first = new BodyPartSeverAttemptEvent(hunter, arm, BodyPartType.Arm);
            SEntMan.EventBus.RaiseLocalEvent(arm, ref first);
            Assert.Multiple(() =>
            {
                Assert.That(first.Cancelled, Is.True);
                Assert.That(first.Succeeded, Is.False);
            });

            var second = new BodyPartSeverAttemptEvent(hunter, arm, BodyPartType.Arm);
            SEntMan.EventBus.RaiseLocalEvent(arm, ref second);
            Assert.Multiple(() =>
            {
                Assert.That(second.Succeeded, Is.True);
                Assert.That(index.TryGetBodyPart(hunter,
                    new CMUMedicalBodyPartKey(BodyPartType.Arm, BodyPartSymmetry.Left),
                    out _), Is.False);
            });
        });

        await Pair.RunTicksSync(Pair.SecondsToTicks(0.3f));
        await Server.WaitAssertion(() =>
        {
            Assert.That(index.TryGetBodyPart(hunter,
                new CMUMedicalBodyPartKey(BodyPartType.Arm, BodyPartSymmetry.Left),
                out var regrown), Is.True);
            Assert.That(regrown, Is.Not.EqualTo(arm));
            SEntMan.DeleteEntity(map);
        });
    }
}
