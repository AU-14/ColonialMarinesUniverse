using Content.Shared.Ghost;
using NUnit.Framework;

namespace Content.Tests.Shared.Ghost;

[TestFixture]
public sealed class GhostWarpGroupingTest
{
    [Test]
    public void OpforSquadSergeantUsesOpforSquadLeads()
    {
        var grouping = GhostWarpGrouping.Classify(
            isWarpPoint: false,
            jobId: "AU14JobOPFORSquadSergeant",
            departmentId: "AU14DepartmentOpfor",
            factions: new[] { "OPFOR" },
            isXeno: false,
            isYautja: false,
            isCorruptedHive: false,
            xenoTier: null,
            realDisplayWeight: 5);

        Assert.Multiple(() =>
        {
            Assert.That(grouping.Tab, Is.EqualTo(GhostWarpGrouping.TabOpfor));
            Assert.That(grouping.Section, Is.EqualTo(GhostWarpGrouping.SectionSquadLeads));
        });
    }

    [Test]
    public void MilitaryRolesUseRoleSpecificSubtabs()
    {
        AssertMilitarySection("AU14JobGOVFORDSPilot", GhostWarpGrouping.SectionPilotsCrew);
        AssertMilitarySection("AU14JobGOVFORPlatCo", GhostWarpGrouping.SectionCommand);
        AssertMilitarySection("AU14JobGOVFORSectionSergeant", GhostWarpGrouping.SectionSquadLeads);
        AssertMilitarySection("AU14JobGOVFORPlatoonCorpsman", GhostWarpGrouping.SectionSpecialists);
        AssertMilitarySection("AU14JobGOVFORSquadRifleman", GhostWarpGrouping.SectionLine);
    }

    [Test]
    public void UnmcMilitaryRolesUseRoleSpecificSubtabs()
    {
        AssertMilitarySection("CMJobPilotDropship", GhostWarpGrouping.SectionPilotsCrew, "UNMC", "CMEngineering");
        AssertMilitarySection("CMJobCommandingOfficer", GhostWarpGrouping.SectionCommand, "UNMC", "CMCommand");
        AssertMilitarySection("CMJobSquadLeader", GhostWarpGrouping.SectionSquadLeads, "UNMC", "CMSquad");
        AssertMilitarySection("CMJobHospitalCorpsman", GhostWarpGrouping.SectionSpecialists, "UNMC", "CMMedbay");
        AssertMilitarySection("CMJobRifleman", GhostWarpGrouping.SectionLine, "UNMC", "CMSquad");
    }

    [Test]
    public void YautjaComponentUsesYautjaHunters()
    {
        var grouping = GhostWarpGrouping.Classify(
            isWarpPoint: false,
            jobId: null,
            departmentId: null,
            factions: new[] { "CMUYautja" },
            isXeno: false,
            isYautja: true,
            isCorruptedHive: false,
            xenoTier: null,
            realDisplayWeight: 0);

        Assert.Multiple(() =>
        {
            Assert.That(grouping.Tab, Is.EqualTo(GhostWarpGrouping.TabYautja));
            Assert.That(grouping.Section, Is.EqualTo(GhostWarpGrouping.SectionHunters));
        });
    }

    [Test]
    public void ThirdPartyJobsUseThirdPartyRoleSubtabs()
    {
        var leader = GhostWarpGrouping.Classify(
            isWarpPoint: false,
            jobId: "AU14JobThirdPartyLeader",
            departmentId: "AU14DepartmentThirdParty",
            factions: null,
            isXeno: false,
            isYautja: false,
            isCorruptedHive: false,
            xenoTier: null,
            realDisplayWeight: 10);

        var member = GhostWarpGrouping.Classify(
            isWarpPoint: false,
            jobId: "AU14JobThirdPartyMember",
            departmentId: "AU14DepartmentThirdParty",
            factions: null,
            isXeno: false,
            isYautja: false,
            isCorruptedHive: false,
            xenoTier: null,
            realDisplayWeight: 1);

        Assert.Multiple(() =>
        {
            Assert.That(leader.Tab, Is.EqualTo(GhostWarpGrouping.TabThirdParty));
            Assert.That(leader.Section, Is.EqualTo(GhostWarpGrouping.SectionLeaders));
            Assert.That(member.Tab, Is.EqualTo(GhostWarpGrouping.TabThirdParty));
            Assert.That(member.Section, Is.EqualTo(GhostWarpGrouping.SectionMembers));
        });
    }

    [Test]
    public void CorruptedHiveXenoUsesCorruptedHiveTier()
    {
        var grouping = GhostWarpGrouping.Classify(
            isWarpPoint: false,
            jobId: "CMXenoWarrior",
            departmentId: null,
            factions: null,
            isXeno: true,
            isYautja: false,
            isCorruptedHive: true,
            xenoTier: 2,
            realDisplayWeight: 0);

        Assert.Multiple(() =>
        {
            Assert.That(grouping.Tab, Is.EqualTo(GhostWarpGrouping.TabCorruptedHive));
            Assert.That(grouping.Section, Is.EqualTo("Tier 2"));
        });
    }

    [Test]
    public void WarpPointsUseLocationsWarpPoints()
    {
        var grouping = GhostWarpGrouping.Classify(
            isWarpPoint: true,
            jobId: null,
            departmentId: null,
            factions: null,
            isXeno: false,
            isYautja: false,
            isCorruptedHive: false,
            xenoTier: null,
            realDisplayWeight: 0);

        Assert.Multiple(() =>
        {
            Assert.That(grouping.Tab, Is.EqualTo(GhostWarpGrouping.TabLocations));
            Assert.That(grouping.Section, Is.EqualTo(GhostWarpGrouping.SectionWarpPoints));
        });
    }

    private static void AssertMilitarySection(
        string jobId,
        string expectedSection,
        string faction = "GOVFOR",
        string departmentId = "AU14DepartmentGovernmentForces")
    {
        var grouping = GhostWarpGrouping.Classify(
            isWarpPoint: false,
            jobId: jobId,
            departmentId: departmentId,
            factions: new[] { faction },
            isXeno: false,
            isYautja: false,
            isCorruptedHive: false,
            xenoTier: null,
            realDisplayWeight: 0);

        Assert.Multiple(() =>
        {
            Assert.That(grouping.Tab, Is.EqualTo(GhostWarpGrouping.TabMilitary));
            Assert.That(grouping.Section, Is.EqualTo(expectedSection));
        });
    }
}
