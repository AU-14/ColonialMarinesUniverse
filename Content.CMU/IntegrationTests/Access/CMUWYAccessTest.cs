using System.Collections.Generic;
using System.Linq;
using Content.Shared.Access;
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
}
