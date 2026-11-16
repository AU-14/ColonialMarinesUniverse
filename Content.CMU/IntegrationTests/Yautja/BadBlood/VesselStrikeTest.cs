using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.ShipWeapons;
using Content.Server.CMU14.Yautja.BadBlood;
using Content.Shared._RMC14.Overwatch;
using Content.Shared.CMU14.ShipWeapons;
using Content.Shared.Roles;
using Content.Shared.Verbs;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.UnitTesting.Pool;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// ARCH-505: the Vessel Strike. Command fires it from a GOVFOR overwatch console after the uplink by spending a ready
// orbital battery; sabotage of the lock site pauses it and costs a one-time penalty; a re-route resumes it; the
// shipside cancel keeps the uplink and locks the strike out; running out decides MarinesVesselDestroyed
[TestFixture]
public sealed class VesselStrikeTest : GameTest
{
    private const string GovforConsole = "RMCOverwatchConsoleGovfor";
    private const string Battery = "CMUShipWeaponGun";
    private static readonly ProtoId<JobPrototype> PlatoonCommander = "AU14JobGOVFORPlatCo";
    private static readonly ProtoId<JobPrototype> SquadSergeant = "AU14JobGOVFORSquadSergeant";

    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    private sealed class Setup
    {
        public EntityUid Rule;
        public EntityUid Console;
        public EntityUid Battery;
        public EntityUid Site;
        public EntityUid Commander;
    }

    // a GOVFOR console and a battery on the test grid, a working comms tower as uplink site and a platoon commander.
    // the chain is uplinked unless told otherwise. call on the server thread
    private Setup Prepare(TestMapData map, BadBloodTestParty party, List<EntityUid> spawned, bool uplinked = true,
        bool battery = true)
    {
        var entMan = Server.EntMan;
        Assert.That(party.Rule, Is.Not.Null, "the Bad Blood rule must start");
        var setup = new Setup { Rule = party.Rule!.Value };

        setup.Console = entMan.SpawnEntity(GovforConsole, map.GridCoords);
        spawned.Add(setup.Console);
        if (battery)
        {
            setup.Battery = entMan.SpawnEntity(Battery, map.GridCoords);
            spawned.Add(setup.Battery);
        }

        setup.Site = SpawnSite(map.GridCoords.Offset(new Vector2(0, 3)), spawned);
        setup.Commander = BadBloodTestHelpers.SpawnMarineNear(Server, map.GridCoords, new Vector2(0, 1));
        spawned.Add(setup.Commander);
        BadBloodTestHelpers.GiveJob(Server, setup.Commander, PlatoonCommander);

        var chain = entMan.GetComponent<BadBloodObjectiveChainComponent>(setup.Rule);
        chain.PowerMet = true;
        chain.CommsMet = true;
        chain.GateMet = true;
        chain.Signature = BadBloodSignature.Bracer;
        chain.Uplinked = uplinked;
        chain.LockSite = uplinked ? setup.Site : null;
        return setup;
    }

    private EntityUid SpawnSite(EntityCoordinates coords, List<EntityUid> spawned)
    {
        var site = BadBloodTestHelpers.SpawnUplinkSite(Server, BadBloodTestSiteKind.Comms, coords);
        spawned.Add(site);
        Server.EntMan.EnsureComponent<BadBloodUplinkSiteComponent>(site).Kind = BadBloodUplinkSiteKind.Comms;
        Server.EntMan.EnsureComponent<BadBloodSabotageTargetComponent>(site);
        return site;
    }

    [Test]
    public async Task FiringNeedsTheUplinkAnAuthorizedJobAndAReadyBattery()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        var spawned = new List<EntityUid>();

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var strikes = Server.System<BadBloodVesselStrikeSystem>();
                var setup = Prepare(map, party, spawned, uplinked: false, battery: false);
                var comp = entMan.GetComponent<BadBloodVesselStrikeComponent>(setup.Rule);

                Assert.That(comp.AuthorizedJobs, Does.Contain(PlatoonCommander.Id), "the rule YAML authorizes the CO");
                Assert.That(strikes.TryStartStrike(setup.Commander, setup.Console), Is.False, "no uplink, no strike");

                var chain = entMan.GetComponent<BadBloodObjectiveChainComponent>(setup.Rule);
                chain.Uplinked = true;
                chain.LockSite = setup.Site;
                Assert.That(strikes.TryStartStrike(setup.Commander, setup.Console), Is.False, "no battery, no strike");

                var sergeant = BadBloodTestHelpers.SpawnMarineNear(Server, map.GridCoords, new Vector2(1, 1));
                spawned.Add(sergeant);
                BadBloodTestHelpers.GiveJob(Server, sergeant, SquadSergeant);

                var battery = entMan.SpawnEntity(Battery, map.GridCoords);
                spawned.Add(battery);
                var gun = entMan.GetComponent<CMUShipWeaponGunComponent>(battery);
                gun.Ready = false;
                Assert.That(strikes.TryStartStrike(setup.Commander, setup.Console), Is.False,
                    "a battery waiting on repairs cannot fire");

                gun.Ready = true;
                Assert.That(strikes.TryStartStrike(sergeant, setup.Console), Is.False, "a squad sergeant is not command");
                Assert.That(strikes.TryStartStrike(party.Grunts[0], setup.Console), Is.False, "a Bad Blood is not command");
                Assert.That(strikes.IsVesselStrikeRunning(), Is.False);

                // the GOVFOR picker lists the strike as fireable now
                Server.System<CMUShipWeaponsSystem>().CMURefreshOverwatchConsoles();
                var listed = entMan.GetComponent<OverwatchConsoleComponent>(setup.Console).CMUShipWeapons;
                Assert.That(listed, Does.Contain(new CMUShipWeaponStatus(BadBloodVesselStrikeSystem.VesselStrikeWeapon, 1, 1)));
                Assert.That(entMan.GetComponent<OverwatchConsoleComponent>(setup.Console).CMUSelectedShipWeapon,
                    Is.Null, "an extra is never picked automatically");

                Assert.That(strikes.TryStartStrike(setup.Commander, setup.Console, out var reason), Is.True, reason);
                Assert.Multiple(() =>
                {
                    Assert.That(strikes.IsVesselStrikeRunning(), Is.True);
                    Assert.That(gun.Ready, Is.False, "firing spends the battery");
                    Assert.That(comp.Remaining, Is.EqualTo(comp.Duration));
                    Assert.That(strikes.TryGetStrikeConsole(out var console) && console == setup.Console, Is.True);
                    Assert.That(entMan.HasComponent<BadBloodVesselStrikeConsoleComponent>(setup.Console), Is.True);
                    Assert.That(strikes.GetStrikeShip(), Is.EqualTo(map.Grid.Owner));
                    Assert.That(entMan.GetComponent<OverwatchConsoleComponent>(setup.Console).CMUShipWeapons,
                        Does.Contain(new CMUShipWeaponStatus(BadBloodVesselStrikeSystem.VesselStrikeWeapon, 0, 1)),
                        "a running strike cannot be fired again");
                });
            });
        }
        finally
        {
            await BadBloodTestHelpers.EndBadBloodRule(Pair, party);
            await Cleanup(spawned, party);
        }
    }

    [Test]
    public async Task SabotagePausesWithAOneTimePenaltyAndAReRouteResumes()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        var spawned = new List<EntityUid>();
        Setup setup = default!;
        EntityUid second = default;
        EntityUid marine = default;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var strikes = Server.System<BadBloodVesselStrikeSystem>();
                setup = Prepare(map, party, spawned);
                second = SpawnSite(map.GridCoords.Offset(new Vector2(3, 3)), spawned);
                marine = BadBloodTestHelpers.SpawnMarineNear(Server, map.GridCoords, new Vector2(2, 1));
                spawned.Add(marine);

                Assert.That(strikes.TryStartStrike(setup.Commander, setup.Console, out var reason), Is.True, reason);
                var comp = entMan.GetComponent<BadBloodVesselStrikeComponent>(setup.Rule);
                comp.Remaining = TimeSpan.FromSeconds(500);

                Assert.That(strikes.CanReroute(marine, second, out _), Is.False,
                    "no re-route while the lock site works, it would only hand out immunity");
                Assert.That(strikes.TryReroute(marine, second), Is.False);

                Assert.That(Server.System<BadBloodSabotageSystem>().TrySabotage(party.Grunts[0], setup.Site), Is.True);
                Assert.That(strikes.CanReroute(marine, second, out var rerouteReason), Is.True,
                    $"a broken lock site can be re-routed: {rerouteReason}");
                Assert.Multiple(() =>
                {
                    Assert.That(comp.Paused, Is.True, "sabotage of the lock site pauses the strike");
                    Assert.That(comp.Remaining, Is.EqualTo(TimeSpan.FromSeconds(560)), "and costs the 60 s penalty");
                    Assert.That(comp.PenaltyApplied, Is.True);
                });

                // a second report of the same sabotage costs nothing more
                var again = new BadBloodSiteSabotagedEvent(setup.Site, party.Grunts[0]);
                entMan.EventBus.RaiseEvent(EventSource.Local, ref again);
                Assert.That(comp.Remaining, Is.EqualTo(TimeSpan.FromSeconds(560)), "the penalty applies once");
            });

            await Pair.RunSeconds(3);

            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var strikes = Server.System<BadBloodVesselStrikeSystem>();
                var comp = entMan.GetComponent<BadBloodVesselStrikeComponent>(setup.Rule);
                Assert.Multiple(() =>
                {
                    Assert.That(comp.Paused, Is.True, "the strike waits while the lock site is down");
                    Assert.That(comp.Remaining, Is.EqualTo(TimeSpan.FromSeconds(560)));
                    Assert.That(comp.PausedTotal, Is.GreaterThan(TimeSpan.Zero));
                    Assert.That(strikes.CanReroute(party.Grunts[0], second, out _), Is.False, "Bad Bloods cannot re-route");
                    Assert.That(strikes.CanReroute(marine, setup.Site, out _), Is.False, "the lock is already there");
                });

                Assert.That(strikes.TryReroute(marine, second), Is.True);
                var chain = Server.System<BadBloodObjectiveChainSystem>();
                var target = entMan.GetComponent<BadBloodSabotageTargetComponent>(second);
                Assert.Multiple(() =>
                {
                    Assert.That(chain.GetLockSite(), Is.EqualTo(second), "the lock moves");
                    Assert.That(comp.Paused, Is.False, "a re-route resumes the strike");
                    Assert.That(comp.PenaltyApplied, Is.False);
                    Assert.That(target.ImmuneUntil, Is.GreaterThan(Server.Timing.CurTime), "the new site is immune");
                    Assert.That(Server.System<BadBloodSabotageSystem>().CanSabotage(party.Grunts[0], second, out _),
                        Is.False);
                });
            });

            await Pair.RunSeconds(3);

            await Server.WaitAssertion(() =>
            {
                var comp = Server.EntMan.GetComponent<BadBloodVesselStrikeComponent>(setup.Rule);
                Assert.That(comp.Remaining, Is.LessThan(TimeSpan.FromSeconds(560)), "the strike counts down again");

                // the penalty never lifts the timer above the full duration
                comp.Remaining = comp.Duration - TimeSpan.FromSeconds(10);
                var ev = new BadBloodSiteSabotagedEvent(second, party.Grunts[0]);
                Server.EntMan.EventBus.RaiseEvent(EventSource.Local, ref ev);
                Assert.That(comp.Remaining, Is.EqualTo(comp.Duration), "capped at the duration");
            });
        }
        finally
        {
            await BadBloodTestHelpers.EndBadBloodRule(Pair, party);
            await Cleanup(spawned, party);
        }
    }

    [Test]
    public async Task CancelKeepsTheUplinkAndLocksTheStrikeOut()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        var spawned = new List<EntityUid>();

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var strikes = Server.System<BadBloodVesselStrikeSystem>();
                var setup = Prepare(map, party, spawned);
                var comp = entMan.GetComponent<BadBloodVesselStrikeComponent>(setup.Rule);
                var gun = entMan.GetComponent<CMUShipWeaponGunComponent>(setup.Battery);

                Assert.That(strikes.TryCancelVesselStrike("test"), Is.False, "nothing to cancel");
                Assert.That(strikes.TryStartStrike(setup.Commander, setup.Console, out var reason), Is.True, reason);
                Assert.That(strikes.TryCancelVesselStrike("test"), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(strikes.IsVesselStrikeRunning(), Is.False);
                    Assert.That(Server.System<BadBloodObjectiveChainSystem>().IsUplinked(), Is.True,
                        "the coordinates are kept");
                    Assert.That(comp.LockoutUntil, Is.GreaterThan(Server.Timing.CurTime));
                    Assert.That(entMan.HasComponent<BadBloodVesselStrikeConsoleComponent>(setup.Console), Is.False);
                    Assert.That(strikes.TryGetStrikeConsole(out _), Is.False);
                });

                // the crew repaired the battery: still locked out
                gun.Ready = true;
                Assert.That(strikes.TryStartStrike(setup.Commander, setup.Console), Is.False, "locked out");

                comp.LockoutUntil = Server.Timing.CurTime - TimeSpan.FromSeconds(1);
                Assert.That(strikes.TryStartStrike(setup.Commander, setup.Console, out reason), Is.True,
                    $"fires again after the lockout without a new uplink: {reason}");
                Assert.That(comp.Remaining, Is.EqualTo(comp.Duration), "a new strike runs the full duration");
            });
        }
        finally
        {
            await BadBloodTestHelpers.EndBadBloodRule(Pair, party);
            await Cleanup(spawned, party);
        }
    }

    [Test]
    public async Task TheMarkerMovesWhenTheFiringConsoleIsDeleted()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        var spawned = new List<EntityUid>();

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var strikes = Server.System<BadBloodVesselStrikeSystem>();
                var setup = Prepare(map, party, spawned);
                var spare = entMan.SpawnEntity(GovforConsole, map.GridCoords);
                spawned.Add(spare);

                Assert.That(strikes.TryStartStrike(setup.Commander, setup.Console, out var reason), Is.True, reason);
                entMan.DeleteEntity(setup.Console);

                Assert.Multiple(() =>
                {
                    Assert.That(strikes.IsVesselStrikeRunning(), Is.True);
                    Assert.That(strikes.TryGetStrikeConsole(out var console) && console == spare, Is.True,
                        "the strike now runs from the other GOVFOR console on the ship");
                    Assert.That(entMan.HasComponent<BadBloodVesselStrikeConsoleComponent>(spare), Is.True);
                });

                // the shipside counter works at the new console
                var verbs = new GetVerbsEvent<AlternativeVerb>(party.Grunts[0], spare, null, null, true, true, true, new());
                entMan.EventBus.RaiseLocalEvent(spare, verbs);
                var text = Loc.GetString("cmu-bad-blood-shipside-sabotage-verb");
                Assert.That(verbs.Verbs.Where(verb => verb.Text == text).ToList(), Has.Count.EqualTo(1),
                    "a Grunt gets the sabotage verb on the new console");
            });
        }
        finally
        {
            await BadBloodTestHelpers.EndBadBloodRule(Pair, party);
            await Cleanup(spawned, party);
        }
    }

    [Test]
    public async Task RunningOutDestroysTheVessel()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        var spawned = new List<EntityUid>();
        Setup setup = default!;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var strikes = Server.System<BadBloodVesselStrikeSystem>();
                setup = Prepare(map, party, spawned);
                Assert.That(strikes.TryStartStrike(setup.Commander, setup.Console, out var reason), Is.True, reason);
                Server.EntMan.GetComponent<BadBloodVesselStrikeComponent>(setup.Rule).Remaining =
                    TimeSpan.FromSeconds(2);
            });

            await Pair.RunSeconds(4);

            await Server.WaitAssertion(() =>
            {
                var strikes = Server.System<BadBloodVesselStrikeSystem>();
                var comp = Server.EntMan.GetComponent<BadBloodVesselStrikeComponent>(setup.Rule);
                Assert.Multiple(() =>
                {
                    Assert.That(strikes.IsStrikeComplete(), Is.True);
                    Assert.That(comp.Running, Is.False);
                    Assert.That(Server.System<BadBloodCampRuleSystem>().GetRoundOutcome(),
                        Is.EqualTo(BadBloodRoundOutcome.MarinesVesselDestroyed));
                    Assert.That(strikes.TryStartStrike(setup.Commander, setup.Console), Is.False,
                        "a destroyed vessel cannot be struck again");
                });
            });
        }
        finally
        {
            await BadBloodTestHelpers.EndBadBloodRule(Pair, party);
            await Cleanup(spawned, party);
        }
    }

    private Task Cleanup(List<EntityUid> spawned, BadBloodTestParty party)
    {
        return Server.WaitPost(() =>
        {
            foreach (var uid in spawned.Concat(party.All))
            {
                if (Server.EntMan.EntityExists(uid))
                    Server.EntMan.DeleteEntity(uid);
            }
        });
    }
}
