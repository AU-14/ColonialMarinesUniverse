using System.Linq;
using Content.Shared._RMC14.Medal;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Roles;

/// <summary>
///     Corporate office and science staff: playtime awards are name badges, and they get the same
///     wardrobe as the role they work alongside.
/// </summary>
[TestFixture]
public sealed class CMUCorporateJobsTest
{
    private static readonly string[] OfficeJobs =
    {
        "CMUJobCorporateExecutive",
        "AU14JobCivilianCorporateAssistant",
        "CMUJobCorporateLegalSpecialist",
        "CMUJobCorporateLegalSupervisor",
        "CMUJobCorporateDivisionManager",
        "CMUJobCorporateChiefExecutive",
    };

    [TestCase("AU14JobCivilianCorporateLiaison")]
    [TestCase("CMUJobCorporateExecutive")]
    [TestCase("AU14JobCivilianCorporateAssistant")]
    [TestCase("CMUJobCorporateLegalSpecialist")]
    [TestCase("CMUJobCorporateLegalSupervisor")]
    [TestCase("CMUJobCorporateDivisionManager")]
    [TestCase("CMUJobCorporateChiefExecutive")]
    [TestCase("AU14JobCivilianScientist")]
    [TestCase("CMUJobCorporateLeadResearcher")]
    public async Task PlaytimeAwardsAreNameBadges(string jobId)
    {
        await using var pair = await PoolManager.GetServerClient();
        var protos = pair.Server.ProtoMan;
        await pair.Server.WaitAssertion(() =>
        {
            var medals = protos.Index<JobPrototype>(jobId).Medals;
            Assert.That(medals, Is.Not.Null);
            foreach (var tier in new[] { RMCPlaytimeMedalType.Bronze, RMCPlaytimeMedalType.Silver, RMCPlaytimeMedalType.Gold, RMCPlaytimeMedalType.Platinum })
            {
                Assert.That(medals!.TryGetValue(tier, out var award), Is.True, $"{jobId} has no {tier} award.");
                Assert.That(award.Id, Does.Contain("Nametag"), $"{jobId}'s {tier} award is a medal, not a name badge.");
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CorporateStaffShareTheirWardrobe()
    {
        await using var pair = await PoolManager.GetServerClient();
        var protos = pair.Server.ProtoMan;
        await pair.Server.WaitAssertion(() =>
        {
            List<string> Groups(string job) => protos.Index<RoleLoadoutPrototype>($"Job{job}").Groups.Select(g => g.Id).ToList();

            Assert.That(Groups("CMUJobCorporateLeadResearcher"), Is.SupersetOf(Groups("AU14JobCivilianScientist")),
                "The research supervisor doesn't have the scientist's wardrobe.");

            var liaison = Groups("AU14JobCivilianCorporateLiaison");
            foreach (var job in OfficeJobs)
                Assert.That(Groups(job), Is.SupersetOf(liaison), $"{job} doesn't have the liaison's wardrobe.");
        });
        await pair.CleanReturnAsync();
    }
}
