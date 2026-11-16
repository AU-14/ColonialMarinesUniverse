using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Yautja.BadBlood;
using Content.Shared._RMC14.Intel;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// FLOW-505 and FLOW-507: marine intel from the Bad Blood clan. A decrypted bracer data-core and the Lodge raid loot
[TestFixture]
public sealed class BadBloodIntelTest : GameTest
{
    private const string Lodge = "CMUBadBloodCamp";
    private const string Armory = "CMUBadBloodCampArmory";
    private const string Bracer = "CMUYautjaBadBloodBracer";
    private const string Trophy = "CMUYautjaHumanSkullTrophy";
    private static readonly EntProtoId<SkillDefinitionComponent> IntelSkill = "RMCSkillIntel";

    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task OnlyASkilledMarineDecryptsADroppedBracer()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        var spawned = new List<EntityUid>();
        EntityUid bracer = default;
        EntityUid marine = default;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var dataCore = Server.System<BadBloodBracerDataCoreSystem>();
                var skills = Server.System<SkillsSystem>();
                var inventory = Server.System<InventorySystem>();
                Server.System<BadBloodObjectiveProbeSystem>().Reset();

                spawned.Add(entMan.SpawnEntity(Lodge, map.GridCoords.Offset(new Vector2(6, 0))));

                var bracerAt = map.GridCoords.Offset(new Vector2(12, 0));
                bracer = entMan.SpawnEntity(Bracer, bracerAt);
                spawned.Add(bracer);
                var core = entMan.GetComponent<BadBloodBracerDataCoreComponent>(bracer);
                Assert.Multiple(() =>
                {
                    Assert.That(core.DecryptTime, Is.EqualTo(TimeSpan.FromSeconds(30)));
                    Assert.That(core.RevealTime, Is.EqualTo(TimeSpan.FromSeconds(60)));
                    Assert.That(core.SkillLevel, Is.EqualTo(1));
                    Assert.That(core.Decrypted, Is.False);
                });
                core.DecryptTime = TimeSpan.FromSeconds(1);

                marine = BadBloodTestHelpers.SpawnMarineNear(Server, bracerAt, Vector2.Zero);
                spawned.Add(marine);

                // no intel training
                skills.SetSkill(marine, IntelSkill, 0);
                Assert.That(dataCore.TryStartDecrypt(marine, bracer), Is.False, "an unskilled marine is refused");

                // a living Bad Blood still wears his
                var grunt = party.Grunts[0];
                if (inventory.TryUnequip(grunt, "gloves", out var old, silent: true, force: true))
                    entMan.DeleteEntity(old.Value);

                var worn = entMan.SpawnEntity(Bracer, map.GridCoords);
                spawned.Add(worn);
                Assert.That(inventory.TryEquip(grunt, worn, "gloves", silent: true, force: true), Is.True);

                skills.SetSkill(marine, IntelSkill, 1);
                Assert.That(dataCore.CanDecrypt(marine, worn, out var reason), Is.False);
                Assert.That(reason, Is.EqualTo("cmu-bad-blood-intel-decrypt-worn"));
                Assert.That(dataCore.CanDecrypt(party.Grunts[0], bracer, out _), Is.False, "a Bad Blood cannot decrypt");

                Assert.That(dataCore.TryStartDecrypt(marine, bracer), Is.True, "a skilled marine decrypts a dropped bracer");
            });

            await Pair.RunSeconds(2);

            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var probe = Server.System<BadBloodObjectiveProbeSystem>();
                Assert.That(Server.System<BadBloodCampSystem>().TryGetLodge(out var lodge), Is.True);
                var revealLeft = lodge.Comp.RevealedUntil - Server.Timing.CurTime;
                Assert.Multiple(() =>
                {
                    Assert.That(entMan.GetComponent<BadBloodBracerDataCoreComponent>(bracer).Decrypted, Is.True);
                    Assert.That(probe.BracerDecrypted, Is.EqualTo(1));
                    Assert.That(probe.LastDecryptUser, Is.EqualTo(marine));
                    Assert.That(probe.LastRevealSource, Is.EqualTo(BadBloodRevealSource.Intel));
                    Assert.That(probe.LastRevealDuration, Is.EqualTo(TimeSpan.FromSeconds(60)));
                    Assert.That(revealLeft, Is.GreaterThan(TimeSpan.FromSeconds(55)), "the Lodge shows for 60 s");
                    Assert.That(revealLeft, Is.LessThanOrEqualTo(TimeSpan.FromSeconds(60)));
                    Assert.That(Server.System<BadBloodCampSystem>().IsLodgeRevealed(), Is.True);

                    Assert.That(entMan.TryGetComponent<IntelRetrieveItemObjectiveComponent>(bracer, out var retrieve), Is.True,
                        "the decrypted bracer is retrieve intel");
                    Assert.That(retrieve!.State, Is.EqualTo(IntelObjectiveState.Active));
                    var retrieveValue = retrieve.Value; // read access only (RA0002)
                    Assert.That(retrieveValue.Float(), Is.EqualTo(1f).Within(0.001f));
                    Assert.That(entMan.HasComponent<ActiveIntelPositionComponent>(bracer), Is.True,
                        "carrying it into the intel area credits it");
                });

                Assert.That(Server.System<BadBloodBracerDataCoreSystem>().TryStartDecrypt(marine, bracer), Is.False,
                    "a data-core is decrypted once");
            });
        }
        finally
        {
            await Cleanup(party, spawned);
        }
    }

    [Test]
    public async Task ShipCallLeavesAStarChartAtTheArmoryAndItsFirstPickupCounts()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        var spawned = new List<EntityUid>();

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var hands = Server.System<SharedHandsSystem>();
                var probe = Server.System<BadBloodObjectiveProbeSystem>();
                probe.Reset();

                spawned.Add(entMan.SpawnEntity(Lodge, map.GridCoords.Offset(new Vector2(6, 0))));
                var armory = entMan.SpawnEntity(Armory, map.GridCoords.Offset(new Vector2(12, 0)));
                spawned.Add(armory);
                Assert.That(CountCharts(), Is.Zero);

                Assert.That(Server.System<BadBloodCampRuleSystem>().TryCallShip(), Is.True);
                Assert.That(probe.ShipCalled, Is.EqualTo(1));

                var found = FindChart();
                Assert.That(found, Is.Not.Null, "the ship call leaves a star chart");
                var chart = found!.Value;
                spawned.Add(chart);
                var xform = Server.System<SharedTransformSystem>();
                var distance = (xform.GetWorldPosition(chart) - xform.GetWorldPosition(armory)).Length();
                Assert.That(distance, Is.LessThan(0.5f), "at the Lodge armory");

                var chartAt = entMan.GetComponent<TransformComponent>(chart).Coordinates;
                var first = BadBloodTestHelpers.SpawnMarineNear(Server, chartAt, Vector2.Zero);
                var second = BadBloodTestHelpers.SpawnMarineNear(Server, chartAt, Vector2.Zero);
                spawned.Add(first);
                spawned.Add(second);
                Assert.That(probe.StarChartRecovered, Is.Zero, "lying at the armory it is not recovered yet");

                Assert.That(hands.TryPickupAnyHand(first, chart), Is.True);
                Assert.That(probe.StarChartRecovered, Is.EqualTo(1));
                Assert.That(probe.LastChartUser, Is.EqualTo(first));
                Assert.That(entMan.GetComponent<BadBloodStarChartComponent>(chart).Recovered, Is.True);

                Assert.That(hands.TryDrop(first, chart), Is.True);
                Assert.That(hands.TryPickupAnyHand(second, chart), Is.True);
                Assert.That(probe.StarChartRecovered, Is.EqualTo(1), "the recovery is counted once");

                Assert.That(entMan.GetComponent<IntelRetrieveItemObjectiveComponent>(chart).State,
                    Is.EqualTo(IntelObjectiveState.Active));
            });
        }
        finally
        {
            await Cleanup(party, spawned);
        }
    }

    [Test]
    public async Task StrippingTheRackTakesOneTrophy()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        var spawned = new List<EntityUid>();
        EntityUid lodge = default;
        EntityUid marine = default;
        BaseContainer rack = default!;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var containers = Server.System<SharedContainerSystem>();
                var lodgeIntel = Server.System<BadBloodLodgeIntelSystem>();
                Server.System<BadBloodObjectiveProbeSystem>().Reset();

                var lodgeAt = map.GridCoords.Offset(new Vector2(6, 0));
                lodge = entMan.SpawnEntity(Lodge, lodgeAt);
                spawned.Add(lodge);
                marine = BadBloodTestHelpers.SpawnMarineNear(Server, lodgeAt, Vector2.Zero);
                spawned.Add(marine);

                Assert.That(lodgeIntel.CanStripTrophy(marine, out var reason), Is.False);
                Assert.That(reason, Is.EqualTo("cmu-bad-blood-intel-strip-empty"));

                var offering = entMan.GetComponent<BadBloodCampOfferingComponent>(lodge);
                Assert.That(containers.TryGetContainer(lodge, offering.ContainerId, out var container), Is.True);
                rack = container!;
                for (var i = 0; i < 2; i++)
                {
                    var trophy = entMan.SpawnEntity(Trophy, lodgeAt);
                    spawned.Add(trophy);
                    Assert.That(containers.Insert(trophy, rack), Is.True);
                }

                Assert.That(lodgeIntel.CanStripTrophy(party.Grunts[0], out _), Is.False, "a Bad Blood does not strip");

                var raid = entMan.EnsureComponent<BadBloodLodgeRaidComponent>(lodge);
                Assert.That(raid.StripTime, Is.EqualTo(TimeSpan.FromSeconds(10)));
                raid.StripTime = TimeSpan.FromSeconds(1);

                Assert.That(lodgeIntel.TryStartStripTrophy(marine), Is.True);
            });

            await Pair.RunSeconds(2);

            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var probe = Server.System<BadBloodObjectiveProbeSystem>();
                Assert.That(probe.TrophyStripped, Is.EqualTo(1));
                var stripped = probe.LastStrippedTrophy!.Value;
                Assert.Multiple(() =>
                {
                    Assert.That(rack.ContainedEntities, Has.Count.EqualTo(1), "one trophy comes off the rack");
                    Assert.That(rack.Contains(stripped), Is.False);
                    Assert.That(Server.System<SharedContainerSystem>().IsEntityInContainer(stripped), Is.False,
                        "it is dropped at the Lodge");
                    Assert.That(probe.LastStripLodge, Is.EqualTo(lodge));
                    Assert.That(entMan.GetComponent<IntelRetrieveItemObjectiveComponent>(stripped).State,
                        Is.EqualTo(IntelObjectiveState.Active), "the trophy is retrieve intel");
                    Assert.That(entMan.HasComponent<ActiveIntelPositionComponent>(stripped), Is.True);
                });
            });
        }
        finally
        {
            await Cleanup(party, spawned);
        }
    }

    // call on the server thread
    private int CountCharts()
    {
        var count = 0;
        var query = Server.EntMan.EntityQueryEnumerator<BadBloodStarChartComponent>();
        while (query.MoveNext(out _, out _))
        {
            count++;
        }

        return count;
    }

    // call on the server thread
    private EntityUid? FindChart()
    {
        var query = Server.EntMan.EntityQueryEnumerator<BadBloodStarChartComponent>();
        return query.MoveNext(out var uid, out _) ? uid : null;
    }

    private async Task Cleanup(BadBloodTestParty party, List<EntityUid> spawned)
    {
        await Server.WaitPost(() =>
        {
            for (var i = spawned.Count - 1; i >= 0; i--)
            {
                if (Server.EntMan.EntityExists(spawned[i]))
                    Server.EntMan.DeleteEntity(spawned[i]);
            }
        });

        await BadBloodTestHelpers.EndBadBloodRule(Pair, party);
    }
}

// records the Bad Blood objective broadcasts (docs/GraphAudit-BadBlood-ObjectivesPlan.md ARCH-501) for the tests.
// global state on a pooled server: call Reset at the start of a test
public sealed class BadBloodObjectiveProbeSystem : EntitySystem
{
    public int HoldStarted;
    public int HoldBroken;
    public int ShipCalled;
    public int BracerDecrypted;
    public int StarChartRecovered;
    public int TrophyStripped;

    public TimeSpan? LastCallAt;
    public TimeSpan? LastArrivesAt;
    public EntityUid? LastDecryptUser;
    public EntityUid? LastChartUser;
    public EntityUid? LastStripLodge;
    public EntityUid? LastStrippedTrophy;
    public BadBloodRevealSource? LastRevealSource;
    public TimeSpan? LastRevealDuration;

    public override void Initialize()
    {
        SubscribeLocalEvent<BadBloodRipeHoldStartedEvent>(OnHoldStarted);
        SubscribeLocalEvent<BadBloodRipeHoldBrokenEvent>(OnHoldBroken);
        SubscribeLocalEvent<BadBloodShipCalledEvent>(OnShipCalled);
        SubscribeLocalEvent<BadBloodBracerDecryptedEvent>(OnBracerDecrypted);
        SubscribeLocalEvent<BadBloodStarChartRecoveredEvent>(OnStarChartRecovered);
        SubscribeLocalEvent<BadBloodTrophyStrippedEvent>(OnTrophyStripped);
        SubscribeLocalEvent<BadBloodLodgeRevealedEvent>(OnLodgeRevealed);
    }

    public void Reset()
    {
        HoldStarted = 0;
        HoldBroken = 0;
        ShipCalled = 0;
        BracerDecrypted = 0;
        StarChartRecovered = 0;
        TrophyStripped = 0;
        LastCallAt = null;
        LastArrivesAt = null;
        LastDecryptUser = null;
        LastChartUser = null;
        LastStripLodge = null;
        LastStrippedTrophy = null;
        LastRevealSource = null;
        LastRevealDuration = null;
    }

    private void OnHoldStarted(ref BadBloodRipeHoldStartedEvent ev)
    {
        HoldStarted++;
        LastCallAt = ev.CallAt;
    }

    private void OnHoldBroken(ref BadBloodRipeHoldBrokenEvent ev)
    {
        HoldBroken++;
    }

    private void OnShipCalled(ref BadBloodShipCalledEvent ev)
    {
        ShipCalled++;
        LastArrivesAt = ev.ArrivesAt;
    }

    private void OnBracerDecrypted(ref BadBloodBracerDecryptedEvent ev)
    {
        BracerDecrypted++;
        LastDecryptUser = ev.User;
    }

    private void OnStarChartRecovered(ref BadBloodStarChartRecoveredEvent ev)
    {
        StarChartRecovered++;
        LastChartUser = ev.User;
    }

    private void OnTrophyStripped(ref BadBloodTrophyStrippedEvent ev)
    {
        TrophyStripped++;
        LastStripLodge = ev.Lodge;
        LastStrippedTrophy = ev.Trophy;
    }

    private void OnLodgeRevealed(ref BadBloodLodgeRevealedEvent ev)
    {
        LastRevealSource = ev.Source;
        LastRevealDuration = ev.Duration;
    }
}
