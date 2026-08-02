using Content.Shared._CMU14.Medical.Anatomy.BodyParts;
using Content.Shared._CMU14.Medical.Core;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._CMU14.Medical.Anatomy.BodyParts;

[TestFixture]
public sealed class CMUProstheticLimbTraitTest
{
    [Test]
    public async Task RoundStartProstheticTraitsCreateCompleteLimbs()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid human = default;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            entMan.EnsureComponent<CMUProstheticLeftArmComponent>(human);
            entMan.EnsureComponent<CMUProstheticRightArmComponent>(human);
            entMan.EnsureComponent<CMUProstheticLeftLegComponent>(human);
            entMan.EnsureComponent<CMUProstheticRightLegComponent>(human);
        });

        await pair.RunTicksSync(2);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var handsSystem = entMan.System<SharedHandsSystem>();
            var medical = entMan.System<CMUMedicalBodyIndexSystem>();

            AssertRoboticPart(entMan, medical, human, BodyPartType.Arm, BodyPartSymmetry.Left);
            AssertRoboticPart(entMan, medical, human, BodyPartType.Hand, BodyPartSymmetry.Left);
            AssertRoboticPart(entMan, medical, human, BodyPartType.Arm, BodyPartSymmetry.Right);
            AssertRoboticPart(entMan, medical, human, BodyPartType.Hand, BodyPartSymmetry.Right);
            AssertRoboticPart(entMan, medical, human, BodyPartType.Leg, BodyPartSymmetry.Left);
            AssertRoboticPart(entMan, medical, human, BodyPartType.Foot, BodyPartSymmetry.Left);
            AssertRoboticPart(entMan, medical, human, BodyPartType.Leg, BodyPartSymmetry.Right);
            AssertRoboticPart(entMan, medical, human, BodyPartType.Foot, BodyPartSymmetry.Right);

            var hands = entMan.GetComponent<HandsComponent>(human);
            Assert.Multiple(() =>
            {
                Assert.That(
                    handsSystem.TryGetHand(
                        (human, hands),
                        SharedBodySystem.GetPartSlotContainerId("left_hand"),
                        out _),
                    Is.True);
                Assert.That(
                    handsSystem.TryGetHand(
                        (human, hands),
                        SharedBodySystem.GetPartSlotContainerId("right_hand"),
                        out _),
                    Is.True);
            });
        });

        await server.WaitPost(() => server.EntMan.DeleteEntity(human));
        await pair.CleanReturnAsync();
    }

    private static void AssertRoboticPart(
        IEntityManager entMan,
        CMUMedicalBodyIndexSystem medical,
        EntityUid body,
        BodyPartType type,
        BodyPartSymmetry symmetry)
    {
        Assert.That(
            medical.TryGetBodyPart(body, new CMUMedicalBodyPartKey(type, symmetry), out var part),
            Is.True,
            $"missing {symmetry} {type}");
        Assert.That(entMan.HasComponent<CMURoboticLimbComponent>(part), Is.True, $"non-robotic {symmetry} {type}");
    }
}
