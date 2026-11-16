using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Yautja.BadBlood;
using Content.Shared._RMC14.Communications;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Power;
using Content.Shared._RMC14.Sensor;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Roles.Jobs;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// TEST-501: the objective-work helpers make what they say they make
[TestFixture]
public sealed class BadBloodHarnessObjectivesTest : GameTest
{
    private const string Totem = "CMUBadBloodTotem";

    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task SiteReactorMarineAndJobHelpers()
    {
        var map = await Pair.CreateTestMap();
        EntityUid sensor = default;
        EntityUid comms = default;
        EntityUid working = default;
        EntityUid broken = default;

        await Server.WaitAssertion(() =>
        {
            sensor = BadBloodTestHelpers.SpawnUplinkSite(Server, BadBloodTestSiteKind.Sensor, map.GridCoords);
            comms = BadBloodTestHelpers.SpawnUplinkSite(Server, BadBloodTestSiteKind.Comms, map.GridCoords);
            working = BadBloodTestHelpers.SpawnReactor(Server, map.GridCoords, working: true);
            broken = BadBloodTestHelpers.SpawnReactor(Server, map.GridCoords, working: false);

            var marine = BadBloodTestHelpers.SpawnMarineNear(Server, map.GridCoords);
            Assert.That(SEntMan.HasComponent<MarineComponent>(marine), Is.True);

            var mind = BadBloodTestHelpers.GiveJob(Server, marine, BadBloodTestHelpers.MemberJob);
            Assert.That(Server.System<SharedMindSystem>().GetMind(marine), Is.EqualTo(mind));
            Assert.That(Server.System<SharedJobSystem>().MindTryGetJobId(mind, out var job), Is.True);
            Assert.That(job, Is.EqualTo(BadBloodTestHelpers.MemberJob));
        });

        // the states hold once the systems have ticked (an unpowered tower must not drop to Off on its own)
        await BadBloodTestHelpers.AdvanceTime(Pair, TimeSpan.FromSeconds(2));

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(SComp<SensorTowerComponent>(sensor).State, Is.EqualTo(SensorTowerState.On));
                Assert.That(SComp<CommunicationsTowerComponent>(comms).State, Is.EqualTo(CommunicationsTowerState.On));
                Assert.That(SComp<RMCFusionReactorComponent>(working).State, Is.EqualTo(RMCFusionReactorState.Working));
                Assert.That(SComp<RMCFusionReactorComponent>(broken).State, Is.EqualTo(RMCFusionReactorState.Weld));
            });
        });
    }

    [Test]
    public async Task ForceTotemRipeMakesARegisteredTotemRipe()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        EntityUid totem = default;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var maps = Server.System<SharedMapSystem>();
                maps.SetTile(map.Grid, map.Tile.GridIndices + new Vector2i(30, 0), map.Tile.Tile);

                var totems = Server.System<BadBloodTotemSystem>();
                totem = SEntMan.SpawnEntity(Totem, map.GridCoords.Offset(new Vector2(30, 0)));
                var comp = SEntMan.GetComponent<BadBloodTotemComponent>(totem);
                Assert.That(comp.Registered, Is.True, "a Totem raised while the rule runs is the clan's");
                Assert.That(totems.IsRipe(totem), Is.False);

                BadBloodTestHelpers.ForceTotemRipe(Server, totem);
                var now = Server.ResolveDependency<IGameTiming>().CurTime;
                Assert.That(now - comp.PlacedAt, Is.GreaterThanOrEqualTo(comp.RipeAfter));
                Assert.That(totems.IsRipe(totem), Is.True);
            });
        }
        finally
        {
            await BadBloodTestHelpers.EndBadBloodRule(Pair, party);
            await Server.WaitPost(() =>
            {
                foreach (var uid in party.All.Append(totem))
                {
                    if (uid.IsValid() && SEntMan.EntityExists(uid))
                        SEntMan.DeleteEntity(uid);
                }
            });
        }
    }
}
