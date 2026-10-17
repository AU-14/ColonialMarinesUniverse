using Content.Server.CMU14.Marines.Roles.Ranks;
using Content.Shared._RMC14.Marines;
using Content.Shared.CMU14.Marines.Roles.Ranks;

namespace Content.IntegrationTests.CMU14.Marines;

[TestFixture]
public sealed class CMUDeputyChevronTest
{
    [Test]
    public async Task GhostDeputyChevronRestoresIconAfterReapplying()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var deputy = entities.SpawnEntity("CMBInvestigationPartyJobDeputy", map.GridCoords);
            var chevron = entities.SpawnEntity("AU14ChevronCMBDeputy", map.GridCoords);
            var changer = entities.GetComponent<RankChangerComponent>(chevron);
            var marine = entities.GetComponent<MarineComponent>(deputy);
            var originalIcon = marine.Icon;
            Assert.That(originalIcon, Is.Not.Null, "Ghost-role initialization must provide the deputy icon.");

            var ranks = entities.System<RankChangerSystem>();
            ranks.ApplyRank(deputy, changer);
            ranks.RevertRank(deputy, changer);
            Assert.That(marine.Icon, Is.Null, "Removing the sole chevron clears the displayed job icon.");
            ranks.ApplyRank(deputy, changer);
            Assert.That(marine.Icon, Is.EqualTo(originalIcon), "Re-equipping must recover the ghost-role job icon.");
        });
        await pair.CleanReturnAsync();
    }
}
