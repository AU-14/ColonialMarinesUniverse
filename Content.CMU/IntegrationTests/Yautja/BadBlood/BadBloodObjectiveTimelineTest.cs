#pragma warning disable RA0002 // the test shortens the reveal dedupe on the rule entity
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Yautja.BadBlood;
using Content.Server.GameTicking;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// STATE-504: the objective timeline records every objective broadcast in order, dedupes the Lodge reveal each hit
// raises, counts deaths near objective sites and prints an "Objective timeline" section at round end
[TestFixture]
public sealed class BadBloodObjectiveTimelineTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task RecordsTransitionsInOrderAndDedupesLodgeHitReveals()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        var spawned = new List<EntityUid>();
        var start = 0;
        EntityUid lodge = default;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var timeline = Server.System<BadBloodObjectiveTimelineSystem>();
                Assert.That(party.Rule, Is.Not.Null, "the Bad Blood rule must start");
                var tunables = entMan.GetComponent<BadBloodObjectiveTimelineComponent>(party.Rule!.Value);
                Assert.Multiple(() =>
                {
                    Assert.That(tunables.EngagementRadius, Is.EqualTo(12f));
                    Assert.That(tunables.RevealDedupe, Is.EqualTo(TimeSpan.FromSeconds(30)));
                });
                tunables.RevealDedupe = TimeSpan.FromSeconds(1);

                var totem = entMan.SpawnEntity(null, map.GridCoords);
                var site = entMan.SpawnEntity(null, map.GridCoords);
                lodge = entMan.SpawnEntity(null, map.GridCoords);
                spawned.Add(totem);
                spawned.Add(site);
                spawned.Add(lodge);

                start = timeline.GetEntries().Count;

                Stage(entMan, BadBloodObjectiveStage.GateMet, "Power+Comms");
                Stage(entMan, BadBloodObjectiveStage.Signature, "Bracer");
                var desecrated = new BadBloodTotemDesecratedEvent(totem);
                entMan.EventBus.RaiseEvent(EventSource.Local, ref desecrated);
                var sabotaged = new BadBloodSiteSabotagedEvent(site, party.Grunts[0]);
                entMan.EventBus.RaiseEvent(EventSource.Local, ref sabotaged);

                // every hit on the Lodge reveals it again; only the first is recorded
                for (var i = 0; i < 3; i++)
                {
                    Reveal(entMan, lodge, BadBloodRevealSource.Attack);
                }

                // another source is its own entry
                Reveal(entMan, lodge, BadBloodRevealSource.Squad);
                Stage(entMan, BadBloodObjectiveStage.Hijack, "the Leader hijacked a dropship");

                var kinds = timeline.GetEntries().Skip(start).Select(e => e.Kind).ToList();
                Assert.That(kinds, Is.EqualTo(new[]
                {
                    BadBloodTimelineKind.GateMet,
                    BadBloodTimelineKind.Signature,
                    BadBloodTimelineKind.TotemDesecrated,
                    BadBloodTimelineKind.SiteSabotaged,
                    BadBloodTimelineKind.LodgeRevealed,
                    BadBloodTimelineKind.LodgeRevealed,
                    BadBloodTimelineKind.Hijack,
                }), "transitions in the order raised, one reveal per source");

                var entries = timeline.GetEntries().Skip(start).ToList();
                Assert.Multiple(() =>
                {
                    Assert.That(entries[0].Detail, Is.EqualTo("Power+Comms"));
                    Assert.That(entries[2].Site, Is.EqualTo(totem));
                    Assert.That(entries[3].Site, Is.EqualTo(site));
                    Assert.That(entries[4].Detail, Does.StartWith(nameof(BadBloodRevealSource.Attack)));
                    Assert.That(entries[5].Detail, Does.StartWith(nameof(BadBloodRevealSource.Squad)));
                    for (var i = 1; i < entries.Count; i++)
                    {
                        Assert.That(entries[i].SinceRule, Is.GreaterThanOrEqualTo(entries[i - 1].SinceRule));
                    }
                });
            });

            // past the dedupe window the next hit is a new reveal
            await Pair.RunSeconds(1.5f);

            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var timeline = Server.System<BadBloodObjectiveTimelineSystem>();
                Reveal(entMan, lodge, BadBloodRevealSource.Attack);
                Reveal(entMan, lodge, BadBloodRevealSource.Attack);

                var reveals = timeline.GetEntries().Skip(start).Count(e => e.Kind == BadBloodTimelineKind.LodgeRevealed);
                Assert.That(reveals, Is.EqualTo(3));
            });
        }
        finally
        {
            await Cleanup(party, spawned);
        }
    }

    [Test]
    public async Task DeathsNearASiteCountForThatSiteOnly()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        var spawned = new List<EntityUid>();
        EntityUid site = default;
        EntityUid near = default;
        EntityUid far = default;

        try
        {
            await Server.WaitPost(() =>
            {
                site = SpawnSite(map.GridCoords.Offset(new Vector2(5, 0)));
                spawned.Add(site);
                near = BadBloodTestHelpers.SpawnMarineNear(Server, map.GridCoords, new Vector2(8, 0));
                far = BadBloodTestHelpers.SpawnMarineNear(Server, map.GridCoords, new Vector2(60, 0));
                spawned.Add(near);
                spawned.Add(far);
            });

            await BadBloodTestHelpers.Kill(Pair, near);
            await BadBloodTestHelpers.Kill(Pair, far);
            // the grunt stands at (1, 0), four tiles from the site
            await BadBloodTestHelpers.Kill(Pair, party.Grunts[0]);

            await Server.WaitAssertion(() =>
            {
                var timeline = Server.System<BadBloodObjectiveTimelineSystem>();
                var engagement = timeline.GetEngagement(site);
                Assert.Multiple(() =>
                {
                    Assert.That(Server.System<MobStateSystem>().IsDead(far), Is.True);
                    Assert.That(engagement.MarineDeaths, Is.EqualTo(1), "the near marine counts, the far one does not");
                    Assert.That(engagement.BadBloodDeaths, Is.EqualTo(1), "a Bad Blood dying at the site counts");
                    Assert.That(engagement.Name, Is.Not.Empty);
                });

                // dying twice is not two deaths
                Server.System<MobStateSystem>().ChangeMobState(near, MobState.Dead);
                Assert.That(timeline.GetEngagement(site).MarineDeaths, Is.EqualTo(1));
            });
        }
        finally
        {
            await Cleanup(party, spawned);
        }
    }

    [Test]
    public async Task RoundEndTextHasTheObjectiveTimelineSection()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        var spawned = new List<EntityUid>();
        EntityUid site = default;
        EntityUid marine = default;

        try
        {
            await Server.WaitPost(() =>
            {
                site = SpawnSite(map.GridCoords.Offset(new Vector2(5, 0)));
                spawned.Add(site);
                marine = BadBloodTestHelpers.SpawnMarineNear(Server, map.GridCoords, new Vector2(6, 0));
                spawned.Add(marine);

                var entMan = Server.EntMan;
                Stage(entMan, BadBloodObjectiveStage.GateMet, "Power+Sensor");
                var sabotaged = new BadBloodSiteSabotagedEvent(site, party.Grunts[0]);
                entMan.EventBus.RaiseEvent(EventSource.Local, ref sabotaged);
                Stage(entMan, BadBloodObjectiveStage.Rerouted, "re-routed", site);
            });

            await BadBloodTestHelpers.Kill(Pair, marine);

            await Server.WaitAssertion(() =>
            {
                var ev = new RoundEndTextAppendEvent();
                Server.EntMan.EventBus.RaiseEvent(EventSource.Local, ev);
                var text = ev.Text;
                var loc = Server.ResolveDependency<ILocalizationManager>();
                var siteName = Server.EntMan.GetComponent<MetaDataComponent>(site).EntityName;

                Assert.Multiple(() =>
                {
                    Assert.That(text, Does.Contain(loc.GetString("cmu-bad-blood-timeline-header")));
                    Assert.That(text, Does.Contain(loc.GetString("cmu-bad-blood-timeline-kind-gatemet")));
                    Assert.That(text, Does.Contain("Power+Sensor"));
                    Assert.That(text, Does.Contain(loc.GetString("cmu-bad-blood-timeline-no-strike")));
                    Assert.That(text, Does.Contain(loc.GetString("cmu-bad-blood-timeline-counts",
                        ("sabotages", 1), ("reroutes", 1), ("cancels", 0), ("hijacks", 0), ("desecrations", 0))));
                    Assert.That(text, Does.Contain(loc.GetString("cmu-bad-blood-timeline-engagement",
                        ("site", siteName), ("marines", 1), ("badBloods", 0))));
                });
            });
        }
        finally
        {
            await Cleanup(party, spawned);
        }
    }

    // a comms tower that is an uplink site. call on the server thread
    private EntityUid SpawnSite(EntityCoordinates coords)
    {
        var site = BadBloodTestHelpers.SpawnUplinkSite(Server, BadBloodTestSiteKind.Comms, coords);
        Server.EntMan.EnsureComponent<BadBloodUplinkSiteComponent>(site).Kind = BadBloodUplinkSiteKind.Comms;
        return site;
    }

    private static void Stage(IEntityManager entMan, BadBloodObjectiveStage stage, string detail, EntityUid? site = null)
    {
        var ev = new BadBloodObjectiveStageEvent(stage, detail, site);
        entMan.EventBus.RaiseEvent(EventSource.Local, ref ev);
    }

    private static void Reveal(IEntityManager entMan, EntityUid lodge, BadBloodRevealSource source)
    {
        var ev = new BadBloodLodgeRevealedEvent(lodge, TimeSpan.FromSeconds(60), source);
        entMan.EventBus.RaiseEvent(EventSource.Local, ref ev);
    }

    private async Task Cleanup(BadBloodTestParty party, List<EntityUid> spawned)
    {
        await BadBloodTestHelpers.EndBadBloodRule(Pair, party);
        await Server.WaitPost(() =>
        {
            foreach (var uid in spawned.Concat(party.All))
            {
                if (Server.EntMan.EntityExists(uid))
                    Server.EntMan.DeleteEntity(uid);
            }
        });
    }
}
