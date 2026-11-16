using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Yautja.BadBlood;
using Content.Shared.DoAfter;
using Content.Shared.Verbs;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// FLOW-514: the shipside counter. A Bad Blood at the console a running Vessel Strike was fired from cancels the strike;
// gate, signature and uplink are kept. Marines and honourable hunters never get the verb
[TestFixture]
public sealed class BadBloodShipsideCounterTest : GameTest
{
    private const string Console = "RMCOverwatchConsole";
    private const string Hunter = "CMUMobYautja";

    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task AGruntCancelsTheRunningStrike()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        var spawned = new List<EntityUid>();
        EntityUid console = default;
        EntityUid grunt = default;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                Assert.That(party.Rule, Is.Not.Null, "the Bad Blood rule must start");
                var rule = party.Rule!.Value;
                grunt = party.Grunts[0];

                var counter = entMan.GetComponent<BadBloodShipsideCounterComponent>(rule);
                Assert.Multiple(() =>
                {
                    Assert.That(counter.SabotageDoAfter, Is.EqualTo(TimeSpan.FromSeconds(30)));
                    Assert.That(counter.AnnounceOnStart, Is.True);
                });
                counter.SabotageDoAfter = TimeSpan.FromSeconds(1);

                // the grunt stands at (1, 0); the console is one tile north of him, unpowered
                var gruntCoords = entMan.GetComponent<TransformComponent>(grunt).Coordinates;
                console = entMan.SpawnEntity(Console, gruntCoords.Offset(new Vector2(0, 1)));
                spawned.Add(console);
                entMan.EnsureComponent<BadBloodVesselStrikeConsoleComponent>(console);

                var marine = BadBloodTestHelpers.SpawnMarineNear(Server, gruntCoords, new Vector2(1, 0));
                var hunter = entMan.SpawnEntity(Hunter, gruntCoords.Offset(new Vector2(-1, 0)));
                spawned.Add(marine);
                spawned.Add(hunter);

                // no strike runs: nobody gets the verb, not even at a marked console
                Assert.That(Server.System<BadBloodVesselStrikeSystem>().IsVesselStrikeRunning(), Is.False);
                Assert.That(SabotageVerbs(grunt, console), Is.Empty, "no verb while no strike runs");

                StartStrike(rule, console, gruntCoords.Offset(new Vector2(0, 6)), spawned);
                Assert.That(Server.System<BadBloodVesselStrikeSystem>().IsVesselStrikeRunning(), Is.True,
                    "the strike state set by the test must read as running");

                Assert.That(SabotageVerbs(marine, console), Is.Empty, "a marine gets no verb");
                Assert.That(SabotageVerbs(hunter, console), Is.Empty, "an honourable hunter gets no verb");

                var verbs = SabotageVerbs(grunt, console);
                Assert.That(verbs, Has.Count.EqualTo(1), "a Grunt gets the verb");
                verbs[0].Act!();

                Assert.That(entMan.GetComponent<DoAfterComponent>(grunt).DoAfters, Has.Count.EqualTo(1),
                    "the verb starts the do-after");
            });

            await Pair.RunSeconds(2);

            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var rule = party.Rule!.Value;
                var strike = entMan.GetComponent<BadBloodVesselStrikeComponent>(rule);
                var chain = entMan.GetComponent<BadBloodObjectiveChainComponent>(rule);
                Assert.Multiple(() =>
                {
                    Assert.That(Server.System<BadBloodVesselStrikeSystem>().IsVesselStrikeRunning(), Is.False,
                        "the sabotage cancels the strike");
                    Assert.That(strike.LockoutUntil, Is.GreaterThan(Server.Timing.CurTime), "the console is locked out");

                    // gate, signature and coordinates are kept: command re-fires without a new uplink
                    Assert.That(chain.GateMet, Is.True);
                    Assert.That(chain.Signature, Is.EqualTo(BadBloodSignature.Bracer));
                    Assert.That(chain.Uplinked, Is.True, "the uplink survives the cancel");
                    Assert.That(Server.System<BadBloodObjectiveChainSystem>().IsUplinked(), Is.True,
                        "the chain still reports the uplink");

                    Assert.That(SabotageVerbs(grunt, console), Is.Empty, "no verb once the strike is cancelled");
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
    public async Task MovingBreaksTheSabotage()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        var spawned = new List<EntityUid>();
        EntityUid grunt = default;
        EntityUid console = default;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                Assert.That(party.Rule, Is.Not.Null, "the Bad Blood rule must start");
                var rule = party.Rule!.Value;
                grunt = party.Grunts[0];
                entMan.GetComponent<BadBloodShipsideCounterComponent>(rule).SabotageDoAfter = TimeSpan.FromSeconds(3);

                var gruntCoords = entMan.GetComponent<TransformComponent>(grunt).Coordinates;
                console = entMan.SpawnEntity(Console, gruntCoords.Offset(new Vector2(0, 1)));
                spawned.Add(console);
                entMan.EnsureComponent<BadBloodVesselStrikeConsoleComponent>(console);
                StartStrike(rule, console, gruntCoords.Offset(new Vector2(0, 6)), spawned);

                Assert.That(Server.System<BadBloodStrikeSabotageSystem>().TryStartSabotage(grunt, console), Is.True);
            });

            await Pair.RunSeconds(1);
            await Server.WaitPost(() =>
            {
                var xform = Server.System<SharedTransformSystem>();
                xform.SetCoordinates(grunt, map.GridCoords.Offset(new Vector2(8, 0)));
            });
            await Pair.RunSeconds(3);

            await Server.WaitAssertion(() =>
            {
                Assert.That(Server.System<BadBloodVesselStrikeSystem>().IsVesselStrikeRunning(), Is.True,
                    "moving away breaks the sabotage");
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

    // a running strike fired from console after a latched gate, a bracer signature and an uplink, the way the chain
    // and the strike leave the rule entity. call on the server thread
    private void StartStrike(EntityUid rule, EntityUid console, EntityCoordinates siteAt, List<EntityUid> spawned)
    {
        var entMan = Server.EntMan;
        var site = BadBloodTestHelpers.SpawnUplinkSite(Server, BadBloodTestSiteKind.Sensor, siteAt);
        spawned.Add(site);

        var chain = entMan.GetComponent<BadBloodObjectiveChainComponent>(rule);
        chain.PowerMet = true;
        chain.CommsMet = true;
        chain.GateMet = true;
        chain.Signature = BadBloodSignature.Bracer;
        chain.Uplinked = true;
        chain.LockSite = site;

        var strike = entMan.GetComponent<BadBloodVesselStrikeComponent>(rule);
        strike.Running = true;
        strike.Paused = false;
        strike.Completed = false;
        strike.Remaining = strike.Duration;
        strike.LockoutUntil = TimeSpan.Zero;
        strike.NextTick = Server.Timing.CurTime + TimeSpan.FromHours(1);
        strike.Console = console;
    }

    private List<AlternativeVerb> SabotageVerbs(EntityUid user, EntityUid console)
    {
        var verbs = new GetVerbsEvent<AlternativeVerb>(user, console, null, null, true, true, true, new());
        Server.EntMan.EventBus.RaiseLocalEvent(console, verbs);
        var text = Loc.GetString("cmu-bad-blood-shipside-sabotage-verb");
        return verbs.Verbs.Where(verb => verb.Text == text).ToList();
    }
}
