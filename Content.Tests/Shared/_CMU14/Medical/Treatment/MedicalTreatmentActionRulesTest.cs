using Content.Shared._CMU14.Medical.Foundation;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared._CMU14.Medical.Human.Care;
using NUnit.Framework;

namespace Content.Tests.Shared._CMU14.Medical.Treatment;

[TestFixture]
public sealed class MedicalTreatmentActionRulesTest
{
    [Test]
    public void MapsFieldActionToHumanTreatmentAttempt()
    {
        var request = new MedicalActionRequest(
            default,
            default,
            null,
            MedicalActionKind.ApplyGauze,
            MedicalActionSourceKind.HandItem,
            MedicalActionTargetKind.Region,
            BodyRegion.RightArm);

        var routed = MedicalTreatmentActionRules.TryCreateHumanTreatmentAttempt(request, out var attempt);

        Assert.Multiple(() =>
        {
            Assert.That(routed, Is.True);
            Assert.That(attempt.Kind, Is.EqualTo(TreatmentKind.Gauze));
            Assert.That(attempt.Region, Is.EqualTo(BodyRegion.RightArm));
        });
    }

    [Test]
    public void MapsSyntheticGraftAndLineToFieldTreatmentSemantics()
    {
        var graftRequest = new MedicalActionRequest(
            default,
            default,
            null,
            MedicalActionKind.ApplySyntheticGraft,
            MedicalActionSourceKind.HandItem,
            MedicalActionTargetKind.Region,
            BodyRegion.LeftLeg);
        var lineRequest = graftRequest with { Kind = MedicalActionKind.ApplySurgicalLine };

        var graft = MedicalTreatmentActionRules.TryCreateHumanTreatmentAttempt(graftRequest, out var graftAttempt);
        var line = MedicalTreatmentActionRules.TryCreateHumanTreatmentAttempt(lineRequest, out var lineAttempt);

        Assert.Multiple(() =>
        {
            Assert.That(graft, Is.True);
            Assert.That(line, Is.True);
            Assert.That(graftAttempt.Kind, Is.EqualTo(TreatmentKind.SyntheticGraft));
            Assert.That(lineAttempt.Kind, Is.EqualTo(TreatmentKind.SurgicalLine));
            Assert.That(graftAttempt.Region, Is.EqualTo(BodyRegion.LeftLeg));
            Assert.That(lineAttempt.Region, Is.EqualTo(BodyRegion.LeftLeg));
        });
    }

    [Test]
    public void SurgeryActionDoesNotRouteAsFieldTreatment()
    {
        var request = new MedicalActionRequest(
            default,
            default,
            null,
            MedicalActionKind.RepairInternalBleeding,
            MedicalActionSourceKind.SurgeryTool,
            MedicalActionTargetKind.Region,
            BodyRegion.Chest);

        var routed = MedicalTreatmentActionRules.TryCreateHumanTreatmentAttempt(request, out _);

        Assert.That(routed, Is.False);
    }
}
