using System;
using System.IO;
using Content.Server._CMU14.Medical.Human.Damage;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared._CMU14.Medical.Human.Systems;
using Content.Shared.Body.Part;
using NUnit.Framework;

namespace Content.Tests.Server._CMU14.Medical.Human;

[TestFixture]
public sealed class HumanBodyPartSeveranceSystemTest
{
    [Test]
    public void ArmSeveranceCreatesLedgerStumpAndDetachedLimb()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        var transaction = BodyPartSeveranceSystem.CreateLedgerSeveranceTransaction(
            BodyPartType.Arm,
            BodyPartSymmetry.Right);

        var result = HumanMedicalLedger.ApplyTransaction(medical, transaction);
        var region = HumanMedicalLedger.GetRegion(medical, BodyRegion.RightArm);

        Assert.Multiple(() =>
        {
            Assert.That(result.Applied, Is.True);
            Assert.That(region.Presence, Is.EqualTo(LimbPresence.Missing));
            Assert.That(medical.Injuries, Has.Some.Matches<InjuryRecord>(injury =>
                injury.Region == BodyRegion.RightArm &&
                injury.Kind == InjuryKind.Stump));
            Assert.That(medical.BleedSources, Has.Some.Matches<BleedSource>(bleed =>
                bleed.Region == BodyRegion.RightArm &&
                bleed.Kind == BleedKind.Stump &&
                bleed.Flags.HasFlag(BleedFlags.Arterial)));
            Assert.That(medical.DetachedLimbs, Has.Some.Matches<DetachedLimbRecord>(limb =>
                limb.Region == BodyRegion.RightArm));
        });
    }

    [Test]
    public void HeadSeveranceCreatesChestStumpAndDetachedHead()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        var transaction = BodyPartSeveranceSystem.CreateLedgerSeveranceTransaction(
            BodyPartType.Head,
            BodyPartSymmetry.None);

        var result = HumanMedicalLedger.ApplyTransaction(medical, transaction);

        Assert.Multiple(() =>
        {
            Assert.That(result.Applied, Is.True);
            Assert.That(medical.Injuries, Has.Some.Matches<InjuryRecord>(injury =>
                injury.Region == BodyRegion.Chest &&
                injury.Kind == InjuryKind.Stump));
            Assert.That(medical.BleedSources, Has.Some.Matches<BleedSource>(bleed =>
                bleed.Region == BodyRegion.Chest &&
                bleed.Kind == BleedKind.Stump &&
                bleed.Flags.HasFlag(BleedFlags.Arterial)));
            Assert.That(medical.DetachedLimbs, Has.Some.Matches<DetachedLimbRecord>(limb =>
                limb.Region == BodyRegion.Head));
        });
    }

    [Test]
    public void ChestSeveranceCreatesNoLedgerEffects()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        var transaction = BodyPartSeveranceSystem.CreateLedgerSeveranceTransaction(
            BodyPartType.Torso,
            BodyPartSymmetry.None);

        var result = HumanMedicalLedger.ApplyTransaction(medical, transaction);

        Assert.Multiple(() =>
        {
            Assert.That(result.Applied, Is.False);
            Assert.That(medical.Injuries, Is.Empty);
            Assert.That(medical.BleedSources, Is.Empty);
            Assert.That(medical.DetachedLimbs, Is.Empty);
        });
    }

    [Test]
    public void SeveranceSystemHandlesDirectBodyPartRemovalEvents()
    {
        var root = FindRepoRoot();
        var path = Path.Combine(
            root,
            "Content.Server",
            "_CMU14",
            "Medical",
            "Human",
            "Damage",
            "BodyPartSeveranceSystem.cs");

        Assert.That(File.Exists(path), Is.True);
        var text = File.ReadAllText(path);

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("SubscribeLocalEvent<BodyComponent, BodyPartRemovedEvent>"));
            Assert.That(text, Does.Contain("TryComp(body.Owner, out medical)"));
            Assert.That(text, Does.Contain("OnBodyPartRemoved"));
            Assert.That(text, Does.Contain("TryApplyLedgerSeverance"));
        });
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SpaceStation14.slnx")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repository root containing SpaceStation14.slnx.");
    }
}
