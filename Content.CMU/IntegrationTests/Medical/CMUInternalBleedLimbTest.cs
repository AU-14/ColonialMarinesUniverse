using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.CMU14.Chemistry.Effects.Positive;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
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
    public async Task TranexamicSuppressesPersistentBleedingUntilItWearsOff()
    {
        var map = await Pair.CreateTestMap();
        EntityUid patient = default;
        EntityUid arm = default;
        await Server.WaitAssertion(() =>
        {
            patient = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            Assert.That(Server.System<CMUMedicalBodyIndexSystem>().TryGetBodyPart(patient,
                new(BodyPartType.Arm, BodyPartSymmetry.Left), out arm), Is.True);
            var wounds = Server.System<SharedCMUWoundsSystem>();
            wounds.SeedInternalBleed(arm, "vascular:test", 0.3f);
            ApplyTranexamic(patient);
            wounds.RecomputeInternalBleed(arm);
            Assert.That(SEntMan.HasComponent<InternalBleedingComponent>(arm), Is.False,
                "recomputing while medicated must keep the untreated bleed suppressed");
        });

        await Server.WaitRunTicks(420);
        await Server.WaitAssertion(() =>
        {
            var bleed = SEntMan.GetComponent<InternalBleedingComponent>(arm);
            Assert.That(bleed.Source, Is.EqualTo("vascular:test"));
            Assert.That(bleed.BloodlossPerSecond, Is.EqualTo(0.3f).Within(0.001f));
            SEntMan.DeleteEntity(patient);
        });
    }

    [Test]
    public async Task ClampingWhileMedicatedPreventsBleedingFromReturning()
    {
        var map = await Pair.CreateTestMap();
        EntityUid patient = default;
        EntityUid arm = default;
        await Server.WaitAssertion(() =>
        {
            patient = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            Assert.That(Server.System<CMUMedicalBodyIndexSystem>().TryGetBodyPart(patient,
                new(BodyPartType.Arm, BodyPartSymmetry.Left), out arm), Is.True);
            var wounds = Server.System<SharedCMUWoundsSystem>();
            wounds.SeedInternalBleed(arm, "blunt", 0.3f);
            ApplyTranexamic(patient);
            wounds.SuppressInternalBleed(arm);
        });

        await Server.WaitRunTicks(420);
        await Server.WaitAssertion(() =>
        {
            Server.System<SharedCMUWoundsSystem>().RecomputeInternalBleed(arm);
            Assert.That(SEntMan.HasComponent<InternalBleedingComponent>(arm), Is.False,
                "the drug wearing off must not undo definitive surgical treatment");
            SEntMan.DeleteEntity(patient);
        });
    }

    private void ApplyTranexamic(EntityUid patient)
    {
        var reagent = SProtoMan.Index<ReagentPrototype>("CMUTranexamicAcid");
        var effect = reagent.Metabolisms!.Metabolisms["Bloodstream"].Effects.OfType<Hemostatic>().Single();
        var context = new ReagentEffectContext(reagent, null, null, null,
            new ReagentQuantity(reagent.ID, FixedPoint2.New(1)), "Bloodstream", null,
            ReagentEffectOrigin.Metabolism);
        Assert.That(Server.System<SharedEntityEffectsSystem>().TryApplyEffect(patient, effect,
            reagentContext: context), Is.True);
    }

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
