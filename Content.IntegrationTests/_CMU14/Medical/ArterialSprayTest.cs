#pragma warning disable RA0002 // the test sets a wound's bleed tier directly

using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Medical.Injuries.Wounds;
using Content.Shared.Body.Part;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared.Damage;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Medical;

[TestFixture]
public sealed class ArterialSprayTest : GameTest
{
    [Test]
    public async Task TreatingAnArterialWoundStopsTheSpray()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var wounds = Server.System<CMUWoundsSystem>();
            var patient = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var torso = Part(patient, BodyPartType.Torso, BodyPartSymmetry.None);

            var slash = new DamageSpecifier();
            slash.DamageDict["Slash"] = 80;
            Assert.That(Server.System<SharedBodyPartHealthSystem>().TryApplyPartDamage(
                patient, torso, slash, impact: DamageImpact.MeleeSlash), Is.True);
            Assert.That(SEntMan.GetComponent<BodyPartWoundComponent>(torso).ExternalBleeding,
                Is.EqualTo(ExternalBleedTier.Arterial));
            Assert.That(wounds.IsSprayingArterialBlood(patient), Is.True);

            wounds.StopSurfaceBleedingOnPart(torso);
            Assert.That(wounds.IsSprayingArterialBlood(patient), Is.False,
                "an arterial wound that's been treated must stop spraying");
        });
    }

    [Test]
    public async Task TourniquetStopsAnArterialLimbSpray()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var wounds = Server.System<CMUWoundsSystem>();
            var patient = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var arm = Part(patient, BodyPartType.Arm, BodyPartSymmetry.Left);
            SEntMan.GetComponent<BodyPartWoundComponent>(arm).ExternalBleeding = ExternalBleedTier.Arterial;
            Assert.That(wounds.IsSprayingArterialBlood(patient), Is.True);

            var tourniquet = SEntMan.SpawnEntity("AU14Tourniquet", map.GridCoords);
            Assert.That(Server.System<SharedCMUTourniquetSystem>().ApplyTourniquetToPart(
                (tourniquet, SEntMan.GetComponent<CMUTourniquetItemComponent>(tourniquet)), arm), Is.True);
            Assert.That(wounds.IsSprayingArterialBlood(patient), Is.False,
                "a tourniquet above an arterial wound must stop the spray");
        });
    }

    [Test]
    public async Task OpenStumpsSprayUntilClamped()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var wounds = Server.System<CMUWoundsSystem>();
            var stumps = Server.System<SharedCMUOpenStumpSystem>();
            var patient = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var torso = Part(patient, BodyPartType.Torso, BodyPartSymmetry.None);

            stumps.AddStump(torso, BodyPartType.Arm, BodyPartSymmetry.Left);
            Assert.That(wounds.IsSprayingArterialBlood(patient), Is.True, "an open stump should spray");

            Assert.That(stumps.TryFindStump(patient, BodyPartType.Arm, BodyPartSymmetry.Left, out var parent, out var stump),
                Is.True);
            stumps.SetClamped(parent, stump, true, null);
            Assert.That(wounds.IsSprayingArterialBlood(patient), Is.False,
                "a tourniquet clamped over the stump must stop the spray");
        });
    }

    private EntityUid Part(EntityUid patient, BodyPartType type, BodyPartSymmetry symmetry)
    {
        Assert.That(Server.System<CMUMedicalBodyIndexSystem>().TryGetBodyPart(patient, new(type, symmetry), out var part),
            Is.True);
        return part;
    }
}
