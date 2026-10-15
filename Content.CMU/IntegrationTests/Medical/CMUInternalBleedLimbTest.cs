using Content.IntegrationTests.Fixtures;
using Content.Shared.Body.Part;
using Content.Shared.CMU14.Medical.Anatomy.Bones;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Medical;

[TestFixture]
[TestOf(typeof(SharedCMUWoundsSystem))]
public sealed class CMUInternalBleedLimbTest : GameTest
{
    [Test]
    public async Task ALimbCarriesOneInternalBleed()
    {
        await Server.WaitAssertion(() =>
        {
            var index = SEntMan.System<CMUMedicalBodyIndexSystem>();
            var wounds = SEntMan.System<SharedCMUWoundsSystem>();
            var patient = SEntMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            Assert.That(index.TryGetBodyPart(patient, new(BodyPartType.Arm, BodyPartSymmetry.Left), out var arm), Is.True);
            Assert.That(index.TryGetBodyPart(patient, new(BodyPartType.Hand, BodyPartSymmetry.Left), out var hand), Is.True);
            Assert.That(index.TryGetBodyPart(patient, new(BodyPartType.Leg, BodyPartSymmetry.Right), out var leg), Is.True);
            Assert.That(index.TryGetBodyPart(patient, new(BodyPartType.Foot, BodyPartSymmetry.Right), out var foot), Is.True);

            wounds.SeedInternalBleed(hand, "vascular:test", 0.25f);
            wounds.SeedInternalBleed(arm, "blunt", 0.3f);

            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<InternalBleedingComponent>(hand), Is.False,
                    "a hand's bleed is tracked on its arm");
                Assert.That(SEntMan.GetComponent<InternalBleedingComponent>(arm).BloodlossPerSecond, Is.EqualTo(0.3f).Within(0.001f),
                    "the arm carries one bleed at the worst rate");
            });

            Assert.That(SEntMan.System<SharedBoneSystem>().SeedFracture(foot, FractureSeverity.Shattered), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<InternalBleedingComponent>(foot), Is.False,
                    "a fractured foot bleeds on its leg");
                Assert.That(SEntMan.HasComponent<InternalBleedingComponent>(leg), Is.True);
            });

            SEntMan.DeleteEntity(patient);
        });
    }
}
