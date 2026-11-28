using System.Collections.Generic;
using System.Linq;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Access;

/// <summary>
///     Weyland-Yutani access tiers: each line of work gets its own, and only leaders get the armory.
/// </summary>
[TestFixture]
public sealed class CMUWYAccessTest
{
    private const string General = "AU14AccessCorporateWEYU";
    private const string Executive = "AU14AccessCorporateWEYULiaison";
    private const string Security = "CMUAccessWYSecurity";
    private const string Research = "CMUAccessWYResearch";
    private const string Armory = "CMUAccessWYArmory";

    private static readonly string[] Tiers = { General, Executive, Security, Research, Armory };

    // Job, then the WY tiers it should hold. Anything not listed it must not hold.
    private static readonly object[] Cases =
    {
        new object[] { "CMUJobCorporateExecutive", new[] { General } },
        new object[] { "AU14JobCivilianCorporateLiaison", new[] { General, Executive, Security, Research, Armory } },
        new object[] { "CMUJobCorporateDivisionManager", new[] { General, Executive, Security, Research, Armory } },
        new object[] { "CMUJobCorporateLegalSpecialist", new[] { General, Executive, Security, Research } },
        new object[] { "CMUJobCorporateLeadResearcher", new[] { General, Research } },
        new object[] { "AU14JobCivilianScientist", new[] { General, Research } },
        new object[] { "AU14JobWYGuard", new[] { General, Security } },
        new object[] { "CMUJobWYSecuritySergeant", new[] { General, Security, Armory } },
        new object[] { "CMUJobWYPersonalProtection", new[] { General, Security } },
        new object[] { "CMUJobWYPersonalProtectionCoordinator", new[] { General, Security, Armory } },
        new object[] { "CMUJobWYDirectorateProtectionOfficer", new[] { General, Executive, Security, Research, Armory } },
        new object[] { "AU14JobWYPMCPartyContractor", new[] { General, Security } },
        new object[] { "AU14JobWYPMCPartyLeader", new[] { General, Security, Armory } },
        new object[] { "CMUJobWYCommercialPilot", new[] { General, Security } },
        new object[] { "AU14JobPARTPilot", new string[0] },
        new object[] { "AU14JobProdigyPartyLeader", new string[0] },
    };

    [TestCaseSource(nameof(Cases))]
    public async Task JobsHoldTheirTier(string job, string[] expected)
    {
        await using var pair = await PoolManager.GetServerClient();
        var prototypes = pair.Server.ProtoMan;

        var access = new HashSet<string>(prototypes.Index<JobPrototype>(job).Access.Select(a => a.Id));
        foreach (var group in prototypes.Index<JobPrototype>(job).AccessGroups)
            access.UnionWith(prototypes.Index(group).Tags.Select(t => t.Id));

        Assert.Multiple(() =>
        {
            foreach (var tier in Tiers)
                Assert.That(access.Contains(tier), Is.EqualTo(expected.Contains(tier)), $"{job} {(expected.Contains(tier) ? "lacks" : "has")} {tier}.");
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TieredDoorsUseRealAccess()
    {
        await using var pair = await PoolManager.GetServerClient();
        var prototypes = pair.Server.ProtoMan;
        var doors = prototypes.EnumeratePrototypes<EntityPrototype>().Where(p => p.ID.StartsWith("CMUWY") && p.ID.Contains("Airlock") || p.ID.StartsWith("CMUWY") && p.ID.Contains("DoubleDoor")).ToList();
        Assert.That(doors, Has.Count.GreaterThan(100));
        foreach (var tier in new[] { "Executive", "Security", "Research", "Armory" })
            Assert.That(doors.Any(d => d.ID.StartsWith("CMUWY" + tier)), $"No {tier} doors.");
        await pair.CleanReturnAsync();
    }

    // Admins in aghost open every WY door, whatever its tier.
    [Test]
    public async Task AdminGhostOpensEveryWYDoor()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var doors = server.ProtoMan.EnumeratePrototypes<EntityPrototype>()
            .Where(p => !p.Abstract && p.ID.StartsWith("CMUWY") && p.Components.ContainsKey("AccessReader"))
            .Select(p => p.ID)
            .ToList();
        Assert.That(doors, Is.Not.Empty);

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var reader = server.System<AccessReaderSystem>();
            var ghost = entities.SpawnEntity("AdminObserver", map.GridCoords);
            Assert.Multiple(() =>
            {
                foreach (var id in doors)
                {
                    var door = entities.SpawnEntity(id, map.GridCoords);
                    Assert.That(reader.IsAllowed(ghost, door), $"Admin ghost is refused by {id}.");
                    entities.DeleteEntity(door);
                }
            });
            entities.DeleteEntity(ghost);
        });
        await pair.CleanReturnAsync();
    }

    private const string WYOverwatch = "RMCOverwatchConsoleWeYa";

    // Every WY employee but the office worker can run the corporate Overwatch console.
    [TestCase("CMUJobCorporateExecutive", false)]
    [TestCase("AU14JobCivilianCorporateLiaison", true)]
    [TestCase("AU14JobCivilianScientist", true)]
    [TestCase("CMUJobCorporateLeadResearcher", true)]
    [TestCase("CMUJobCorporateLegalSpecialist", true)]
    [TestCase("AU14JobWYGuard", true)]
    [TestCase("CMUJobWYSecuritySergeant", true)]
    [TestCase("CMUJobWYPersonalProtection", true)]
    [TestCase("CMUJobWYBioTechnician", true)]
    [TestCase("AU14JobPARTPilot", false)]
    public async Task WYOverwatchOpensForWYStaffButNotOfficeWorkers(string job, bool allowed)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var prototypes = server.ProtoMan;

        var access = new HashSet<ProtoId<AccessLevelPrototype>>(prototypes.Index<JobPrototype>(job).Access);
        foreach (var group in prototypes.Index<JobPrototype>(job).AccessGroups)
            access.UnionWith(prototypes.Index(group).Tags);

        Assert.That(prototypes.Index(WYOverwatch).TryGetComponent<AccessReaderComponent>(out var reader, server.ResolveDependency<IComponentFactory>()));
        Assert.That(server.System<AccessReaderSystem>().AreAccessTagsAllowed(access, reader!), Is.EqualTo(allowed));
        await pair.CleanReturnAsync();
    }

    // Admin ghosts get in as well.
    [Test]
    public async Task WYOverwatchAdmitsAdminGhosts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var console = entities.SpawnEntity(WYOverwatch, map.GridCoords);
            var ghost = entities.SpawnEntity("AdminObserver", map.GridCoords);
            Assert.That(server.System<AccessReaderSystem>().IsAllowed(ghost, console));
            entities.DeleteEntity(console);
            entities.DeleteEntity(ghost);
        });
        await pair.CleanReturnAsync();
    }

    // The console still needs Overwatch training, so the WY roles it opens for carry it.
    [TestCase("AU14JobCivilianScientist")]
    [TestCase("CMUJobCorporateLeadResearcher")]
    [TestCase("CMUJobCorporateLegalSpecialist")]
    [TestCase("CMUJobCorporateLegalSupervisor")]
    [TestCase("AU14JobWYGuard")]
    [TestCase("CMUJobWYSecuritySergeant")]
    [TestCase("CMUJobWYPersonalProtection")]
    [TestCase("CMUJobWYRiotGrenadier")]
    [TestCase("CMUJobWYBioTechnician")]
    [TestCase("CMUJobWYIAAuditor")]
    [TestCase("CMUJobWYRecoveryOperator")]
    [TestCase("CMUJobWYDirectorateProtectionOfficer")]
    [TestCase("CMUJobWeYuGoon")]
    [TestCase("AU14JobWYPMCPartyContractor")]
    [TestCase("AU14JobWYPMCPartyEngineer")]
    public async Task WYStaffHaveOverwatchTraining(string job)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var factory = server.ResolveDependency<IComponentFactory>();
        var proto = server.ProtoMan.Index<JobPrototype>(job);

        Assert.That(proto.RoundComponents.TryGetValue("Skills", out var entry), $"{job} has no skills.");
        var comp = (SkillsComponent) entry!.Component;
        var skills = comp.Skills;
        if (comp.Preset is { } preset)
        {
            Assert.That(server.ProtoMan.Index(preset.Id).TryGetComponent<SkillPresetComponent>(out var presetComp, factory));
            skills = presetComp!.Skills;
        }

        Assert.That(skills.GetValueOrDefault("RMCSkillOverwatch"), Is.GreaterThanOrEqualTo(1), $"{job} lacks Overwatch training.");
        await pair.CleanReturnAsync();
    }

    // Admins can hand out every WY tier with the CMU admin access configurator.
    [Test]
    public async Task AdminConfiguratorGrantsEveryWYTier()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        Assert.That(server.ProtoMan.Index("CMUAdminAccessConfigurator")
            .TryGetComponent<AccessOverriderComponent>(out var overrider, server.ResolveDependency<IComponentFactory>()));
        var levels = overrider!.AccessLevels.Select(l => l.Id).ToHashSet();
        foreach (var tier in Tiers)
            Assert.That(levels, Does.Contain(tier));
        await pair.CleanReturnAsync();
    }
}
