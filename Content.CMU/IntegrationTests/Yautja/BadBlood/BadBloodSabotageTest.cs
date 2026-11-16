using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Yautja.BadBlood;
using Content.Shared._RMC14.Communications;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Power;
using Content.Shared._RMC14.Rules;
using Content.Shared._RMC14.Sensor;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Verbs;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// FLOW-509: Bad Bloods sabotage marine objective sites. Every kind breaks the way its own system breaks it, marines
// get no verb, a repaired site is immune for a while, and a Bad Blood cannot simply switch a comms tower off
[TestFixture]
public sealed class BadBloodSabotageTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task EverySiteKindBecomesATargetAndBreaks()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1, startRule: false);
        var spawned = new List<EntityUid>();
        EntityUid sensor = default, comms = default, reactor = default, relay = default;

        try
        {
            await Server.WaitPost(() =>
            {
                var entMan = Server.EntMan;
                // the sites are planet sites
                entMan.EnsureComponent<RMCPlanetComponent>(map.MapUid);

                sensor = BadBloodTestHelpers.SpawnUplinkSite(Server, BadBloodTestSiteKind.Sensor,
                    map.GridCoords.Offset(new Vector2(0, 2)));
                comms = BadBloodTestHelpers.SpawnUplinkSite(Server, BadBloodTestSiteKind.Comms,
                    map.GridCoords.Offset(new Vector2(2, 2)));
                reactor = BadBloodTestHelpers.SpawnReactor(Server, map.GridCoords.Offset(new Vector2(4, 2)));
                relay = entMan.SpawnEntity(null, map.GridCoords.Offset(new Vector2(6, 2)));
                entMan.EnsureComponent<BadBloodUplinkSiteComponent>(relay).Kind = BadBloodUplinkSiteKind.Relay;
                spawned.AddRange(new[] { sensor, comms, reactor, relay });
            });

            await BadBloodTestHelpers.StartBadBloodRule(Pair, party);

            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                Assert.That(party.Rule, Is.Not.Null, "the Bad Blood rule must start");
                foreach (var site in new[] { sensor, comms, reactor, relay })
                {
                    Assert.That(entMan.HasComponent<BadBloodSabotageTargetComponent>(site), Is.True,
                        $"{entMan.ToPrettyString(site)} becomes a sabotage target when the rule starts");
                }

                var sabotage = Server.System<BadBloodSabotageSystem>();
                var grunt = party.Grunts[0];
                foreach (var site in new[] { sensor, comms, reactor, relay })
                {
                    Assert.That(sabotage.TrySabotage(grunt, site), Is.True,
                        $"a Bad Blood sabotages {entMan.ToPrettyString(site)}");
                }

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.GetComponent<SensorTowerComponent>(sensor).State,
                        Is.EqualTo(SensorTowerState.Wrench), "the sensor tower needs a wrench");
                    Assert.That(entMan.GetComponent<CommunicationsTowerComponent>(comms).State,
                        Is.EqualTo(CommunicationsTowerState.Broken), "the comms tower is broken");
                    Assert.That(entMan.GetComponent<RMCFusionReactorComponent>(reactor).State,
                        Is.EqualTo(RMCFusionReactorState.Wire), "the reactor loses two stages");
                    Assert.That(sabotage.TrySabotage(grunt, comms), Is.False, "a broken site cannot be sabotaged again");
                });
            });

            await Pair.RunSeconds(1);

            await Server.WaitAssertion(() =>
            {
                Assert.That(Server.EntMan.Deleted(relay), Is.True, "a field relay is destroyed outright");
            });
        }
        finally
        {
            await BadBloodTestHelpers.EndBadBloodRule(Pair, party);
            await Cleanup(spawned, party);
        }
    }

    [Test]
    public async Task OnlyBadBloodsGetTheVerb()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        var spawned = new List<EntityUid>();

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var site = BadBloodTestHelpers.SpawnUplinkSite(Server, BadBloodTestSiteKind.Comms,
                    map.GridCoords.Offset(new Vector2(1, 1)));
                var marine = BadBloodTestHelpers.SpawnMarineNear(Server, map.GridCoords, new Vector2(1, 2));
                spawned.Add(site);
                spawned.Add(marine);
                entMan.EnsureComponent<BadBloodSabotageTargetComponent>(site);

                Assert.Multiple(() =>
                {
                    Assert.That(SabotageVerbs(marine, site), Is.Empty, "a marine gets no Sabotage verb");
                    Assert.That(SabotageVerbs(party.Grunts[0], site), Has.Count.EqualTo(1), "a Grunt gets the verb");
                    Assert.That(Server.System<BadBloodSabotageSystem>().TrySabotage(marine, site), Is.False,
                        "a marine cannot sabotage");
                });

                // the verb starts the do-after
                SabotageVerbs(party.Grunts[0], site)[0].Act!();
                Assert.That(entMan.GetComponent<DoAfterComponent>(party.Grunts[0]).DoAfters, Has.Count.EqualTo(1));
            });
        }
        finally
        {
            await BadBloodTestHelpers.EndBadBloodRule(Pair, party);
            await Cleanup(spawned, party);
        }
    }

    [Test]
    public async Task ImmunityRefusesAndARepairGrantsIt()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        var spawned = new List<EntityUid>();
        EntityUid site = default;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var sabotage = Server.System<BadBloodSabotageSystem>();
                var grunt = party.Grunts[0];
                var now = Server.Timing.CurTime;
                site = BadBloodTestHelpers.SpawnUplinkSite(Server, BadBloodTestSiteKind.Comms,
                    map.GridCoords.Offset(new Vector2(1, 1)));
                spawned.Add(site);

                var target = entMan.EnsureComponent<BadBloodSabotageTargetComponent>(site);
                Assert.That(target.SabotageTime, Is.EqualTo(TimeSpan.FromSeconds(25)));

                target.ImmuneUntil = now + TimeSpan.FromSeconds(60);
                Assert.That(sabotage.CanSabotage(grunt, site, out _), Is.False, "refused while immune");
                Assert.That(sabotage.TrySabotage(grunt, site), Is.False);
                Assert.That(entMan.GetComponent<CommunicationsTowerComponent>(site).State,
                    Is.EqualTo(CommunicationsTowerState.On));

                target.ImmuneUntil = TimeSpan.Zero;
                Assert.That(sabotage.TrySabotage(grunt, site), Is.True, "allowed once the immunity ran out");
                Assert.That(target.AwaitingRepair, Is.True);

                // the repair: welded back to Off, then switched On
                var tower = entMan.GetComponent<CommunicationsTowerComponent>(site);
                tower.State = CommunicationsTowerState.Off;
            });

            await Pair.RunSeconds(2);

            await Server.WaitAssertion(() =>
            {
                var target = Server.EntMan.GetComponent<BadBloodSabotageTargetComponent>(site);
                Assert.That(target.ImmuneUntil, Is.LessThanOrEqualTo(Server.Timing.CurTime),
                    "a tower that is only Off again is not repaired yet");
                Server.EntMan.GetComponent<CommunicationsTowerComponent>(site).State = CommunicationsTowerState.On;
            });

            await Pair.RunSeconds(2);

            await Server.WaitAssertion(() =>
            {
                var target = Server.EntMan.GetComponent<BadBloodSabotageTargetComponent>(site);
                var strike = Server.EntMan.GetComponent<BadBloodVesselStrikeComponent>(party.Rule!.Value);
                var now = Server.Timing.CurTime;
                Assert.Multiple(() =>
                {
                    Assert.That(target.AwaitingRepair, Is.False);
                    Assert.That(target.ImmuneUntil, Is.GreaterThan(now), "a repaired site is immune");
                    Assert.That(target.ImmuneUntil, Is.LessThanOrEqualTo(now + strike.RepairImmunity));
                    Assert.That(Server.System<BadBloodSabotageSystem>().TrySabotage(party.Grunts[0], site), Is.False,
                        "the repaired site cannot be sabotaged right away");
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
    public async Task ImmunityAlsoStopsPlainDamage()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        var spawned = new List<EntityUid>();

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var damageable = Server.System<DamageableSystem>();
                var site = BadBloodTestHelpers.SpawnUplinkSite(Server, BadBloodTestSiteKind.Comms,
                    map.GridCoords.Offset(new Vector2(1, 1)));
                spawned.Add(site);
                var target = entMan.EnsureComponent<BadBloodSabotageTargetComponent>(site);
                var blunt = new DamageSpecifier(Server.ProtoMan.Index<DamageTypePrototype>("Blunt"), FixedPoint2.New(450));

                target.ImmuneUntil = Server.Timing.CurTime + TimeSpan.FromSeconds(60);
                Assert.Multiple(() =>
                {
                    Assert.That(damageable.TryChangeDamage(site, blunt, ignoreResistances: true), Is.Null,
                        "damage to an immune site is cancelled");
                    Assert.That(entMan.GetComponent<CommunicationsTowerComponent>(site).State,
                        Is.EqualTo(CommunicationsTowerState.On), "an immune comms tower cannot be shot down");
                });

                target.ImmuneUntil = TimeSpan.Zero;
                damageable.TryChangeDamage(site, blunt, ignoreResistances: true);
                Assert.That(entMan.GetComponent<CommunicationsTowerComponent>(site).State,
                    Is.EqualTo(CommunicationsTowerState.Broken), "damage applies again once the immunity ran out");
            });
        }
        finally
        {
            await BadBloodTestHelpers.EndBadBloodRule(Pair, party);
            await Cleanup(spawned, party);
        }
    }

    [Test]
    public async Task ABadBloodHandToggleOfATowerDoesNothing()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        var spawned = new List<EntityUid>();

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var site = BadBloodTestHelpers.SpawnUplinkSite(Server, BadBloodTestSiteKind.Comms,
                    map.GridCoords.Offset(new Vector2(1, 1)));
                var sensor = BadBloodTestHelpers.SpawnUplinkSite(Server, BadBloodTestSiteKind.Sensor,
                    map.GridCoords.Offset(new Vector2(3, 1)));
                var marine = BadBloodTestHelpers.SpawnMarineNear(Server, map.GridCoords, new Vector2(3, 2));
                spawned.Add(site);
                spawned.Add(sensor);
                spawned.Add(marine);
                entMan.EnsureComponent<BadBloodSabotageTargetComponent>(site);
                entMan.EnsureComponent<BadBloodSabotageTargetComponent>(sensor);

                var clan = new InteractHandEvent(party.Grunts[0], site);
                entMan.EventBus.RaiseLocalEvent(site, clan);
                var clanSensor = new InteractHandEvent(party.Grunts[0], sensor);
                entMan.EventBus.RaiseLocalEvent(sensor, clanSensor);
                Assert.Multiple(() =>
                {
                    Assert.That(clan.Handled, Is.True, "the Bad Blood hand-toggle is taken before the tower sees it");
                    Assert.That(entMan.GetComponent<CommunicationsTowerComponent>(site).State,
                        Is.EqualTo(CommunicationsTowerState.On), "the comms tower stays On");
                    Assert.That(clanSensor.Handled, Is.True, "the same for a sensor tower");
                    Assert.That(entMan.GetComponent<SensorTowerComponent>(sensor).State,
                        Is.EqualTo(SensorTowerState.On), "the sensor tower stays On");
                });

                // a trained marine still switches the sensor tower by hand
                var tower = entMan.GetComponent<SensorTowerComponent>(sensor);
                Server.System<SkillsSystem>().SetSkill(marine, tower.Skill, tower.SkillLevel);
                var human = new InteractHandEvent(marine, sensor);
                entMan.EventBus.RaiseLocalEvent(sensor, human);
                Assert.That(tower.State, Is.EqualTo(SensorTowerState.Off), "a marine's hand-toggle still works");
            });
        }
        finally
        {
            await BadBloodTestHelpers.EndBadBloodRule(Pair, party);
            await Cleanup(spawned, party);
        }
    }

    private List<AlternativeVerb> SabotageVerbs(EntityUid user, EntityUid site)
    {
        var verbs = new GetVerbsEvent<AlternativeVerb>(user, site, null, null, true, true, true, new());
        Server.EntMan.EventBus.RaiseLocalEvent(site, verbs);
        var text = Loc.GetString("cmu-bad-blood-sabotage-verb");
        return verbs.Verbs.Where(verb => verb.Text == text).ToList();
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
