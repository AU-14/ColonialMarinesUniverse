using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Content.Tests.Shared._CMU14.Medical.Human;

[TestFixture]
public sealed class HumanMedicalSupplyContractTest
{
    [Test]
    public void MarineMedicalVendorExposesBasicSelfCareLoop()
    {
        var text = Read("Resources/Prototypes/_RMC14/Entities/Structures/Machines/Vending/medical.yml");

        AssertActiveIds(
            text,
            "Resources/Prototypes/_RMC14/Entities/Structures/Machines/Vending/medical.yml",
            "CMTricordrazineAutoInjectorNoSkill",
            "CMHealthAnalyzer",
            "CMOintment10",
            "CMGauze10",
            "CMUSplintItem");
    }

    [Test]
    public void BasicFirstAidKitsExposeLedgerFieldCareLoop()
    {
        var text = Read("Resources/Prototypes/_RMC14/Entities/Objects/Medical/firstaidkits.yml");

        AssertActiveIds(
            text,
            "Resources/Prototypes/_RMC14/Entities/Objects/Medical/firstaidkits.yml",
            "CMHealthAnalyzer",
            "CMTricordrazineAutoInjectorNoSkill",
            "CMInaprovalineAutoInjector",
            "CMGauze10",
            "CMOintment10",
            "CMUSplintItem",
            "AU14TramadolAutoInjector",
            "AU14OxycodoneAutoInjector",
            "CMSurgicalLine");
    }

    [Test]
    public void CorpsmanRacksExposeCm13FieldMedicalLoop()
    {
        var text = Read("Resources/Prototypes/_RMC14/Entities/Structures/Machines/Vending/Squad/medic.yml");

        AssertActiveIds(
            text,
            "Resources/Prototypes/_RMC14/Entities/Structures/Machines/Vending/Squad/medic.yml",
            "CMHealthAnalyzer",
            "CMBloodPackFull",
            "CMStasisBagFolded",
            "CMRollerBedSpawnFolded",
            "CMAdvFirstAidKitFilled",
            "CMTraumaKit10",
            "CMBurnKit10",
            "CMTraumaKit10",
            "CMTraumaKit10",
            "CMBurnKit10",
            "CMUSplintItem",
            "AU14PillCanisterTramadol",
            "AU14PillCanisterOxycodone",
            "AU14TramadolAutoInjector",
            "AU14OxycodoneAutoInjector",
            "AU14CMDefibrillator",
            "CMSurgicalLine",
            "CMUFixOVein",
            "CMSurgicalCaseFilled");
    }

    [Test]
    public void MedicalDepartmentVendorsExposeDoctorAndSurgeryLoop()
    {
        var crewText = Read("Resources/Prototypes/_RMC14/Entities/Structures/Machines/Vending/Crew/medical.yml");
        var supplyText = Read("Resources/Prototypes/_RMC14/Entities/Structures/Machines/Vending/medical.yml");

        AssertActiveIds(
            crewText,
            "Resources/Prototypes/_RMC14/Entities/Structures/Machines/Vending/Crew/medical.yml",
            "RMCVendorBundleCrewMedicalEssentialsDoctor",
            "CMBeltMedicalFilled",
            "CMBeltMedicBagFilled",
            "RMCPouchFirstAidSplintsGauzeOintment",
            "RMCPouchFirstAidPills",
            "RMCPouchFirstAidInjectors");

        AssertActiveIds(
            supplyText,
            "Resources/Prototypes/_RMC14/Entities/Structures/Machines/Vending/medical.yml",
            "CMHealthAnalyzer",
            "AU14CMDefibrillator",
            "CMStasisBagFolded",
            "CMBloodPackFull",
            "CMUSplintItem",
            "CMUCastItem",
            "CMSurgicalLine",
            "CMUFixOVein",
            "CMSurgicalCaseFilled",
            "AU14PillCanisterTramadol",
            "AU14PillCanisterOxycodone",
            "AU14TramadolAutoInjector",
            "AU14OxycodoneAutoInjector");
    }

    [Test]
    public void SurgicalTrayExposesLedgerBleedSuppressionItems()
    {
        var text = Read("Resources/Prototypes/_RMC14/Entities/Objects/Medical/surgical_tray.yml");

        AssertActiveIds(
            text,
            "Resources/Prototypes/_RMC14/Entities/Objects/Medical/surgical_tray.yml",
            "CMSurgicalLine",
            "CMSynthGraft",
            "CMUFixOVein");
    }

    [Test]
    public void FixOVeinPrototypeIsSurgeryToolNotFieldTreatment()
    {
        const string path = "Resources/Prototypes/_CMU14/Medical/items/fix_o_vein.yml";
        var text = Read(path);

        AssertActiveFields(
            text,
            path,
            "- type: CMSurgeryTool",
            "- type: Tag",
            "- CMUFixOVein");

        AssertNoActiveFields(
            text,
            path,
            "- type: WoundTreater",
            "- type: MedicallyUnskilledDoAfter",
            "cmuMedicalAction:",
            "ApplyFixOVein");
    }

    [Test]
    public void FieldCareAndUtilityItemsDoNotExposeSurgeryToolRoles()
    {
        AssertNoActiveFields(
            Read("Resources/Prototypes/_RMC14/Entities/Objects/Medical/healing.yml"),
            "Resources/Prototypes/_RMC14/Entities/Objects/Medical/healing.yml",
            "- type: CMSurgeryTool",
            "- type: CMHemostat",
            "- type: CMCautery",
            "- type: CMUOrganClamp");

        AssertNoActiveFields(
            Read("Resources/Prototypes/_RMC14/Entities/Objects/Power/coil.yml"),
            "Resources/Prototypes/_RMC14/Entities/Objects/Power/coil.yml",
            "- type: CMSurgeryTool",
            "- type: CMHemostat",
            "- type: CMCautery",
            "- type: CMUOrganClamp");

        AssertNoActiveFields(
            Read("Resources/Prototypes/_RMC14/Entities/Clothing/Head/headband.yml"),
            "Resources/Prototypes/_RMC14/Entities/Clothing/Head/headband.yml",
            "- type: CMSurgeryTool",
            "- type: CMUOrganClamp");
    }

    [Test]
    public void MedicalRolesHaveCm13SkillSplit()
    {
        var corpsman = Read("Resources/Prototypes/_RMC14/Roles/Jobs/Marines/hospital_corpsman.yml");
        var doctor = Read("Resources/Prototypes/_RMC14/Roles/Jobs/Medical/doctor.yml");
        var fieldDoctor = Read("Resources/Prototypes/_RMC14/Roles/Jobs/Medical/field_doctor.yml");

        Assert.Multiple(() =>
        {
            AssertActiveFields(
                corpsman,
                "Resources/Prototypes/_RMC14/Roles/Jobs/Marines/hospital_corpsman.yml",
                "RMCSkillMedical: 2",
                "RMCSkillSurgery: 1",
                "Chemicals: 120");

            Assert.That(corpsman, Does.Contain("- type: CMVendorUser"));

            AssertActiveFields(
                doctor,
                "Resources/Prototypes/_RMC14/Roles/Jobs/Medical/doctor.yml",
                "RMCSkillMedical: 3",
                "RMCSkillSurgery: 2");

            AssertActiveFields(
                fieldDoctor,
                "Resources/Prototypes/_RMC14/Roles/Jobs/Medical/field_doctor.yml",
                "RMCSkillMedical: 3",
                "RMCSkillSurgery: 2");
        });
    }

    private static void AssertActiveIds(string text, string path, params string[] ids)
    {
        Assert.Multiple(() =>
        {
            foreach (var id in ids)
            {
                var pattern = $@"^\s*-\s*id:\s*{Regex.Escape(id)}(?:\s|$)";
                var regex = new Regex(pattern, RegexOptions.Multiline);
                Assert.That(
                    regex.IsMatch(text),
                    Is.True,
                    $"{path} must contain active vendor/storage entry '{id}'.");
            }
        });
    }

    private static void AssertActiveFields(string text, string path, params string[] fields)
    {
        Assert.Multiple(() =>
        {
            foreach (var field in fields)
            {
                var pattern = $@"^\s*{Regex.Escape(field)}(?:\s|$)";
                var regex = new Regex(pattern, RegexOptions.Multiline);
                Assert.That(
                    regex.IsMatch(text),
                    Is.True,
                    $"{path} must contain active field '{field}'.");
            }
        });
    }

    private static void AssertNoActiveFields(string text, string path, params string[] fields)
    {
        Assert.Multiple(() =>
        {
            foreach (var field in fields)
            {
                var pattern = $@"^\s*{Regex.Escape(field)}(?:\s|$)";
                var regex = new Regex(pattern, RegexOptions.Multiline);
                Assert.That(
                    regex.IsMatch(text),
                    Is.False,
                    $"{path} must not contain active field '{field}'.");
            }
        });
    }

    private static string Read(string relative)
    {
        return File.ReadAllText(Path.Combine(FindRepoRoot(), relative));
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
