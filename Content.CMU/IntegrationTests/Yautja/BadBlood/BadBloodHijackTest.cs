#pragma warning disable RA0002 // tests read component data directly
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Yautja.BadBlood;
using Content.Shared._RMC14.AntiAir;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// ECS-503, STATE-503, TEST-503: the clan's Leader can hijack a dropship the xeno way, and a Bad Blood hijack is tracked
[TestFixture]
public sealed class BadBloodHijackTest : GameTest
{
    private const string Hunter = "CMUMobYautja";
    private const string EventMob = "CMUMobYautjaBadBlood";

    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task OnlyTheLeaderCarriesTheHijackerAndItMovesWithLeadership()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 2);
        var spawned = new List<EntityUid>();

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var rules = Server.System<BadBloodCampRuleSystem>();
                Assert.That(party.Rule, Is.Not.Null, "the helper starts the rule");

                Assert.Multiple(() =>
                {
                    Assert.That(rules.GetLeader(), Is.EqualTo(party.Leader));
                    Assert.That(entMan.HasComponent<DropshipHijackerComponent>(party.Leader), Is.True,
                        "the camp Leader can hijack a dropship");
                    foreach (var grunt in party.Grunts)
                    {
                        Assert.That(entMan.HasComponent<DropshipHijackerComponent>(grunt), Is.False,
                            "grunts cannot hijack");
                    }
                });

                // leadership moves, the hijacker moves with it
                var heir = party.Grunts[0];
                Assert.That(rules.SetLeader(heir), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<DropshipHijackerComponent>(heir), Is.True);
                    Assert.That(entMan.HasComponent<DropshipHijackerComponent>(party.Leader), Is.False,
                        "the old Leader loses the hijacker");
                    Assert.That(entMan.HasComponent<DropshipHijackerComponent>(party.Grunts[1]), Is.False);
                });

                // honourable hunters never lead the clan and never get it
                var hunter = entMan.SpawnEntity(Hunter, map.GridCoords);
                spawned.Add(hunter);
                Assert.That(rules.SetLeader(hunter), Is.False);
                Assert.That(entMan.HasComponent<DropshipHijackerComponent>(hunter), Is.False);

                // the event mob declares the hijacker itself and keeps it when it stops leading
                var eventMob = entMan.SpawnEntity(EventMob, map.GridCoords);
                spawned.Add(eventMob);
                Assert.That(entMan.HasComponent<DropshipHijackerComponent>(eventMob), Is.True);
                Assert.That(rules.SetLeader(eventMob), Is.True);
                Assert.That(rules.SetLeader(party.Leader), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<DropshipHijackerComponent>(eventMob), Is.True,
                        "a prototype hijacker keeps the component after leading");
                    Assert.That(entMan.HasComponent<DropshipHijackerComponent>(party.Leader), Is.True);
                    Assert.That(entMan.HasComponent<DropshipHijackerComponent>(heir), Is.False);
                });
            });
        }
        finally
        {
            await BadBloodTestHelpers.EndBadBloodRule(Pair, party);
            await Server.WaitPost(() =>
            {
                foreach (var uid in spawned)
                {
                    if (Server.EntMan.EntityExists(uid))
                        Server.EntMan.DeleteEntity(uid);
                }
            });
        }

        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.EntMan.HasComponent<DropshipHijackerComponent>(party.Leader), Is.False,
                "ending the rule clears the Leader and the hijacker with it");
        });
    }

    [Test]
    public async Task ABadBloodHijackMarksTheDropshipAndOthersDoNot()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        var spawned = new List<EntityUid>();

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;

                // stand-in dropships: the handler only reads the event's entities
                var badBloodShip = entMan.SpawnEntity(null, map.GridCoords);
                var otherShip = entMan.SpawnEntity(null, map.GridCoords);
                var hunter = entMan.SpawnEntity(Hunter, map.GridCoords);
                spawned.Add(badBloodShip);
                spawned.Add(otherShip);
                spawned.Add(hunter);

                Raise(entMan, badBloodShip, party.Leader, map.Grid.Owner);
                Raise(entMan, otherShip, hunter, map.Grid.Owner);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.TryGetComponent(badBloodShip, out BadBloodHijackedDropshipComponent? marked), Is.True,
                        "a Bad Blood hijack marks the dropship");
                    Assert.That(marked?.Hijacker, Is.EqualTo(party.Leader));
                    Assert.That(marked?.CrashLogged, Is.False);
                    Assert.That(entMan.HasComponent<BadBloodHijackedDropshipComponent>(otherShip), Is.False,
                        "a hijack by anyone else is not a Bad Blood hijack");
                });
            });
        }
        finally
        {
            await BadBloodTestHelpers.EndBadBloodRule(Pair, party);
            await Server.WaitPost(() =>
            {
                foreach (var uid in spawned)
                {
                    if (Server.EntMan.EntityExists(uid))
                        Server.EntMan.DeleteEntity(uid);
                }
            });
        }
    }

    // TEST-503: a Bad Blood hijack during a running Vessel Strike marks the dropship and leaves the strike alone: it
    // keeps running, its timer is not touched, the Leader stays and the clan is not wiped. Then the shipside counter,
    // the console sabotage do-after, cancels the strike with a lockout and the uplink kept
    [Test]
    public async Task AHijackLeavesTheStrikeAndTheConsoleSabotageCancelsIt()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        var spawned = new List<EntityUid>();
        EntityUid console = default;
        EntityUid dropship = default;
        var remainingBefore = TimeSpan.Zero;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var rules = Server.System<BadBloodCampRuleSystem>();
                var strikes = Server.System<BadBloodVesselStrikeSystem>();
                Assert.That(party.Rule, Is.Not.Null, "the Bad Blood rule must start");
                var rule = party.Rule!.Value;

                // the VesselStrikeTest setup: a GOVFOR console and a battery on the grid, a working comms tower as the
                // lock site, a platoon commander, and a chain that is uplinked
                console = entMan.SpawnEntity(StrikeConsole, map.GridCoords);
                spawned.Add(console);
                spawned.Add(entMan.SpawnEntity(StrikeBattery, map.GridCoords));

                var site = BadBloodTestHelpers.SpawnUplinkSite(Server, BadBloodTestSiteKind.Comms,
                    map.GridCoords.Offset(new Vector2(0, 3)));
                spawned.Add(site);
                entMan.EnsureComponent<BadBloodUplinkSiteComponent>(site).Kind = BadBloodUplinkSiteKind.Comms;
                entMan.EnsureComponent<BadBloodSabotageTargetComponent>(site);

                var commander = BadBloodTestHelpers.SpawnMarineNear(Server, map.GridCoords, new Vector2(0, 1));
                spawned.Add(commander);
                BadBloodTestHelpers.GiveJob(Server, commander, PlatoonCommander);

                var chain = entMan.GetComponent<BadBloodObjectiveChainComponent>(rule);
                chain.PowerMet = true;
                chain.CommsMet = true;
                chain.GateMet = true;
                chain.Signature = BadBloodSignature.Bracer;
                chain.Uplinked = true;
                chain.LockSite = site;

                Assert.That(strikes.TryStartStrike(commander, console, out var reason), Is.True, reason);
                var strike = entMan.GetComponent<BadBloodVesselStrikeComponent>(rule);
                remainingBefore = strike.Remaining;

                // the Leader hijacks: the anti-air resolution, then the crash on the test map
                dropship = entMan.SpawnEntity(null, map.GridCoords);
                spawned.Add(dropship);
                Raise(entMan, dropship, party.Leader, map.Grid.Owner);
                var landed = new DropshipHijackLandedEvent(map.MapUid);
                entMan.EventBus.RaiseEvent(EventSource.Local, ref landed);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.TryGetComponent(dropship, out BadBloodHijackedDropshipComponent? marked), Is.True,
                        "the hijacked dropship is marked");
                    Assert.That(marked?.Hijacker, Is.EqualTo(party.Leader));
                    Assert.That(marked?.CrashLogged, Is.True, "the crash is logged once it lands");
                    Assert.That(strikes.IsVesselStrikeRunning(), Is.True, "the hijack does not touch the strike");
                    Assert.That(strike.Remaining, Is.EqualTo(remainingBefore), "nor its timer");
                    Assert.That(rules.GetLeader(), Is.EqualTo(party.Leader));
                    Assert.That(rules.IsLeaderGone(), Is.False);
                });
            });

            await Pair.RunSeconds(2);

            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var rules = Server.System<BadBloodCampRuleSystem>();
                var strikes = Server.System<BadBloodVesselStrikeSystem>();
                var strike = entMan.GetComponent<BadBloodVesselStrikeComponent>(party.Rule!.Value);
                Assert.Multiple(() =>
                {
                    Assert.That(strikes.IsVesselStrikeRunning(), Is.True);
                    Assert.That(strike.Paused, Is.False);
                    Assert.That(strike.Remaining, Is.LessThan(remainingBefore), "the strike keeps counting down");
                    Assert.That(strike.Remaining, Is.GreaterThanOrEqualTo(remainingBefore - TimeSpan.FromSeconds(3)),
                        "by its normal ticking only");
                    Assert.That(rules.GetLeader(), Is.EqualTo(party.Leader), "the Leader is not gone");
                    Assert.That(rules.IsLeaderGone(), Is.False);
                    Assert.That(rules.CountLivingBadBloods(), Is.GreaterThanOrEqualTo(2), "the clan is not wiped");
                    Assert.That(rules.GetRoundOutcome(), Is.EqualTo(BadBloodRoundOutcome.None), "the round goes on");
                });

                // the grunt at (1, 0) works the strike console one tile west of him
                entMan.GetComponent<BadBloodShipsideCounterComponent>(party.Rule!.Value).SabotageDoAfter =
                    TimeSpan.FromSeconds(1);
                Assert.That(Server.System<BadBloodStrikeSabotageSystem>().TryStartSabotage(party.Grunts[0], console),
                    Is.True);
            });

            await Pair.RunSeconds(2);

            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var strikes = Server.System<BadBloodVesselStrikeSystem>();
                var strike = entMan.GetComponent<BadBloodVesselStrikeComponent>(party.Rule!.Value);
                Assert.Multiple(() =>
                {
                    Assert.That(strikes.IsVesselStrikeRunning(), Is.False, "the console sabotage cancels the strike");
                    Assert.That(strike.Completed, Is.False);
                    Assert.That(strike.LockoutUntil, Is.GreaterThan(Server.Timing.CurTime), "with the lockout");
                    Assert.That(Server.System<BadBloodObjectiveChainSystem>().IsUplinked(), Is.True,
                        "the uplink is kept");
                    Assert.That(entMan.HasComponent<BadBloodVesselStrikeConsoleComponent>(console), Is.False);
                    Assert.That(Server.System<BadBloodCampRuleSystem>().GetRoundOutcome(),
                        Is.EqualTo(BadBloodRoundOutcome.None));
                });
            });
        }
        finally
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

    [Test]
    public async Task AYautjaHijackGivesTheHiveNoSurge()
    {
        var map = await Pair.CreateTestMap();
        EntityUid hive = default;

        await Server.WaitAssertion(() =>
        {
            hive = SEntMan.SpawnEntity("CMUAlphaHive", map.GridCoords);

            // a Bad Blood hijack is typed Yautja: crash effects only, no evolution surge or ovipositor reset
            var yautja = new DropshipHijackStartEvent(null, HijackerType: DropshipHijackerType.Yautja);
            SEntMan.EventBus.RaiseEvent(EventSource.Local, ref yautja);
            Assert.That(SEntMan.GetComponent<HiveComponent>(hive).HijackSurged, Is.False,
                "a Yautja hijack must not surge the hive");

            // the xeno hijack still surges, so the check above can fail
            var xeno = new DropshipHijackStartEvent(null, HijackerType: DropshipHijackerType.Xeno);
            SEntMan.EventBus.RaiseEvent(EventSource.Local, ref xeno);
            Assert.That(SEntMan.GetComponent<HiveComponent>(hive).HijackSurged, Is.True);
        });

        await Server.WaitPost(() =>
        {
            if (SEntMan.EntityExists(hive))
                SEntMan.DeleteEntity(hive);
        });
    }

    private const string StrikeConsole = "RMCOverwatchConsoleGovfor";
    private const string StrikeBattery = "CMUShipWeaponGun";
    private static readonly ProtoId<JobPrototype> PlatoonCommander = "AU14JobGOVFORPlatCo";

    // no anti-air console and no deterrence, so the RMC anti-air subscriber does nothing with it
    private static void Raise(IEntityManager entMan, EntityUid dropship, EntityUid actor, EntityUid destination)
    {
        var ev = new RMCDropshipHijackAntiAirResolvedEvent(
            dropship,
            actor,
            destination,
            destination,
            null,
            null,
            null,
            false,
            null,
            0,
            0);
        entMan.EventBus.RaiseEvent(EventSource.Local, ref ev);
    }
}
