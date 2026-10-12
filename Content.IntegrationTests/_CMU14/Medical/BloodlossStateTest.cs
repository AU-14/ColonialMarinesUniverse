using Content.IntegrationTests.Fixtures;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.CMU14.CrawlState;
using Content.Shared.CMU14.Medical.Bloodloss;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Medical;

[TestFixture]
public sealed class BloodlossStateTest : GameTest
{
    [Test]
    public async Task LowBloodKnocksOutWithAnUnstoppableCrawlAndVeryLowBloodKills()
    {
        var map = await Pair.CreateTestMap();
        EntityUid human = default;

        await Server.WaitAssertion(() =>
        {
            human = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            DrainTo(human, 0.30f);
        });

        // Long enough for the blood check and the crawl's activation delay.
        await Pair.RunSeconds(7f);

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<CMUBloodlossUnconsciousComponent>(human), Is.True);
                Assert.That(Server.System<MobStateSystem>().IsCritical(human), Is.True,
                    "under 40% blood should knock you out");
                Assert.That(SEntMan.TryGetComponent<ActiveCrawlWhileCritComponent>(human, out var crawl) && crawl.Started,
                    Is.True, "a mob knocked out by blood loss should be able to crawl");
            });

            var hit = new DamageSpecifier { DamageDict = { ["Blunt"] = FixedPoint2.New(5) } };
            Server.System<DamageableSystem>().TryChangeDamage(human, hit, ignoreResistances: true);
            Assert.That(SEntMan.HasComponent<ActiveCrawlWhileCritComponent>(human), Is.True,
                "getting hit shouldn't stop a blood loss crawl");

            DrainTo(human, 0.05f);
        });

        await Pair.RunSeconds(2f);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.System<MobStateSystem>().IsDead(human), Is.True, "under 10% blood should kill you");
        });
    }

    private void DrainTo(EntityUid human, float level)
    {
        var bloodstream = Server.System<BloodstreamSystem>();
        var blood = SEntMan.GetComponent<BloodstreamComponent>(human);
        var current = bloodstream.GetBloodLevel((human, blood));
        var drain = (current - level) * blood.BloodReferenceSolution.Volume.Float();
        Assert.That(bloodstream.TryModifyBloodLevel((human, blood), FixedPoint2.New(-drain)), Is.True);
    }
}
