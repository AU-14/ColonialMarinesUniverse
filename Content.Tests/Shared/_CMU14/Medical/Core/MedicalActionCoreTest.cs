using System;
using Content.Shared._CMU14.Medical.Foundation;
using NUnit.Framework;

namespace Content.Tests.Shared._CMU14.Medical.Core;

[TestFixture]
public sealed class MedicalActionCoreTest
{
    [Test]
    public void MedicalActionEnumsUseByteStorage()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Enum.GetUnderlyingType(typeof(MedicalActionKind)), Is.EqualTo(typeof(byte)));
            Assert.That(Enum.GetUnderlyingType(typeof(MedicalActionTargetKind)), Is.EqualTo(typeof(byte)));
            Assert.That(Enum.GetUnderlyingType(typeof(MedicalActionSourceKind)), Is.EqualTo(typeof(byte)));
            Assert.That(Enum.GetUnderlyingType(typeof(MedicalActionOutcome)), Is.EqualTo(typeof(byte)));
            Assert.That(Enum.GetUnderlyingType(typeof(MedicalActionFlags)), Is.EqualTo(typeof(ushort)));
        });
    }

    [Test]
    public void FieldTreatmentActionsUseRegionDoAfters()
    {
        var gauze = MedicalActionRules.GetDefaultFlags(MedicalActionKind.ApplyGauze);
        var suture = MedicalActionRules.GetDefaultFlags(MedicalActionKind.ApplySuture);

        Assert.Multiple(() =>
        {
            Assert.That(MedicalActionRules.IsFieldTreatment(MedicalActionKind.ApplyGauze), Is.True);
            Assert.That(MedicalActionRules.RequiresRegion(MedicalActionKind.ApplyGauze), Is.True);
            Assert.That(gauze & MedicalActionFlags.RequiresBodyPartPicker, Is.Not.EqualTo(MedicalActionFlags.None));
            Assert.That(gauze & MedicalActionFlags.RequiresDoAfter, Is.Not.EqualTo(MedicalActionFlags.None));
            Assert.That(gauze & MedicalActionFlags.StopsBleeding, Is.Not.EqualTo(MedicalActionFlags.None));
            Assert.That(suture & MedicalActionFlags.StopsBleeding, Is.Not.EqualTo(MedicalActionFlags.None));
        });
    }

    [Test]
    public void SurgeryActionsUseDoAfterAndAccessFlags()
    {
        var ibRepair = MedicalActionRules.GetDefaultFlags(MedicalActionKind.RepairInternalBleeding);
        var setBone = MedicalActionRules.GetDefaultFlags(MedicalActionKind.SetBone);

        Assert.Multiple(() =>
        {
            Assert.That(MedicalActionRules.IsSurgery(MedicalActionKind.RepairInternalBleeding), Is.True);
            Assert.That(MedicalActionRules.RequiresRegion(MedicalActionKind.RepairInternalBleeding), Is.True);
            Assert.That(ibRepair & MedicalActionFlags.RequiresDoAfter, Is.Not.EqualTo(MedicalActionFlags.None));
            Assert.That(ibRepair & MedicalActionFlags.RequiresDeepAccess, Is.Not.EqualTo(MedicalActionFlags.None));
            Assert.That(ibRepair & MedicalActionFlags.StopsBleeding, Is.Not.EqualTo(MedicalActionFlags.None));
            Assert.That(setBone & MedicalActionFlags.RequiresDeepAccess, Is.EqualTo(MedicalActionFlags.None));
            Assert.That(setBone & MedicalActionFlags.RequiresDoAfter, Is.Not.EqualTo(MedicalActionFlags.None));
        });
    }

    [Test]
    public void FieldSuppressionItemsRequireFollowupTreatment()
    {
        var graft = MedicalActionRules.GetDefaultFlags(MedicalActionKind.ApplySyntheticGraft);
        var line = MedicalActionRules.GetDefaultFlags(MedicalActionKind.ApplySurgicalLine);

        Assert.Multiple(() =>
        {
            Assert.That(MedicalActionRules.IsFieldTreatment(MedicalActionKind.ApplySyntheticGraft), Is.True);
            Assert.That(MedicalActionRules.RequiresRegion(MedicalActionKind.ApplySyntheticGraft), Is.True);
            Assert.That(graft & MedicalActionFlags.SuppressesBleeding, Is.Not.EqualTo(MedicalActionFlags.None));
            Assert.That(graft & MedicalActionFlags.RequiresFollowupTreatment, Is.Not.EqualTo(MedicalActionFlags.None));
            Assert.That(line & MedicalActionFlags.SuppressesBleeding, Is.Not.EqualTo(MedicalActionFlags.None));
            Assert.That(line & MedicalActionFlags.RequiresFollowupTreatment, Is.Not.EqualTo(MedicalActionFlags.None));
        });
    }

    [Test]
    public void FixOVeinIsNotAFieldTreatmentAction()
    {
        Assert.That(Enum.GetNames(typeof(MedicalActionKind)), Does.Not.Contain("ApplyFixOVein"));
    }

    [Test]
    public void AttemptEventStartsUnhandledAndCanCarryRouteResult()
    {
        var request = new MedicalActionRequest(
            default,
            default,
            null,
            MedicalActionKind.ApplyGauze,
            MedicalActionSourceKind.HandItem,
            MedicalActionTargetKind.Region);

        var attempt = new MedicalActionAttemptEvent(request);
        var result = MedicalActionRules.RequiresDoAfter(request.Kind);

        attempt.Result = result;
        attempt.Handled = true;

        Assert.Multiple(() =>
        {
            Assert.That(attempt.Request, Is.EqualTo(request));
            Assert.That(attempt.Handled, Is.True);
            Assert.That(attempt.Result, Is.EqualTo(result));
            Assert.That(attempt.Cancelled, Is.False);
        });
    }
}
