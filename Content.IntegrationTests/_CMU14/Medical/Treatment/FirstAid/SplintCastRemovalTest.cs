using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Medical.Stasis;
using Content.Shared.Body.Part;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Medical.Anatomy.Bones;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Treatment.FirstAid;
using Content.Shared.Tag;
using Content.Shared.Verbs;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;

namespace Content.IntegrationTests.CMU14.Medical.Treatment.FirstAid;

// splints had no remove verb at all, and the cast one only showed for the patient once the heal timer was done
[TestFixture]
public sealed class SplintCastRemovalTest : GameTest
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task MedicCanTakeSupportOffAnotherPatient(bool cast)
    {
        var map = await Pair.CreateTestMap();
        EntityUid medic = default, patient = default, part = default;

        await Server.WaitAssertion(() =>
        {
            medic = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            patient = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            SEntMan.EnsureComponent<CMInStasisComponent>(patient);
            SEntMan.System<TagSystem>().AddTag(medic, "InstantDoAfters");

            Assert.That(SEntMan.System<CMUMedicalBodyIndexSystem>().TryGetBodyPart(patient,
                new CMUMedicalBodyPartKey(BodyPartType.Arm, BodyPartSymmetry.Left), out part), Is.True);
            Assert.That(SEntMan.System<SharedBoneSystem>().SeedFracture(part, FractureSeverity.Simple), Is.True);

            var treatment = SEntMan.System<SharedCMUSplintItemSystem>();
            var item = SEntMan.SpawnEntity(cast ? "CMUCastItem" : "CMUSplintItem", map.GridCoords);
            var applied = cast
                ? treatment.ApplyCastToPart((item, SComp<CMUCastItemComponent>(item)), part)
                : treatment.ApplySplintToPart((item, SComp<CMUSplintItemComponent>(item)), part);
            Assert.That(applied, Is.True);
            Assert.That(Supported(), Is.True);

            var text = Loc.GetString(cast ? "cmu-medical-cast-verb-remove" : "cmu-medical-splint-verb-remove");
            var verb = SEntMan.System<SharedVerbSystem>()
                .GetLocalVerbs(patient, medic, typeof(AlternativeVerb))
                .SingleOrDefault(v => v.Text == text);
            Assert.That(verb, Is.Not.Null, "a medic standing over the patient should get the remove verb");
            verb!.Act!.Invoke();
        });

        await RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Supported(), Is.False);
            // pulling it early just loses the treatment, the break is still there
            Assert.That(SEntMan.HasComponent<FractureComponent>(part), Is.True);
        });

        bool Supported() => cast
            ? SEntMan.HasComponent<CMUCastComponent>(part)
            : SEntMan.HasComponent<CMUSplintedComponent>(part);
    }
}
