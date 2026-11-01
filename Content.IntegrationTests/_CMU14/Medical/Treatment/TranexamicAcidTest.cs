using Content.IntegrationTests.Fixtures;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Medical.Treatment;

[TestFixture]
public sealed class TranexamicAcidTest : GameTest
{
    [Test]
    public async Task TranexamicAcidStopsBleedingWhileInTheBloodstream()
    {
        var map = await Pair.CreateTestMap();
        EntityUid part = default;

        await Server.WaitAssertion(() =>
        {
            var patient = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            Assert.That(Server.System<CMUMedicalBodyIndexSystem>()
                .TryGetBodyPart(patient, new(BodyPartType.Torso, BodyPartSymmetry.None), out part), Is.True);

            var slash = new DamageSpecifier();
            slash.DamageDict["Slash"] = 80;
            Assert.That(Server.System<SharedBodyPartHealthSystem>().TryApplyPartDamage(
                patient, part, slash, impact: DamageImpact.MeleeSlash), Is.True);
            Assert.That(SEntMan.GetComponent<BodyPartWoundComponent>(part).ExternalBleeding,
                Is.Not.EqualTo(ExternalBleedTier.None), "the wound should be bleeding before treatment");

            Assert.That(Server.System<BloodstreamSystem>().TryAddToBloodstream(patient,
                new([new("CMUTranexamicAcid", FixedPoint2.New(10))])), Is.True);
        });

        await Pair.RunSeconds(3f);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<BodyPartWoundComponent>(part).ExternalBleeding,
                Is.EqualTo(ExternalBleedTier.None), "tranexamic acid in the blood should stop the bleeding");
        });
    }
}
