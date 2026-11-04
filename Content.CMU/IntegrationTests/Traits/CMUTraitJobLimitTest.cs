using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Roles;

namespace Content.IntegrationTests.CMU14.Traits;

/// <summary>
///     A trait with allowed jobs (like Mute) is forbidden by every other job's requirements.
/// </summary>
[TestFixture]
public sealed class CMUTraitJobLimitTest : GameTest
{
    [TestCase("AU14JobCivilianColonist", false)]
    [TestCase("AU14JobGOVFORSquadRifleman", true)]
    [TestCase("AU14JobCivilianCorporateLiaison", true)]
    public async Task MuteIsOnlyForColonists(string job, bool forbidden)
    {
        await Server.WaitAssertion(() =>
        {
            var requirements = Server.System<SharedRoleSystem>().GetRoleRequirements(SProtoMan.Index<JobPrototype>(job));
            var forbids = requirements != null && requirements.OfType<TraitsRequirement>()
                .Any(r => r.Inverted && r.Traits.Contains("CMUMute"));
            Assert.That(forbids, Is.EqualTo(forbidden));
        });
    }
}
