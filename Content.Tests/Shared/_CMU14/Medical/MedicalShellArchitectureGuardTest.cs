using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace Content.Tests.Shared._CMU14.Medical;

[TestFixture]
public sealed class MedicalShellArchitectureGuardTest
{
    private static readonly string[] CurrentTopLevelModules =
    {
        "Chemistry",
        "Equipment",
        "Foundation",
        "Human",
        "Machines",
        "Presentation",
        "Synthetic",
        "Targeting",
    };

    private static readonly string[] TemporaryOffenderFolders =
    {
        "Metabolism",
        "Organs",
        "Penalties",
        "Shrapnel",
        "Stabilizers",
        "StatusEffects",
        "Surgery",
        "TemporaryBlurryVision",
    };

    [Test]
    public void MedicalShellUsesCurrentTopLevelModules()
    {
        var root = FindRepoRoot();
        var roots = new[]
        {
            Path.Combine(root, "Content.Shared", "_CMU14", "Medical"),
            Path.Combine(root, "Content.Server", "_CMU14", "Medical"),
            Path.Combine(root, "Content.Client", "_CMU14", "Medical"),
        };

        Assert.Multiple(() =>
        {
            foreach (var sourceRoot in roots)
            {
                foreach (var directory in Directory.GetDirectories(sourceRoot))
                {
                    var name = Path.GetFileName(directory);
                    Assert.That(CurrentTopLevelModules, Does.Contain(name), directory);
                }
            }
        });
    }

    [Test]
    public void RetiredShellFoldersStayEmptyOrMissing()
    {
        var root = FindRepoRoot();
        var contentRoots = new[]
        {
            Path.Combine(root, "Content.Shared", "_CMU14", "Medical"),
            Path.Combine(root, "Content.Server", "_CMU14", "Medical"),
            Path.Combine(root, "Content.Client", "_CMU14", "Medical"),
        };

        Assert.Multiple(() =>
        {
            foreach (var sourceRoot in contentRoots)
            {
                foreach (var folder in TemporaryOffenderFolders)
                {
                    var path = Path.Combine(sourceRoot, folder);
                    if (!Directory.Exists(path))
                        continue;

                    Assert.That(Directory.GetFiles(path, "*", SearchOption.AllDirectories), Is.Empty, path);
                }
            }
        });
    }

    [Test]
    public void PrunedFieldTreatmentsModuleHasNoSourceFiles()
    {
        var root = FindRepoRoot();
        var roots = new[]
        {
            Path.Combine(root, "Content.Shared", "_CMU14", "Medical", "FieldTreatments"),
            Path.Combine(root, "Content.Server", "_CMU14", "Medical", "FieldTreatments"),
            Path.Combine(root, "Content.Client", "_CMU14", "Medical", "FieldTreatments"),
        };

        foreach (var sourceRoot in roots)
        {
            if (!Directory.Exists(sourceRoot))
                continue;

            Assert.That(
                Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories),
                Is.Empty,
                sourceRoot);
        }
    }

    [Test]
    public void PrunedItemsModuleHasNoSourceFilesOrAppliedState()
    {
        var root = FindRepoRoot();
        var roots = new[]
        {
            Path.Combine(root, "Content.Shared", "_CMU14", "Medical", "Items"),
            Path.Combine(root, "Content.Server", "_CMU14", "Medical", "Items"),
            Path.Combine(root, "Content.Client", "_CMU14", "Medical", "Items"),
        };

        foreach (var sourceRoot in roots)
        {
            if (!Directory.Exists(sourceRoot))
                continue;

            Assert.That(
                Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories),
                Is.Empty,
                sourceRoot);
        }

        AssertNoProductionMatches(
            root,
            "CMUSplintedComponent",
            "CMUCastComponent",
            "CMUSplintChangedEvent",
            "CMUCastChangedEvent",
            "id: CMUSplinted",
            "name: In Cast",
            "description: A fractured limb is set in a cast.");
    }

    [Test]
    public void PrunedBonesModuleHasNoSourceFiles()
    {
        var root = FindRepoRoot();
        var roots = new[]
        {
            Path.Combine(root, "Content.Shared", "_CMU14", "Medical", "Bones"),
            Path.Combine(root, "Content.Server", "_CMU14", "Medical", "Bones"),
            Path.Combine(root, "Content.Client", "_CMU14", "Medical", "Bones"),
        };

        foreach (var sourceRoot in roots)
        {
            if (!Directory.Exists(sourceRoot))
                continue;

            Assert.That(
                Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories),
                Is.Empty,
                sourceRoot);
        }
    }

    [Test]
    public void PrunedWoundsModuleHasNoSourceFilesOrPendingBandageState()
    {
        var root = FindRepoRoot();
        var roots = new[]
        {
            Path.Combine(root, "Content.Shared", "_CMU14", "Medical", "Wounds"),
            Path.Combine(root, "Content.Server", "_CMU14", "Medical", "Wounds"),
            Path.Combine(root, "Content.Client", "_CMU14", "Medical", "Wounds"),
        };

        foreach (var sourceRoot in roots)
        {
            if (!Directory.Exists(sourceRoot))
                continue;

            Assert.That(
                Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories),
                Is.Empty,
                sourceRoot);
        }

        AssertNoProductionMatches(
            root,
            "CMUBandagePendingComponent",
            "CMUBandageDoAfterEvent",
            "WoundTreatedEvent",
            "CMUTourniquetComponent");
    }

    [Test]
    public void PrunedBodyPartModuleHasNoSourceFiles()
    {
        var root = FindRepoRoot();
        var roots = new[]
        {
            Path.Combine(root, "Content.Shared", "_CMU14", "Medical", "BodyPart"),
            Path.Combine(root, "Content.Server", "_CMU14", "Medical", "BodyPart"),
            Path.Combine(root, "Content.Client", "_CMU14", "Medical", "BodyPart"),
        };

        foreach (var sourceRoot in roots)
        {
            if (!Directory.Exists(sourceRoot))
                continue;

            Assert.That(
                Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories),
                Is.Empty,
                sourceRoot);
        }
    }

    [Test]
    public void PrunedStatusProjectionModulesHaveNoSourceFiles()
    {
        var root = FindRepoRoot();
        var roots = new[]
        {
            Path.Combine(root, "Content.Shared", "_CMU14", "Medical", "Penalties"),
            Path.Combine(root, "Content.Server", "_CMU14", "Medical", "Penalties"),
            Path.Combine(root, "Content.Client", "_CMU14", "Medical", "Penalties"),
            Path.Combine(root, "Content.Shared", "_CMU14", "Medical", "StatusEffects"),
            Path.Combine(root, "Content.Server", "_CMU14", "Medical", "StatusEffects"),
            Path.Combine(root, "Content.Client", "_CMU14", "Medical", "StatusEffects"),
        };

        foreach (var sourceRoot in roots)
        {
            if (!Directory.Exists(sourceRoot))
                continue;

            Assert.That(
                Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories),
                Is.Empty,
                sourceRoot);
        }
    }

    [Test]
    public void PrunedShrapnelModuleHasNoSourceFiles()
    {
        var root = FindRepoRoot();
        var roots = new[]
        {
            Path.Combine(root, "Content.Shared", "_CMU14", "Medical", "Shrapnel"),
            Path.Combine(root, "Content.Server", "_CMU14", "Medical", "Shrapnel"),
            Path.Combine(root, "Content.Client", "_CMU14", "Medical", "Shrapnel"),
        };

        foreach (var sourceRoot in roots)
        {
            if (!Directory.Exists(sourceRoot))
                continue;

            Assert.That(
                Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories),
                Is.Empty,
                sourceRoot);
        }
    }

    [Test]
    public void PrunedCosmeticModuleHasNoSourceFiles()
    {
        var root = FindRepoRoot();
        var roots = new[]
        {
            Path.Combine(root, "Content.Shared", "_CMU14", "Medical", "Cosmetic"),
            Path.Combine(root, "Content.Server", "_CMU14", "Medical", "Cosmetic"),
            Path.Combine(root, "Content.Client", "_CMU14", "Medical", "Cosmetic"),
        };

        foreach (var sourceRoot in roots)
        {
            if (!Directory.Exists(sourceRoot))
                continue;

            Assert.That(
                Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories),
                Is.Empty,
                sourceRoot);
        }
    }

    [Test]
    public void PrunedTemporaryBlurryVisionModuleHasNoSourceFiles()
    {
        var root = FindRepoRoot();
        var roots = new[]
        {
            Path.Combine(root, "Content.Shared", "_CMU14", "Medical", "TemporaryBlurryVision"),
            Path.Combine(root, "Content.Server", "_CMU14", "Medical", "TemporaryBlurryVision"),
            Path.Combine(root, "Content.Client", "_CMU14", "Medical", "TemporaryBlurryVision"),
        };

        foreach (var sourceRoot in roots)
        {
            if (!Directory.Exists(sourceRoot))
                continue;

            Assert.That(
                Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories),
                Is.Empty,
                sourceRoot);
        }
    }

    [Test]
    public void PrunedChemicalShellModulesHaveNoFiles()
    {
        var root = FindRepoRoot();
        var roots = new[]
        {
            Path.Combine(root, "Content.Shared", "_CMU14", "Medical", "Metabolism"),
            Path.Combine(root, "Content.Server", "_CMU14", "Medical", "Metabolism"),
            Path.Combine(root, "Content.Client", "_CMU14", "Medical", "Metabolism"),
            Path.Combine(root, "Content.Shared", "_CMU14", "Medical", "Stabilizers"),
            Path.Combine(root, "Content.Server", "_CMU14", "Medical", "Stabilizers"),
            Path.Combine(root, "Content.Client", "_CMU14", "Medical", "Stabilizers"),
        };

        foreach (var sourceRoot in roots)
        {
            if (!Directory.Exists(sourceRoot))
                continue;

            Assert.That(
                Directory.GetFiles(sourceRoot, "*", SearchOption.AllDirectories),
                Is.Empty,
                sourceRoot);
        }
    }

    [Test]
    public void PrunedOrganShellModuleHasNoSourceFiles()
    {
        var root = FindRepoRoot();
        var roots = new[]
        {
            Path.Combine(root, "Content.Shared", "_CMU14", "Medical", "Organs"),
            Path.Combine(root, "Content.Server", "_CMU14", "Medical", "Organs"),
            Path.Combine(root, "Content.Client", "_CMU14", "Medical", "Organs"),
        };

        foreach (var sourceRoot in roots)
        {
            if (!Directory.Exists(sourceRoot))
                continue;

            Assert.That(
                Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories),
                Is.Empty,
                sourceRoot);
        }
    }

    [Test]
    public void PrunedEntityEffectsModuleHasNoSourceFiles()
    {
        var root = FindRepoRoot();
        var roots = new[]
        {
            Path.Combine(root, "Content.Shared", "_CMU14", "Medical", "EntityEffects"),
            Path.Combine(root, "Content.Server", "_CMU14", "Medical", "EntityEffects"),
            Path.Combine(root, "Content.Client", "_CMU14", "Medical", "EntityEffects"),
        };

        foreach (var sourceRoot in roots)
        {
            if (!Directory.Exists(sourceRoot))
                continue;

            Assert.That(
                Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories),
                Is.Empty,
                sourceRoot);
        }
    }

    [Test]
    public void MedicalRootHasNoLooseSourceFiles()
    {
        var root = FindRepoRoot();
        var roots = new[]
        {
            Path.Combine(root, "Content.Shared", "_CMU14", "Medical"),
            Path.Combine(root, "Content.Server", "_CMU14", "Medical"),
            Path.Combine(root, "Content.Client", "_CMU14", "Medical"),
        };

        foreach (var sourceRoot in roots)
        {
            Assert.That(
                Directory.GetFiles(sourceRoot, "*.cs", SearchOption.TopDirectoryOnly),
                Is.Empty,
                sourceRoot);
        }
    }

    [Test]
    public void PrunedPresentationShellModulesHaveNoFiles()
    {
        var root = FindRepoRoot();
        var roots = new[]
        {
            Path.Combine(root, "Content.Server", "_CMU14", "Medical", "HUD"),
            Path.Combine(root, "Content.Client", "_CMU14", "Medical", "HUD"),
            Path.Combine(root, "Content.Client", "_CMU14", "Medical", "UI"),
        };

        foreach (var sourceRoot in roots)
        {
            if (!Directory.Exists(sourceRoot))
                continue;

            Assert.That(
                Directory.GetFiles(sourceRoot, "*", SearchOption.AllDirectories),
                Is.Empty,
                sourceRoot);
        }
    }

    private static void AssertNoProductionMatches(string root, params string[] terms)
    {
        var searchRoots = new[]
        {
            "Content.Shared",
            "Content.Server",
            "Content.Client",
            "Resources/Prototypes",
        };

        var matches = new List<string>();
        foreach (var searchRoot in searchRoots)
        {
            var absolute = Path.Combine(root, searchRoot);
            if (!Directory.Exists(absolute))
                continue;

            foreach (var file in Directory.EnumerateFiles(absolute, "*.*", SearchOption.AllDirectories))
            {
                if (!file.EndsWith(".cs", StringComparison.Ordinal) &&
                    !file.EndsWith(".yml", StringComparison.Ordinal))
                {
                    continue;
                }

                var text = File.ReadAllText(file);
                foreach (var term in terms)
                {
                    if (text.Contains(term, StringComparison.Ordinal))
                        matches.Add($"{Path.GetRelativePath(root, file)}: {term}");
                }
            }
        }

        Assert.That(matches, Is.Empty);
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
