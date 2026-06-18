using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Content.Tests.Shared._CMU14.Medical.Human;

[TestFixture]
public sealed class HumanMedicalLegacyGuardTest
{
    private static readonly string[] SourceRoots =
    {
        "Content.Shared",
        "Content.Server",
        "Content.Client",
        "Content.Tests",
        "Content.IntegrationTests",
        "Resources/Prototypes",
        "Docs",
    };

    private static readonly string[] TextExtensions =
    {
        ".cs",
        ".yml",
        ".yaml",
        ".ftl",
        ".md",
    };

    private static readonly string[] ForbiddenBridgeTerms =
    {
        "Content.Shared._CMU14.Medical.Human.Compatibility",
        "LegacyHumanMedicalDamageAdapter",
        "SharedLegacyHumanMedicalDamageAdapterSystem",
        "HumanMedicalCompatibilityTest",
    };

    private static readonly string[] ForbiddenSoftCutTerms =
    {
        "cmu.medical.human_ledger_authoritative",
        "HumanLedgerAuthoritative",
        "humanLedgerAuthoritative",
    };

    private static readonly string[] LedgerCollectionWrites =
    {
        ".Regions =",
        ".Injuries =",
        ".Organs =",
        ".BleedSources =",
        ".DetachedLimbs =",
        ".NextInjuryId =",
        ".NextBleedSourceId =",
        ".NextDetachedLimbId =",
    };

    private static readonly string[] ForbiddenHumanMedicalLedgerDirtyCalls =
    {
        "Dirty(patient, medical)",
        "Dirty(uid, medical)",
        "Dirty(body.Owner, body.Comp)",
        "Dirty(body, ent.Comp)",
    };

    private static readonly string[] ForbiddenPrunedGameplayTerms =
    {
        "CMUTraumaGovernor",
        "TraumaGovernor",
        "trauma_governor",
        "CMUActionTraumaGovernor",
        "CMUPlainGauze",
        "CMUPlainTraumaDressing",
        "CMUCoagulantPowder",
        "CMUBurnGel",
        "CMUpgradedBurnKit",
        "CMUpgradedTraumaKit",
        "CMUFieldBleedControl",
        "CMUFieldTreatmentBaseKind",
        "cmu-medical-surgery-patient-not-controlled",
    };

    private static readonly string[] LegacyClinicalAuthorityTerms =
    {
        "CMUHumanMedicalComponent",
        "BodyPartHealthComponent",
        "BodyPartWoundComponent",
        "InternalBleedingComponent",
        "FractureComponent",
        "OrganHealthComponent",
    };

    private static readonly string GuardTestFile =
        Normalize("Content.Tests/Shared/_CMU14/Medical/Human/HumanMedicalLegacyGuardTest.cs");

    private static readonly string[] AllowedLedgerWriteFiles =
    {
        Normalize("Content.Shared/_CMU14/Medical/Human/Components/HumanMedicalComponent.cs"),
        Normalize("Content.Shared/_CMU14/Medical/Human/Systems/HumanMedicalLedger.cs"),
        Normalize("Content.Tests/Shared/_CMU14/Medical/Human/HumanMedicalLedgerTest.cs"),
        GuardTestFile,
    };

    [Test]
    public void HumanMedicalCompatibilityBridgeDoesNotExist()
    {
        AssertNoMatches(ForbiddenBridgeTerms, includeDocs: false);
    }

    [Test]
    public void HumanMedicalSoftCutCVarDoesNotExist()
    {
        AssertNoMatches(ForbiddenSoftCutTerms, includeDocs: false);
    }

    [Test]
    public void OnlyLedgerOwnsDirectHotCollectionWrites()
    {
        var matches = FindMatches(LedgerCollectionWrites, includeDocs: false)
            .Where(match => !AllowedLedgerWriteFiles.Contains(Normalize(match.Path)))
            .ToList();

        Assert.That(matches, Is.Empty, FormatMatches(matches));
    }

    [Test]
    public void HumanMedicalLedgerIsNeverDirtiedDirectly()
    {
        AssertNoMatches(ForbiddenHumanMedicalLedgerDirtyCalls, includeDocs: false);
    }

    [Test]
    public void PrunedMedicalGameplayDoesNotExist()
    {
        AssertNoMatches(ForbiddenPrunedGameplayTerms, includeDocs: false);
    }

    [Test]
    public void LegacyAuthorityGuardStaysCodeOwned()
    {
        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(Path.Combine(
                FindRepoRoot(),
                "Content.Shared/_CMU14/Medical/Human/Components/HumanMedicalComponent.cs")), Is.True);
            AssertNoMatches(LegacyClinicalAuthorityTerms, includeDocs: false);
        });
    }

    [Test]
    public void HumanMobPrototypesUseNativeLedgerComponents()
    {
        var root = FindRepoRoot();
        var humanPrototypeFiles = new[]
        {
            "Resources/Prototypes/Entities/Mobs/Species/human.yml",
            "Resources/Prototypes/_RMC14/Entities/Mobs/Species/human.yml",
            "Resources/Prototypes/_RMC14/Entities/Mobs/Species/base.yml",
        };

        foreach (var relative in humanPrototypeFiles)
        {
            var text = File.ReadAllText(Path.Combine(root, relative));

            Assert.Multiple(() =>
            {
                Assert.That(text, Does.Contain("- type: HumanMedical"), relative);
                Assert.That(text, Does.Contain("- type: HumanMedicalSummary"), relative);
                Assert.That(text, Does.Not.Contain("- type: CMSurgeryTarget"), relative);
                Assert.That(text, Does.Not.Contain("enum.CMSurgeryUIKey.Key"), relative);
                Assert.That(text, Does.Not.Contain("type: CMSurgeryBui"), relative);
                Assert.That(text, Does.Not.Contain("- type: CMUHumanMedical"), relative);
            });
        }
    }

    [Test]
    public void HumanBodyPartPrototypesAreLedgerAnchorsOnly()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(
            root,
            "Resources/Prototypes/_CMU14/Medical/body_parts/human.yml"));

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("- type: AnatomyRegion"));
            Assert.That(text, Does.Not.Contain("- type: BodyPartHealth"));
            Assert.That(text, Does.Not.Contain("- type: Bone"));
        });
    }

    [Test]
    public void HumanOrganPrototypesDoNotCarryOldOrganHealthAuthority()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(
            root,
            "Resources/Prototypes/_CMU14/Medical/organs/human.yml"));

        Assert.That(text, Does.Not.Contain("- type: OrganHealth"));
    }

    [Test]
    public void HealthScannerExtensionDoesNotReadLegacyHumanMedicalAuthority()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(
            root,
            "Content.Shared/_CMU14/Medical/Human/Diagnostics/Devices/HealthScannerCMUExtensionSystem.cs"));

        AssertDoesNotContainAny(text, LegacyClinicalAuthorityTerms);
    }

    [Test]
    public void BodyScannerDoesNotReadLegacyHumanMedicalAuthority()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(
            root,
            "Content.Server/_CMU14/Medical/Machines/CMUBodyScannerSystem.cs"));

        AssertDoesNotContainAny(text, LegacyClinicalAuthorityTerms);
    }

    [Test]
    public void MedicalExamineDoesNotReadLegacyHumanMedicalAuthority()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(
            root,
            "Content.Shared/_CMU14/Medical/Human/Diagnostics/Examine/CMUMedicalExamineSystem.cs"));

        AssertDoesNotContainAny(text, LegacyClinicalAuthorityTerms);
        Assert.That(text, Does.Not.Contain("ExternalBleedTier"));
    }

    [Test]
    public void DetailedMedicalExamineUsesLedgerBleedSeverity()
    {
        var root = FindRepoRoot();
        var files = new[]
        {
            "Content.Shared/_CMU14/Medical/Human/Diagnostics/Examine/CMUDetailedPhysicalExamineDoAfterEvent.cs",
            "Content.Server/_CMU14/Medical/Human/Diagnostics/Examine/CMUDetailedMedicalExamineSystem.cs",
            "Content.Client/_CMU14/Medical/Human/Diagnostics/Examine/CMUInspectInjuriesWindow.xaml.cs",
        };

        Assert.Multiple(() =>
        {
            foreach (var relative in files)
            {
                var text = File.ReadAllText(Path.Combine(root, relative));
                Assert.That(text, Does.Not.Contain("CMUHumanMedicalComponent"), relative);
                Assert.That(text, Does.Not.Contain("ExternalBleedTier"), relative);
            }
        });
    }

    [Test]
    public void RmcSuppressorSystemsUseHumanMedicalLedgerMarker()
    {
        var root = FindRepoRoot();
        var files = new[]
        {
            "Content.Shared/_RMC14/Medical/Wounds/SharedWoundsSystem.cs",
            "Content.Shared/_RMC14/HealthExaminable/RMCHealthExaminableSystem.cs",
            "Content.Shared/_RMC14/Medical/Examine/RMCMedicalExamineSystem.cs",
            "Content.Server/_RMC14/Medical/Surgery/CMSurgerySystem.cs",
        };

        Assert.Multiple(() =>
        {
            foreach (var relative in files)
            {
                var text = File.ReadAllText(Path.Combine(root, relative));
                Assert.That(text, Does.Contain("HumanMedicalComponent"), relative);
                Assert.That(text, Does.Not.Contain("CMUHumanMedicalComponent"), relative);
            }
        });
    }

    [Test]
    public void LedgerFieldTreatmentInterceptOwnsRmcWoundTreatersForHumans()
    {
        var root = FindRepoRoot();
        var ledgerPath = Path.Combine(
            root,
            "Content.Server/_CMU14/Medical/Human/Care/HumanFieldTreatmentSystem.cs");
        var hubPath = Path.Combine(
            root,
            "Content.Server/_CMU14/Medical/Treatment/CMUMedicInteractHubSystem.cs");

        Assert.That(File.Exists(ledgerPath), Is.True);

        var ledgerText = File.ReadAllText(ledgerPath);

        Assert.Multiple(() =>
        {
            Assert.That(ledgerText, Does.Contain("CMUWoundTreaterInterceptEvent"));
            Assert.That(ledgerText, Does.Contain("HumanTreatmentSystem"));
            Assert.That(ledgerText, Does.Contain("MedicalTreatmentActionRules"));
            Assert.That(ledgerText, Does.Contain("MedicalActionKind.ApplySurgicalLine"));
            Assert.That(ledgerText, Does.Contain("MedicalActionKind.ApplySyntheticGraft"));
            Assert.That(ledgerText, Does.Contain("CMUMedicalAction"));
            Assert.That(ledgerText, Does.Contain("CMUStopsArterialBleeding"));
            Assert.That(ledgerText, Does.Contain("HumanMedicalComponent"));
            AssertDoesNotContainAny(ledgerText, LegacyClinicalAuthorityTerms);
            Assert.That(File.Exists(hubPath), Is.False);
        });
    }

    [Test]
    public void LedgerSplintTreatmentOwnsSplintItemsForHumans()
    {
        var root = FindRepoRoot();
        var path = Path.Combine(
            root,
            "Content.Server/_CMU14/Medical/Human/Care/HumanOrthopedicTreatmentSystem.cs");

        Assert.That(File.Exists(path), Is.True);

        var text = File.ReadAllText(path);

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("SubscribeLocalEvent<CMUSplintItemComponent, AfterInteractEvent>"));
            Assert.That(text, Does.Contain("CMUSplintItemComponent"));
            Assert.That(text, Does.Contain("HumanTreatmentSystem"));
            Assert.That(text, Does.Contain("MedicalTreatmentActionRules"));
            Assert.That(text, Does.Contain("MedicalActionKind.ApplySplint"));
            Assert.That(text, Does.Contain("HumanMedicalComponent"));
            AssertDoesNotContainAny(text, LegacyClinicalAuthorityTerms);
        });
    }

    [Test]
    public void LedgerCastTreatmentOwnsCastItemsForHumans()
    {
        var root = FindRepoRoot();
        var path = Path.Combine(
            root,
            "Content.Server/_CMU14/Medical/Human/Care/HumanOrthopedicTreatmentSystem.cs");

        Assert.That(File.Exists(path), Is.True);

        var text = File.ReadAllText(path);

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("SubscribeLocalEvent<CMUCastItemComponent, AfterInteractEvent>"));
            Assert.That(text, Does.Contain("CMUCastItemComponent"));
            Assert.That(text, Does.Contain("HumanTreatmentSystem"));
            Assert.That(text, Does.Contain("MedicalTreatmentActionRules"));
            Assert.That(text, Does.Contain("MedicalActionKind.ApplyCast"));
            Assert.That(text, Does.Contain("HumanMedicalComponent"));
            AssertDoesNotContainAny(text, LegacyClinicalAuthorityTerms);
        });
    }

    [Test]
    public void LedgerClampTreatmentOwnsOrganClampItemsForHumans()
    {
        var root = FindRepoRoot();
        var path = Path.Combine(
            root,
            "Content.Server/_CMU14/Medical/Human/Care/HumanBleedControlTreatmentSystem.cs");

        Assert.That(File.Exists(path), Is.True);

        var text = File.ReadAllText(path);

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("CMUOrganClampComponent"));
            Assert.That(text, Does.Contain("HumanTreatmentSystem"));
            Assert.That(text, Does.Contain("MedicalTreatmentActionRules"));
            Assert.That(text, Does.Contain("MedicalActionKind.ApplyClamp"));
            Assert.That(text, Does.Contain("HumanMedicalComponent"));
            AssertDoesNotContainAny(text, LegacyClinicalAuthorityTerms);
        });
    }

    [Test]
    public void LedgerTourniquetTreatmentOwnsTourniquetItemsForHumans()
    {
        var root = FindRepoRoot();
        var path = Path.Combine(
            root,
            "Content.Shared/_CMU14/Medical/Human/Care/SharedCMUTourniquetSystem.cs");

        Assert.That(File.Exists(path), Is.True);

        var text = File.ReadAllText(path);

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("CMUTourniquetItemComponent"));
            Assert.That(text, Does.Contain("HumanTreatmentSystem"));
            Assert.That(text, Does.Contain("MedicalTreatmentActionRules"));
            Assert.That(text, Does.Contain("MedicalActionKind.ApplyTourniquet"));
            Assert.That(text, Does.Contain("MedicalActionKind.RemoveTourniquet"));
            Assert.That(text, Does.Contain("HumanMedicalComponent"));
            AssertDoesNotContainAny(text, LegacyClinicalAuthorityTerms);
            Assert.That(text, Does.Not.Contain("CMUTourniquetComponent"));
            Assert.That(text, Does.Not.Contain("CMUNecroticComponent"));
        });
    }

    [Test]
    public void LedgerFieldBleedControlOwnsBleedControlItemsForHumans()
    {
        var root = FindRepoRoot();
        var path = Path.Combine(
            root,
            "Content.Server/_CMU14/Medical/Human/Care/HumanFieldTreatmentSystem.cs");

        Assert.That(File.Exists(path), Is.True);

        var text = File.ReadAllText(path);

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("HumanTreatmentSystem"));
            Assert.That(text, Does.Contain("MedicalTreatmentActionRules"));
            Assert.That(text, Does.Contain("MedicalBleedControlRules"));
            Assert.That(text, Does.Contain("MedicalActionKind.ApplyGauze"));
            Assert.That(text, Does.Contain("HumanMedicalComponent"));
            AssertDoesNotContainAny(text, LegacyClinicalAuthorityTerms);
            Assert.That(text, Does.Not.Contain("CMUWoundsSystem"));
            Assert.That(text, Does.Not.Contain("StopSurfaceBleedingOnPart"));
            Assert.That(text, Does.Not.Contain("CMUMedicalMixingBase"));
            Assert.That(File.Exists(Path.Combine(
                root,
                "Content.Server/_CMU14/Medical/Treatment/CMUFieldBleedControlSystem.cs")), Is.False);
            Assert.That(File.Exists(Path.Combine(
                root,
                "Content.Shared/_CMU14/Medical/Treatment/CMUFieldBleedControlComponent.cs")), Is.False);
            Assert.That(File.Exists(Path.Combine(
                root,
                "Content.Shared/_CMU14/Medical/Treatment/CMUFieldBleedControlDoAfterEvent.cs")), Is.False);
        });
    }

    [Test]
    public void FieldCraftingMenuIsDeleted()
    {
        var root = FindRepoRoot();
        var deletedFiles = new[]
        {
            "Content.Server/_CMU14/Medical/FieldTreatments/CMUMedicalFieldMixingSystem.cs",
            "Content.Shared/_CMU14/Medical/FieldTreatments/CMUMedicalFieldCraftingUI.cs",
            "Content.Client/_CMU14/Medical/FieldTreatments/CMUMedicalFieldCraftingBui.cs",
            "Content.Client/_CMU14/Medical/FieldTreatments/CMUMedicalFieldCraftingInputSystem.cs",
            "Content.Client/_CMU14/Medical/FieldTreatments/CMUMedicalFieldCraftingMenu.xaml",
            "Content.Client/_CMU14/Medical/FieldTreatments/CMUMedicalFieldCraftingMenu.xaml.cs",
        };

        Assert.Multiple(() =>
        {
            foreach (var deleted in deletedFiles)
                Assert.That(File.Exists(Path.Combine(root, deleted)), Is.False, deleted);
        });

        AssertNoMatches(
            new[]
            {
                "CMUMedicalFieldCrafting",
                "CMUMedicalIngredientComponent",
                "- type: CMUMedicalIngredient",
                "enum.CMUMedicalFieldCraftingUI.Key",
                "CMUMedicalMixingBase",
                "field-mixed",
                "field mixing",
            },
            includeDocs: false);
    }

    [Test]
    public void HitLocationTargetsHumanMedicalLedgerBodies()
    {
        var root = FindRepoRoot();
        var path = Path.Combine(
            root,
            "Content.Shared/_CMU14/Medical/Targeting/SharedHitLocationSystem.cs");

        Assert.That(File.Exists(path), Is.True);

        var text = File.ReadAllText(path);

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("HumanMedicalComponent"));
            Assert.That(text, Does.Not.Contain("CMUHumanMedicalComponent"));
        });
    }

    [Test]
    public void BodyPartSeveranceCreatesLedgerStumpsWithoutDirectBloodloss()
    {
        var root = FindRepoRoot();
        var path = Path.Combine(
            root,
            "Content.Server/_CMU14/Medical/Human/Damage/BodyPartSeveranceSystem.cs");

        Assert.That(File.Exists(path), Is.True);

        var text = File.ReadAllText(path);

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("HumanMedicalComponent"));
            Assert.That(text, Does.Contain("LimbLossRules.CreateTraumaticSeverance"));
            Assert.That(text, Does.Contain("SharedHumanMedicalSystem"));
            Assert.That(text, Does.Not.Contain("CMUHumanMedicalComponent"));
            Assert.That(text, Does.Not.Contain("BodyPartHealthComponent"));
            Assert.That(text, Does.Not.Contain("SharedBodyPartHealthSystem"));
            Assert.That(text, Does.Not.Contain("BodyPartWoundComponent"));
            Assert.That(text, Does.Not.Contain("DamageableSystem"));
            Assert.That(text, Does.Not.Contain("StumpBleedDamage"));
            Assert.That(text, Does.Not.Contain("\"Bloodloss\""));
        });
    }

    [Test]
    public void SeveranceCosmeticsUseLedgerHumanComponent()
    {
        var root = FindRepoRoot();
        var path = Path.Combine(
            root,
            "Content.Server/_CMU14/Medical/Presentation/CMUSeveranceCosmeticSystem.cs");

        Assert.That(File.Exists(path), Is.True);

        var text = File.ReadAllText(path);

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("HumanMedicalComponent"));
            Assert.That(text, Does.Not.Contain("CMUHumanMedicalComponent"));
            Assert.That(text, Does.Not.Contain("InternalBleedingComponent"));
        });
    }

    [Test]
    public void MedicalSpeedPenaltiesUseLedgerHumanState()
    {
        var root = FindRepoRoot();
        var files = new[]
        {
            "Content.Shared/_CMU14/Medical/Human/Effects/SharedCMUMedicalSpeedSystem.cs",
            "Content.Server/_CMU14/Medical/Human/Effects/CMUMedicalSpeedSystem.cs",
        };

        Assert.Multiple(() =>
        {
            foreach (var relative in files)
            {
                var text = File.ReadAllText(Path.Combine(root, relative));
                Assert.That(text, Does.Contain("HumanMedicalComponent"), relative);
                AssertDoesNotContainAny(text, LegacyClinicalAuthorityTerms);
            }

            var feedback = File.ReadAllText(Path.Combine(
                root,
                "Content.Server/_CMU14/Medical/Human/Effects/CMUPainFeedbackSystem.cs"));
            Assert.That(feedback, Does.Contain("ActivePainFeedbackComponent"));
            Assert.That(
                feedback,
                Does.Not.Contain("EntityQueryEnumerator<CMUPainFeedbackComponent, PainShockComponent, HumanMedicalComponent"));
        });
    }

    [Test]
    public void PainStatusSystemsUseLedgerHumanState()
    {
        var root = FindRepoRoot();
        var files = new[]
        {
            "Content.Shared/_CMU14/Medical/Human/Effects/SharedPainShockSystem.cs",
            "Content.Server/_CMU14/Medical/Human/Effects/CMUPainFeedbackSystem.cs",
            "Content.Shared/_CMU14/Medical/Human/Effects/SemiPermanentInjuryTriggerSystem.cs",
        };

        Assert.Multiple(() =>
        {
            foreach (var relative in files)
            {
                var text = File.ReadAllText(Path.Combine(root, relative));
                Assert.That(text, Does.Contain("HumanMedicalComponent"), relative);
                AssertDoesNotContainAny(text, LegacyClinicalAuthorityTerms);
            }
        });
    }

    [Test]
    public void ShrapnelUsesLedgerHumanState()
    {
        var root = FindRepoRoot();
        var path = Path.Combine(
            root,
            "Content.Shared/_CMU14/Medical/Human/Damage/Shrapnel/SharedCMUShrapnelSystem.cs");

        Assert.That(File.Exists(path), Is.True);

        var text = File.ReadAllText(path);

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("HumanMedicalComponent"));
            AssertDoesNotContainAny(text, LegacyClinicalAuthorityTerms);
            Assert.That(text, Does.Not.Contain("SharedCMUWoundsSystem"));
        });
    }

    [Test]
    public void SupportDiagnosticsUseLedgerHumanState()
    {
        var root = FindRepoRoot();
        var files = new[]
        {
            "Content.Server/_CMU14/Medical/Presentation/CMUMedicalVisibilitySystem.cs",
            "Content.Server/_CMU14/Medical/Human/Diagnostics/Devices/CMUStethoscopeSystem.cs",
            "Content.Server/_CMU14/Medical/Foundation/Telemetry/AutoHolocardSystem.cs",
            "Content.Client/_CMU14/Medical/Foundation/Telemetry/CMUMedicalPerfCommand.cs",
        };

        Assert.Multiple(() =>
        {
            foreach (var relative in files)
            {
                var text = File.ReadAllText(Path.Combine(root, relative));
                Assert.That(text, Does.Contain("HumanMedicalComponent"), relative);
                AssertDoesNotContainAny(text, LegacyClinicalAuthorityTerms);
            }
        });

        var stethoscope = File.ReadAllText(Path.Combine(
            root,
            "Content.Server/_CMU14/Medical/Human/Diagnostics/Devices/CMUStethoscopeSystem.cs"));
        Assert.That(stethoscope, Does.Contain("SubscribeLocalEvent<RMCStethoscopeComponent, CMUStethoscopeLedgerAttemptEvent>"));
        Assert.That(stethoscope, Does.Not.Contain("SubscribeLocalEvent<RMCStethoscopeComponent, AfterInteractEvent>"));
    }

    [Test]
    public void MutationHealingSystemsUseLedgerHumanState()
    {
        var root = FindRepoRoot();
        var files = new[]
        {
            "Content.Server/_CMU14/Medical/Human/Systems/CMUMedicalRejuvenateSystem.cs",
            "Content.Server/_CMU14/Medical/Human/Organs/Heart/HeartDefibrillatorPatchSystem.cs",
            "Content.Shared/_CMU14/Medical/Chemistry/Effects/CMURestartHeartEffect.cs",
            "Content.Server/_CMU14/Yautja/YautjaHealingGunSystem.cs",
        };

        Assert.Multiple(() =>
        {
            foreach (var relative in files)
            {
                var text = File.ReadAllText(Path.Combine(root, relative));
                Assert.That(text, Does.Contain("HumanMedicalComponent"), relative);
                AssertDoesNotContainAny(text, LegacyClinicalAuthorityTerms);
                Assert.That(text, Does.Not.Contain("SharedCMUWoundsSystem"));
            }
        });
    }

    [Test]
    public void ChemistryAndStabilizerSystemsUseLedgerHumanState()
    {
        var root = FindRepoRoot();
        var files = new[]
        {
            "Content.Shared/_CMU14/Medical/Chemistry/SharedMetabolismHubSystem.cs",
            "Content.Shared/_CMU14/Medical/Chemistry/Effects/HealOrganEffect.cs",
        };

        Assert.Multiple(() =>
        {
            foreach (var relative in files)
            {
                var text = File.ReadAllText(Path.Combine(root, relative));
                Assert.That(text, Does.Contain("HumanMedicalComponent"), relative);
                AssertDoesNotContainAny(text, LegacyClinicalAuthorityTerms);
                Assert.That(text, Does.Not.Contain("SharedOrganHealthSystem"));
            }
        });
    }

    [Test]
    public void OrganSymptomSystemsUseLedgerHumanState()
    {
        var root = FindRepoRoot();
        var files = new[]
        {
            "Content.Shared/_CMU14/Medical/Human/Organs/Brain/SharedBrainSystem.cs",
            "Content.Shared/_CMU14/Medical/Human/Organs/Ears/SharedEarsSystem.cs",
            "Content.Shared/_CMU14/Medical/Human/Organs/Eyes/SharedEyesSystem.cs",
            "Content.Shared/_CMU14/Medical/Human/Organs/Heart/SharedHeartSystem.cs",
            "Content.Shared/_CMU14/Medical/Human/Organs/Kidneys/SharedKidneysSystem.cs",
            "Content.Shared/_CMU14/Medical/Human/Organs/Liver/SharedLiverSystem.cs",
            "Content.Shared/_CMU14/Medical/Human/Organs/Lungs/SharedLungsSystem.cs",
            "Content.Shared/_CMU14/Medical/Human/Organs/Stomach/SharedStomachSystem.cs",
        };

        Assert.Multiple(() =>
        {
            foreach (var relative in files)
            {
                var text = File.ReadAllText(Path.Combine(root, relative));
                Assert.That(text, Does.Contain("HumanMedicalComponent"), relative);
                AssertDoesNotContainAny(text, LegacyClinicalAuthorityTerms);
                Assert.That(text, Does.Not.Contain("SharedOrganHealthSystem"), relative);
                Assert.That(text, Does.Not.Contain("OrganStageChangedEvent"), relative);
            }
        });
    }

    [Test]
    public void OldCmuSurgeryWorkflowDoesNotExist()
    {
        var root = FindRepoRoot();
        var files = new[]
        {
            "Content.Shared/_CMU14/Medical/Surgery/SharedCMUSurgerySystem.cs",
            "Content.Shared/_CMU14/Medical/Surgery/SharedCMUSurgeryFlowSystem.cs",
            "Content.Shared/_CMU14/Medical/Surgery/CMUSurgeryUI.cs",
            "Content.Shared/_CMU14/Medical/Surgery/CMUSurgeryArmedStepComponent.cs",
            "Content.Shared/_CMU14/Medical/Surgery/CMUSurgeryInFlightComponent.cs",
            "Content.Shared/_CMU14/Medical/Surgery/CMUSurgeryInProgressComponent.cs",
            "Content.Shared/_CMU14/Medical/Surgery/CMUSurgeryStepDoAfterEvent.cs",
            "Content.Shared/_CMU14/Medical/Surgery/CMUSurgeryStepMetadataPrototype.cs",
            "Content.Shared/_CMU14/Medical/Surgery/Conditions/CMUFracturedSurgeryConditionComponent.cs",
            "Content.Shared/_CMU14/Medical/Surgery/Conditions/CMUInternalBleedingSurgeryConditionComponent.cs",
            "Content.Shared/_CMU14/Medical/Surgery/Conditions/CMUOrganDamagedSurgeryConditionComponent.cs",
            "Content.Shared/_CMU14/Medical/Surgery/Effects/CMUSurgeryStepCauterizeBleedEffectComponent.cs",
            "Content.Shared/_CMU14/Medical/Surgery/Effects/CMUSurgeryStepRepairOrganEffectComponent.cs",
            "Content.Shared/_CMU14/Medical/Surgery/Effects/CMUSurgeryStepSetBoneEffectComponent.cs",
        };

        Assert.Multiple(() =>
        {
            foreach (var relative in files)
                Assert.That(File.Exists(Path.Combine(root, relative)), Is.False, relative);
        });
    }

    [Test]
    public void HumanSurgeryToolsUseLedgerDirectly()
    {
        var root = FindRepoRoot();
        var path = Path.Combine(
            root,
            "Content.Server/_CMU14/Medical/Human/Surgery/HumanSurgeryToolSystem.cs");

        Assert.That(File.Exists(path), Is.True);

        var text = File.ReadAllText(path);

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("HumanMedicalComponent"));
            Assert.That(text, Does.Contain("HumanSurgerySystem"));
            Assert.That(text, Does.Contain("CMSurgeryToolComponent"));
            Assert.That(text, Does.Not.Contain("SubscribeLocalEvent<CMSurgeryToolComponent, AfterInteractEvent>"));
            Assert.That(text, Does.Contain("HumanSurgeryToolDoAfterEvent"));
            AssertDoesNotContainAny(text, LegacyClinicalAuthorityTerms);
            Assert.That(text, Does.Not.Contain("SharedCMUSurgeryFlowSystem"));
            Assert.That(text, Does.Not.Contain("CMUSurgeryArmedStep"));
            Assert.That(text, Does.Not.Contain("CMUSurgeryBui"));
        });
    }

    [Test]
    public void AutodocSurgeryExecutorDoesNotExist()
    {
        var root = FindRepoRoot();
        var files = new[]
        {
            "Content.Server/_CMU14/Medical/Surgery/CMUAutodocSystem.cs",
            "Content.Client/_CMU14/Medical/Surgery/CMUAutodocBui.cs",
        };

        Assert.Multiple(() =>
        {
            foreach (var relative in files)
                Assert.That(File.Exists(Path.Combine(root, relative)), Is.False, relative);
        });
    }

    [Test]
    public void AutodocShellIsCurrentMachineGameplayOnly()
    {
        var root = FindRepoRoot();
        var records = new List<MatchRecord>();
        var roots = new[]
        {
            "Resources/Prototypes",
            "Resources/Maps",
        };

        foreach (var relativeRoot in roots)
        {
            var fullRoot = Path.Combine(root, relativeRoot);
            foreach (var path in Directory.EnumerateFiles(fullRoot, "*", SearchOption.AllDirectories))
            {
                if (!TextExtensions.Contains(Path.GetExtension(path)))
                    continue;

                var lines = File.ReadAllLines(path);
                for (var i = 0; i < lines.Length; i++)
                {
                    if (lines[i].Contains("CMUAutodoc", StringComparison.Ordinal))
                    {
                        var relative = Normalize(Path.GetRelativePath(root, path));
                        if (relative != "Resources/Prototypes/_CMU14/Medical/medical_machines.yml")
                            records.Add(new MatchRecord(relative, i + 1, "CMUAutodoc"));
                    }
                    if (lines[i].Contains("CMUYautjaStructureYautjaMachinesAutodoc", StringComparison.Ordinal))
                        records.Add(new MatchRecord(Normalize(Path.GetRelativePath(root, path)), i + 1, "CMUYautjaStructureYautjaMachinesAutodoc"));
                }
            }
        }

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(Path.Combine(
                root,
                "Content.Server/_CMU14/Medical/Machines/CMUAutodocSystem.cs")), Is.True);
            Assert.That(records, Is.Empty, FormatMatches(records));
        });
    }

    [Test]
    public void RemainingMedicalGameplayReadersUseLedgerHumanState()
    {
        var root = FindRepoRoot();
        var files = new[]
        {
            "Content.Server/_CMU14/Medical/Human/Effects/CMUAccuracyEventSubscriber.cs",
            "Content.Shared/_CMU14/Medical/Human/Damage/CMUExplosionMedicalTraumaSystem.cs",
            "Content.Server/_CMU14/Medical/Foundation/Telemetry/CMUMedicalTelemetrySystem.cs",
            "Content.Shared/_CMU14/Explosion/CMUHumanSynthExplosionVulnerabilitySystem.cs",
            "Content.Server/Body/Systems/RespiratorSystem.cs",
            "Content.Server/Body/Systems/MetabolizerSystem.cs",
            "Content.Server/_RMC14/Synth/SynthSystem.cs",
            "Content.Shared/_RMC14/Xenonids/Alchemist/XenoAlchemistSystem.cs",
        };

        Assert.Multiple(() =>
        {
            foreach (var relative in files)
            {
                var text = File.ReadAllText(Path.Combine(root, relative));
                Assert.That(text, Does.Contain("HumanMedicalComponent"), relative);
                AssertDoesNotContainAny(text, LegacyClinicalAuthorityTerms);
                Assert.That(text, Does.Not.Contain("SharedBodyPartHealthSystem"), relative);
                Assert.That(text, Does.Not.Contain("SharedCMUWoundsSystem"), relative);
                Assert.That(text, Does.Not.Contain("SharedOrganHealthSystem"), relative);
                Assert.That(text, Does.Not.Contain("OrganStageChangedEvent"), relative);
                Assert.That(text, Does.Not.Contain("FractureSeverityChangedEvent"), relative);
            }
        });
    }

    [Test]
    public void RetiredFieldAndOrthopedicLegacySystemsStayInactive()
    {
        var root = FindRepoRoot();
        var removedBandagePath = Path.Combine(
            root,
            "Content.Server/_CMU14/Medical/Wounds/CMUBandageInterceptionSystem.cs");
        var splintShellPath = Path.Combine(
            root,
            "Content.Shared/_CMU14/Medical/Equipment/SharedCMUOrthopedicEquipmentSystem.cs");

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(removedBandagePath), Is.False);

            var text = File.ReadAllText(splintShellPath);
            Assert.That(text, Does.Contain("HumanOrthopedicTreatmentSystem"));
            AssertDoesNotContainAny(text, LegacyClinicalAuthorityTerms);
            Assert.That(text, Does.Not.Contain("SharedFractureSystem"));
            Assert.That(text, Does.Not.Contain("BoneFracturedEvent"));
            Assert.That(text, Does.Not.Contain("CMUHumanMedicalComponent"));
        });
    }

    [Test]
    public void RetiredLegacyAuthorityFilesDoNotExist()
    {
        var root = FindRepoRoot();
        var removedFiles = new[]
        {
            "Content.Shared/_CMU14/Medical/CMUHumanMedicalComponent.cs",
            "Content.Shared/_CMU14/Medical/Wounds/InternalBleedingComponent.cs",
            "Content.Shared/_CMU14/Medical/Wounds/CMUInternalBleedingSuppressedComponent.cs",
            "Content.Shared/_CMU14/Medical/Wounds/BodyPartWoundComponent.cs",
            "Content.Shared/_CMU14/Medical/Wounds/SharedCMUWoundsSystem.cs",
            "Content.Client/_CMU14/Medical/Wounds/CMUWoundsSystem.cs",
            "Content.Server/_CMU14/Medical/Wounds/CMUWoundsSystem.cs",
            "Content.Shared/_CMU14/Medical/BodyPart/BodyPartHealthComponent.cs",
            "Content.Shared/_CMU14/Medical/BodyPart/SharedBodyPartHealthSystem.cs",
            "Content.Client/_CMU14/Medical/BodyPart/BodyPartHealthSystem.cs",
            "Content.Server/_CMU14/Medical/BodyPart/BodyPartHealthSystem.cs",
            "Content.Shared/_CMU14/Medical/Bones/FractureComponent.cs",
            "Content.Shared/_CMU14/Medical/Bones/BoneComponent.cs",
            "Content.Shared/_CMU14/Medical/Bones/SharedFractureSystem.cs",
            "Content.Shared/_CMU14/Medical/Bones/SharedBoneSystem.cs",
            "Content.Client/_CMU14/Medical/Bones/FractureSystem.cs",
            "Content.Server/_CMU14/Medical/Bones/FractureSystem.cs",
            "Content.Client/_CMU14/Medical/Bones/BoneSystem.cs",
            "Content.Server/_CMU14/Medical/Bones/BoneSystem.cs",
            "Content.Shared/_CMU14/Medical/Organs/OrganHealthComponent.cs",
            "Content.Shared/_CMU14/Medical/Organs/OrganStasisComponent.cs",
            "Content.Shared/_CMU14/Medical/Organs/SharedOrganHealthSystem.cs",
            "Content.Client/_CMU14/Medical/Organs/OrganHealthSystem.cs",
            "Content.Server/_CMU14/Medical/Organs/OrganHealthSystem.cs",
            "Content.Shared/_CMU14/Medical/Organs/Events/OrganStageChangedEvent.cs",
            "Content.Shared/_CMU14/Medical/Organs/Events/OrganDamagedEvent.cs",
            "Content.Shared/_CMU14/Medical/Bones/Events/FractureSeverityChangedEvent.cs",
            "Content.Shared/_CMU14/Medical/Bones/Events/BoneFracturedEvent.cs",
            "Content.Shared/_CMU14/Medical/Bones/Events/BoneFractureAttemptEvent.cs",
            "Content.Shared/_CMU14/Medical/BodyPart/Events/BodyPartDamagedEvent.cs",
            "Content.Shared/_CMU14/Medical/BodyPart/Events/BodyPartHealedEvent.cs",
            "Content.Shared/_CMU14/Medical/BodyPart/Events/BodyPartPainThresholdCrossedEvent.cs",
            "Content.Shared/_CMU14/Medical/Wounds/Events/BodyPartWoundAppliedEvent.cs",
        };

        Assert.Multiple(() =>
        {
            foreach (var relative in removedFiles)
                Assert.That(File.Exists(Path.Combine(root, relative)), Is.False, relative);
        });
    }

    private static void AssertNoMatches(string[] terms, bool includeDocs)
    {
        var matches = FindMatches(terms, includeDocs);

        Assert.That(matches, Is.Empty, FormatMatches(matches));
    }

    private static void AssertDoesNotContainAny(string text, IReadOnlyCollection<string> terms)
    {
        Assert.Multiple(() =>
        {
            foreach (var term in terms)
                Assert.That(text, Does.Not.Contain(term));
        });
    }

    private static List<MatchRecord> FindMatches(string[] terms, bool includeDocs)
    {
        var root = FindRepoRoot();
        var records = new List<MatchRecord>();

        foreach (var sourceRoot in SourceRoots)
        {
            if (!includeDocs && sourceRoot == "Docs")
                continue;

            var fullRoot = Path.Combine(root, sourceRoot);
            if (!Directory.Exists(fullRoot))
                continue;

            foreach (var path in Directory.EnumerateFiles(fullRoot, "*", SearchOption.AllDirectories))
            {
                if (!TextExtensions.Contains(Path.GetExtension(path)))
                    continue;

                var relative = Normalize(Path.GetRelativePath(root, path));
                if (relative.StartsWith(Normalize("RobustToolbox/"), StringComparison.Ordinal))
                    continue;
                if (relative == GuardTestFile)
                    continue;

                var lines = File.ReadAllLines(path);
                for (var i = 0; i < lines.Length; i++)
                {
                    foreach (var term in terms)
                    {
                        if (lines[i].Contains(term, StringComparison.Ordinal) &&
                            !lines[i].Contains(term + ">", StringComparison.Ordinal))
                        {
                            records.Add(new MatchRecord(relative, i + 1, term));
                        }
                    }
                }
            }
        }

        return records;
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

    private static string FormatMatches(IReadOnlyList<MatchRecord> matches)
    {
        if (matches.Count == 0)
            return string.Empty;

        return string.Join(Environment.NewLine, matches.Select(match =>
            $"{match.Path}:{match.Line}: {match.Term}"));
    }

    private static string Normalize(string path)
    {
        return path.Replace('\\', '/');
    }

    private readonly record struct MatchRecord(string Path, int Line, string Term);
}
