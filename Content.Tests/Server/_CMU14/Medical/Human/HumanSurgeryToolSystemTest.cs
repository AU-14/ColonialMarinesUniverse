using System;
using System.IO;
using NUnit.Framework;

namespace Content.Tests.Server._CMU14.Medical.Human;

[TestFixture]
public sealed class HumanSurgeryToolSystemTest
{
    [Test]
    public void AutoSurgeryToolPathSelectsProcedureBeforeCm13PatientGate()
    {
        var text = ReadSurgeryToolSystem();
        var procedureSelection = text.IndexOf("TryCreateSurgeryAttempt(user, patient, tool, medical", StringComparison.Ordinal);
        var firstGate = text.IndexOf("TryValidateCM13SurgeryConditions(user, patient, tool, medical, attempt", StringComparison.Ordinal);

        Assert.Multiple(() =>
        {
            Assert.That(firstGate, Is.GreaterThanOrEqualTo(0));
            Assert.That(firstGate, Is.GreaterThan(procedureSelection));
            Assert.That(text, Does.Contain("cmu-medical-surgery-patient-not-lying"));
            Assert.That(text, Does.Not.Contain("TryValidatePatientForSurgery"));
        });
    }

    [Test]
    public void SurgeryDoAfterRevalidatesPatientBeforeApplyingProcedure()
    {
        var text = ReadSurgeryToolSystem();
        var revalidation = text.IndexOf("TryValidateCM13SurgeryConditions(args.User, patient.Owner, tool, patient.Comp, attempt", StringComparison.Ordinal);
        var apply = text.IndexOf("TryApplyServerBackedSurgery(patient.Owner, tool, patient.Comp, attempt, args.User)", StringComparison.Ordinal);
        var armorRevalidation = text.IndexOf("TryValidateArmorForSurgery(patient.Owner, attempt.Region", StringComparison.Ordinal);

        Assert.Multiple(() =>
        {
            Assert.That(revalidation, Is.GreaterThanOrEqualTo(0));
            Assert.That(apply, Is.GreaterThan(revalidation));
            Assert.That(armorRevalidation, Is.GreaterThan(revalidation));
            Assert.That(apply, Is.GreaterThan(armorRevalidation));
        });
    }

    [Test]
    public void SurgeryGateRecognizesCm13SurfacePainAndFailureRules()
    {
        var text = ReadSurgeryToolSystem();

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("TryValidateCM13SurgeryConditions"));
            Assert.That(text, Does.Contain("TryResolveSurgeryOutcome"));
            Assert.That(text, Does.Contain("ApplySurgeryFailure"));
            Assert.That(text, Does.Contain("HumanSurgeryRules.GetFailureChance"));
            Assert.That(text, Does.Contain("HumanSurgeryRules.GetPainFailureChance"));
            Assert.That(text, Does.Contain("SurgeryToolQuality.BadSubstitute"));
            Assert.That(text, Does.Contain("SurgerySurfaceQuality.Awful"));
            Assert.That(text, Does.Contain("_random.Prob"));
            Assert.That(text, Does.Contain("IsLyingDownForSurgery"));
            Assert.That(text, Does.Contain("BuckleComponent"));
            Assert.That(text, Does.Contain("StrapPosition.Down"));
            Assert.That(text, Does.Contain("CMOperatingTableComponent"));
            Assert.That(text, Does.Contain("SleepingComponent"));
            Assert.That(text, Does.Contain("RMCUnconsciousComponent"));
        });
    }

    [Test]
    public void SurgeryGateRecognizesWorkingAnestheticInternalsAsSourceAwareAnesthesia()
    {
        var text = ReadSurgeryToolSystem();

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("SharedInternalsSystem"));
            Assert.That(text, Does.Contain("GasTankComponent"));
            Assert.That(text, Does.Contain("Gas.NitrousOxide"));
            Assert.That(text, Does.Contain("TryGetInhaledAnesthesia"));
            Assert.That(text, Does.Contain("_sleeping.TrySleeping"));
            Assert.That(text, Does.Contain("HasRupturedLung(medical)"));
        });
    }

    [Test]
    public void AdvancedSurgeryRoutesForeignObjectsAndEscharThroughRealSystems()
    {
        var text = ReadSurgeryToolSystem();

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("TryCreateForeignObjectRemovalAttempt(patient, tool, medical, region"));
            Assert.That(text, Does.Contain("TryExtractSurgicalShrapnelFromRegion"));
            Assert.That(text, Does.Contain("TryCreateEscharRemovalAttempt(patient, tool, medical, region"));
            Assert.That(text, Does.Contain("ClearEscharPartMarker"));
            Assert.That(text, Does.Not.Contain("deep foreign bodies currently live on body-part shrapnel"));
            Assert.That(text, Does.Not.Contain("eschar is still represented by body-part components"));
        });
    }

    [Test]
    public void FixOVeinAndSurgicalLineUseCm13InternalBleedToolQuality()
    {
        var text = ReadSurgeryToolSystem();

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("CMUFixOVeinTag"));
            Assert.That(text, Does.Contain("CMSurgicalLineTag"));
            Assert.That(text, Does.Contain("SurgeryToolQuality.Ideal"));
            Assert.That(text, Does.Contain("SurgeryToolQuality.Substitute"));
            Assert.That(text, Does.Contain("TryCreateInternalBleedRepairAttempt"));
        });
    }

    [Test]
    public void SurgeryToolSubscriptionsUseSpecificToolOwners()
    {
        var text = ReadSurgeryToolSystem();

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("SubscribeLocalEvent<CMScalpelComponent, AfterInteractEvent>(OnScalpelAfterInteract)"));
            Assert.That(text, Does.Contain("SubscribeLocalEvent<CMHemostatComponent, AfterInteractEvent>(OnHemostatAfterInteract)"));
            Assert.That(text, Does.Contain("SubscribeLocalEvent<CMRetractorComponent, AfterInteractEvent>(OnRetractorAfterInteract)"));
            Assert.That(text, Does.Not.Contain("SubscribeLocalEvent<CMSurgeryToolComponent, AfterInteractEvent>"));
            Assert.That(text, Does.Not.Contain("OnGenericSurgeryToolAfterInteract"));
        });
    }

    [Test]
    public void AutoSurgeryToolPathChecksBlockingArmorBeforeStarting()
    {
        var text = ReadSurgeryToolSystem();
        var procedureSelection = text.IndexOf("TryCreateSurgeryAttempt(user, patient, tool, medical", StringComparison.Ordinal);
        var armorGate = text.IndexOf("TryValidateArmorForSurgery(patient, attempt.Region", StringComparison.Ordinal);
        var doAfter = text.IndexOf("_doAfter.TryStartDoAfter(doAfter)", StringComparison.Ordinal);

        Assert.Multiple(() =>
        {
            Assert.That(armorGate, Is.GreaterThan(procedureSelection));
            Assert.That(doAfter, Is.GreaterThan(armorGate));
            Assert.That(text, Does.Contain("CMHardArmorComponent"));
            Assert.That(text, Does.Contain("cmu-medical-surgery-remove-helmet"));
            Assert.That(text, Does.Contain("cmu-medical-surgery-remove-armor"));
            Assert.That(text, Does.Contain("SlotFlags.HEAD"));
            Assert.That(text, Does.Contain("SlotFlags.OUTERCLOTHING"));
        });
    }

    [Test]
    public void AutoSurgeryToolPathSkipsMissingRegionsExceptStumps()
    {
        var text = ReadSurgeryToolSystem();
        var stumpSelection = text.IndexOf("TryCreateStumpRepairAttempt(tool, medical, region", StringComparison.Ordinal);
        var nonStumpGate = text.IndexOf("CanPerformNonStumpSurgeryOnRegion(medical, region)", StringComparison.Ordinal);
        var incisionSelection = text.IndexOf("TryCreateIncisionAttempt(patient, tool, medical, region", StringComparison.Ordinal);
        var repairSelection = text.IndexOf("TryCreateRepairAttempt(patient, tool, medical, region", StringComparison.Ordinal);

        Assert.Multiple(() =>
        {
            Assert.That(stumpSelection, Is.GreaterThanOrEqualTo(0));
            Assert.That(nonStumpGate, Is.GreaterThan(stumpSelection));
            Assert.That(incisionSelection, Is.GreaterThan(nonStumpGate));
            Assert.That(repairSelection, Is.GreaterThan(nonStumpGate));
            Assert.That(text, Does.Contain("regionState.Presence == LimbPresence.Present"));
            Assert.That(text, Does.Contain("SurgeryStepKind.RepairStump"));
        });
    }

    [Test]
    public void OrganClampAfterInteractHasSingleOwningSubscriber()
    {
        const string subscription = "SubscribeLocalEvent<CMUOrganClampComponent, AfterInteractEvent>";

        var root = FindRepoRoot();
        var surgeryText = ReadSurgeryToolSystem();
        var bleedControlPath = Path.Combine(
            root,
            "Content.Server",
            "_CMU14",
            "Medical",
            "Human",
            "Care",
            "HumanBleedControlTreatmentSystem.cs");

        Assert.That(File.Exists(bleedControlPath), Is.True);
        var bleedControlText = File.ReadAllText(bleedControlPath);
        var totalSubscriptions =
            CountOccurrences(surgeryText, subscription) +
            CountOccurrences(bleedControlText, subscription);

        Assert.Multiple(() =>
        {
            Assert.That(totalSubscriptions, Is.EqualTo(1));
            Assert.That(bleedControlText, Does.Contain(subscription));
            Assert.That(bleedControlText, Does.Contain("TryHandleSurgeryToolInteraction"));
            Assert.That(surgeryText, Does.Not.Contain(subscription));
        });
    }

    private static string ReadSurgeryToolSystem()
    {
        var root = FindRepoRoot();
        var path = Path.Combine(
            root,
            "Content.Server",
            "_CMU14",
            "Medical",
            "Human",
            "Surgery",
            "HumanSurgeryToolSystem.cs");

        Assert.That(File.Exists(path), Is.True);
        return File.ReadAllText(path);
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;

        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
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
