using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Pair;
using Content.Server.CMU14.Yautja.BadBlood;
using Content.Shared._RMC14.Intel;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Destructible;
using Content.Shared.FixedPoint;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Nutrition.Prototypes;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// FLOW-118: Clan Totems are placed apart, pay the clan while consecrated, and give the Lodge away when they fall
[TestFixture]
public sealed class ClanTotemTest : GameTest
{
    private const string Lodge = "CMUBadBloodCamp";
    private const string Totem = "CMUBadBloodTotem";
    private const string Human = "CMMobHuman";
    private const string Skull = "CMUYautjaHumanSkullTrophy";

    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task TotemsKeepTheirDistanceAndTheClanItsLimit()
    {
        var map = await Pair.CreateTestMap();
        await LayFloor(map, 0, 5, 25, 50, 60, 75, 100);
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        var grunt = party.Grunts[0];
        var spawned = new List<EntityUid>();

        try
        {
            await Server.WaitPost(() => spawned.Add(Server.EntMan.SpawnEntity(Lodge, map.GridCoords)));

            // every Bad Blood gets the Raise Totem action once the rule runs
            await Pair.RunSeconds(2);

            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var rules = Server.System<BadBloodCampRuleSystem>();
                var totems = Server.System<BadBloodTotemSystem>();
                Assert.That(rules.TryGetRule(out var rule), Is.True);

                Assert.That(entMan.TryGetComponent(grunt, out BadBloodTotemBuilderComponent? builder), Is.True);
                Assert.That(builder!.RaiseTotemActionEntity, Is.Not.Null, "every Bad Blood holds the Raise Totem action");
                Assert.That(rule.Comp.FreeTotems, Is.EqualTo(1), "the first Lodge grants one free Totem");
                Assert.That(rules.GetTotemLimit(), Is.EqualTo(3));

                Assert.That(Raise(totems, map, grunt, 5, spawned), Is.False, "a Totem keeps 20 tiles from the Lodge");

                Assert.That(Raise(totems, map, grunt, 25, spawned), Is.True, "the first Totem is free");
                Assert.Multiple(() =>
                {
                    Assert.That(rule.Comp.FreeTotems, Is.Zero);
                    Assert.That(rules.GetSp(), Is.EqualTo(FixedPoint4.Zero));
                    Assert.That(rules.GetTotems(), Has.Count.EqualTo(1));
                });

                Assert.That(Raise(totems, map, grunt, 50, spawned), Is.False, "the second Totem costs SP the clan does not have");
                Assert.That(rules.GetTotems(), Has.Count.EqualTo(1), "a Totem that is not paid for is not left standing");

                Assert.That(rules.AddSp(10), Is.True);
                Assert.That(Raise(totems, map, grunt, 50, spawned), Is.True);
                Assert.That(rule.Comp.TotemCost, Is.EqualTo(FixedPoint4.New(3)));
                Assert.That(rules.GetSp(), Is.EqualTo(FixedPoint4.New(10) - rule.Comp.TotemCost), "a Totem costs 3 SP");

                Assert.That(Raise(totems, map, grunt, 60, spawned), Is.False, "a Totem keeps 20 tiles from another Totem");

                Assert.That(Raise(totems, map, grunt, 75, spawned), Is.True);
                Assert.That(rules.GetSp(), Is.EqualTo(FixedPoint4.New(4)));

                Assert.That(Raise(totems, map, grunt, 100, spawned), Is.False, "the clan holds three Totems at most");
                Assert.Multiple(() =>
                {
                    Assert.That(rules.GetTotems(), Has.Count.EqualTo(3));
                    Assert.That(rules.GetSp(), Is.EqualTo(FixedPoint4.New(4)), "a refused Totem costs nothing");
                });

                // only a Bad Blood raises a Totem
                Assert.That(totems.TryStartRaisingTotem(spawned[0]), Is.False);
            });
        }
        finally
        {
            await Cleanup(party, spawned);
        }
    }

    // FLOW-607: a marine alone in the contest radius pauses income and ripening, dims the light and tells both sides.
    // A Bad Blood stepping in breaks the contest. Standing near it never desecrates it
    [Test]
    public async Task IncomeRipensCountsHalfTowardsTiersAndAMarineAlonePausesIt()
    {
        var map = await Pair.CreateTestMap();
        await LayFloor(map, 0, 30, 31, 33);
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        var grunt = party.Grunts[0];
        var spawned = new List<EntityUid>();
        EntityUid totem = default;
        EntityUid marine = default;
        FixedPoint4 before = default;
        var popups = 0;
        var announcements = 0;
        var broken = 0;
        var ended = 0;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var rules = Server.System<BadBloodCampRuleSystem>();
                var totems = Server.System<BadBloodTotemSystem>();
                var now = Server.ResolveDependency<IGameTiming>().CurTime;
                Assert.That(rules.TryGetRule(out var rule), Is.True);

                totem = entMan.SpawnEntity(Totem, map.GridCoords.Offset(new Vector2(30, 0)));
                spawned.Add(totem);
                var comp = entMan.GetComponent<BadBloodTotemComponent>(totem);
                var ent = new Entity<BadBloodTotemComponent>(totem, comp);
                Assert.That(comp.Registered, Is.True, "a Totem raised while the rule runs is the clan's");
                Assert.That(comp.ContestAnnounceCooldown, Is.EqualTo(TimeSpan.FromSeconds(60)), "the YAML default");
                AssertLight(totem, comp.LightEnergy, "a working Totem burns bright");

                // ripening by uncontested standing time: 0.2, then 0.4 after 10 minutes, then 0.7 after 20
                Assert.That(totems.GetIncomePerMinute(comp), Is.EqualTo(0.2f).Within(0.0001f));
                comp.StoodFor = TimeSpan.FromMinutes(11);
                Assert.Multiple(() =>
                {
                    Assert.That(totems.GetIncomePerMinute(comp), Is.EqualTo(0.4f).Within(0.0001f));
                    Assert.That(totems.IsRipe(totem), Is.False);
                });

                comp.StoodFor = TimeSpan.FromMinutes(21);
                Assert.Multiple(() =>
                {
                    Assert.That(totems.GetIncomePerMinute(comp), Is.EqualTo(0.7f).Within(0.0001f));
                    Assert.That(totems.IsRipe(totem), Is.True);
                    Assert.That(totems.CountRipeTotems(), Is.EqualTo(1));
                });

                // ten minutes of a ripe Totem: 7 SP to spend, half of it towards the armory tiers
                var sp = rules.GetSp();
                var lifetime = rules.GetLifetimeSp();
                totems.Accrue(ent, now, TimeSpan.FromMinutes(10));
                Assert.Multiple(() =>
                {
                    Assert.That((rules.GetSp() - sp).Double(), Is.EqualTo(7.0).Within(0.011));
                    Assert.That((rules.GetLifetimeSp() - lifetime).Double(), Is.EqualTo(3.5).Within(0.011),
                        "Totem income counts 50% towards tiers");
                });

                // a marine walks up with no Bad Blood near: the Totem is contested
                marine = entMan.SpawnEntity(Human, map.GridCoords.Offset(new Vector2(33, 0)));
                spawned.Add(marine);
                entMan.EnsureComponent<MarineComponent>(marine);
                before = rules.GetSp();
                popups = totems.ContestPopupsSent;
                announcements = totems.ContestAnnouncementsSent;
            });

            await Pair.RunSeconds(30);
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var totems = Server.System<BadBloodTotemSystem>();
                var comp = entMan.GetComponent<BadBloodTotemComponent>(totem);
                Assert.Multiple(() =>
                {
                    Assert.That(comp.Contested, Is.True, "a marine alone in range contests the Totem");
                    Assert.That(Server.System<BadBloodCampRuleSystem>().GetSp(), Is.EqualTo(before),
                        "a contested Totem pays nothing");
                    Assert.That(comp.StoodFor, Is.EqualTo(TimeSpan.FromMinutes(21)), "and does not ripen");
                    Assert.That(totems.IsRipe(totem), Is.True, "a contest freezes ripeness, it does not undo it");
                    Assert.That(comp.Desecrated, Is.False, "a marine standing beside it does not desecrate it without the verb");
                    Assert.That(comp.ContestWarned, Does.Contain(marine));
                    Assert.That(totems.ContestPopupsSent, Is.EqualTo(popups + 1), "the marine hears it once per contest");
                    Assert.That(totems.ContestAnnouncementsSent, Is.EqualTo(announcements + 1), "the clan is told once");
                    Assert.That(totems.GetTotemExamineText((totem, comp)),
                        Does.Contain(Loc.GetString("cmu-bad-blood-totem-examine-contested")), "a Bad Blood reads the contest");
                });
                AssertLight(totem, comp.ContestedLightEnergy, "a contested Totem's light is dim");

                // a Bad Blood steps in and breaks the contest
                broken = totems.ContestBrokenPopupsSent;
                ended = totems.ContestEndedAnnouncementsSent;
                Server.System<SharedTransformSystem>().SetCoordinates(grunt, map.GridCoords.Offset(new Vector2(31, 0)));
            });

            await Pair.RunSeconds(5);
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var totems = Server.System<BadBloodTotemSystem>();
                var comp = entMan.GetComponent<BadBloodTotemComponent>(totem);
                Assert.Multiple(() =>
                {
                    Assert.That(comp.Contested, Is.False, "a Bad Blood at its Totem keeps it working");
                    Assert.That(Server.System<BadBloodCampRuleSystem>().GetSp(), Is.GreaterThan(before), "income resumes");
                    Assert.That(comp.StoodFor, Is.GreaterThan(TimeSpan.FromMinutes(21)), "and so does ripening");
                    Assert.That(totems.ContestBrokenPopupsSent, Is.EqualTo(broken + 1), "the marine hears the hum come back");
                    Assert.That(totems.ContestEndedAnnouncementsSent, Is.EqualTo(ended + 1), "the clan hears it feeds them again");
                });
                AssertLight(totem, comp.LightEnergy, "the light is bright again");

                // the Bad Blood walks off and the marine holds it again within the minute
                Server.System<SharedTransformSystem>().SetCoordinates(grunt, map.GridCoords);
            });

            await Pair.RunSeconds(3);
            await Server.WaitAssertion(() =>
            {
                var totems = Server.System<BadBloodTotemSystem>();
                Assert.Multiple(() =>
                {
                    Assert.That(Server.EntMan.GetComponent<BadBloodTotemComponent>(totem).Contested, Is.True);
                    Assert.That(totems.ContestAnnouncementsSent, Is.EqualTo(announcements + 1),
                        "a second contest within 60 seconds is not announced to the clan again");
                    Assert.That(totems.ContestPopupsSent, Is.EqualTo(popups + 2), "the marine is told of the new contest");
                    Assert.That(Server.EntMan.GetComponent<BadBloodTotemComponent>(totem).Desecrated, Is.False);
                });
            });
        }
        finally
        {
            await Cleanup(party, spawned);
        }
    }

    // STATE-302: three ripe Totems the marines leave alone out-earn hunting. 0.7 SP a minute each is 21 SP in ten
    // minutes, and a desecrated one pays nothing
    [Test]
    public async Task ThreeRipeTotemsPayTwentyOneSpInTenMinutesAndADesecratedOnePaysNothing()
    {
        var map = await Pair.CreateTestMap();
        await LayFloor(map, 0, 30, 60, 90);
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        var spawned = new List<EntityUid>();
        var raised = new List<EntityUid>();
        var paidBefore = new Dictionary<EntityUid, FixedPoint4>();

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var rules = Server.System<BadBloodCampRuleSystem>();
                var totems = Server.System<BadBloodTotemSystem>();
                var now = Server.ResolveDependency<IGameTiming>().CurTime;
                Assert.That(rules.TryGetRule(out _), Is.True);

                foreach (var column in new[] { 30, 60, 90 })
                {
                    var totem = entMan.SpawnEntity(Totem, map.GridCoords.Offset(new Vector2(column, 0)));
                    spawned.Add(totem);
                    raised.Add(totem);
                    entMan.GetComponent<BadBloodTotemComponent>(totem).StoodFor = TimeSpan.FromMinutes(21);
                }

                Assert.That(totems.CountRipeTotems(), Is.EqualTo(3));

                // ten minutes of the update loop's own bookkeeping
                var sp = rules.GetSp();
                var lifetime = rules.GetLifetimeSp();
                foreach (var totem in raised)
                {
                    totems.Accrue(new Entity<BadBloodTotemComponent>(totem, entMan.GetComponent<BadBloodTotemComponent>(totem)),
                        now, TimeSpan.FromMinutes(10));
                }

                Assert.Multiple(() =>
                {
                    Assert.That((rules.GetSp() - sp).Double(), Is.EqualTo(21.0).Within(0.05),
                        "three ripe Totems pay 21 SP in ten minutes");
                    Assert.That((rules.GetLifetimeSp() - lifetime).Double(), Is.EqualTo(10.5).Within(0.05),
                        "and half of it counts towards the armory tiers");
                });

                totems.SetDesecrated(new Entity<BadBloodTotemComponent>(raised[0], entMan.GetComponent<BadBloodTotemComponent>(raised[0])), true);
                foreach (var totem in raised)
                {
                    paidBefore[totem] = entMan.GetComponent<BadBloodTotemComponent>(totem).PaidIncome;
                }
            });

            await Pair.RunSeconds(15);
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                Assert.Multiple(() =>
                {
                    Assert.That(entMan.GetComponent<BadBloodTotemComponent>(raised[0]).PaidIncome, Is.EqualTo(paidBefore[raised[0]]),
                        "a desecrated Totem pays nothing");
                    Assert.That(entMan.GetComponent<BadBloodTotemComponent>(raised[1]).PaidIncome, Is.GreaterThan(paidBefore[raised[1]]),
                        "the consecrated Totems keep paying");
                    Assert.That(entMan.GetComponent<BadBloodTotemComponent>(raised[2]).PaidIncome, Is.GreaterThan(paidBefore[raised[2]]));
                });
            });
        }
        finally
        {
            await Cleanup(party, spawned);
        }
    }

    [Test]
    public async Task AuraHalvesTheDrainAndSpeedsTheBracer()
    {
        var map = await Pair.CreateTestMap();
        await LayFloor(map, 0, 30, 32, 90);
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        var grunt = party.Grunts[0];
        var spawned = new List<EntityUid>();
        var baseHunger = 0f;
        var baseThirst = 0f;
        var baseRegen = 0f;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                Refresh(entMan, grunt);
                baseHunger = Rate(entMan, grunt, SatiationSystem.Hunger);
                baseThirst = Rate(entMan, grunt, SatiationSystem.Thirst);
                baseRegen = RegenMultiplier(entMan, grunt);
                Assert.That(baseHunger, Is.LessThan(0f), "a Bad Blood gets hungry");
                Assert.That(baseThirst, Is.LessThan(0f));

                spawned.Add(entMan.SpawnEntity(Totem, map.GridCoords.Offset(new Vector2(30, 0))));
                Server.System<SharedTransformSystem>().SetCoordinates(grunt, map.GridCoords.Offset(new Vector2(32, 0)));
            });

            await Pair.RunSeconds(3);
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                Assert.That(entMan.HasComponent<BadBloodTotemAuraComponent>(grunt), Is.True, "the grunt stands in the aura");
                Assert.That(entMan.HasComponent<BadBloodTotemAuraComponent>(party.Leader), Is.False, "the Leader does not");
                Assert.Multiple(() =>
                {
                    Assert.That(Rate(entMan, grunt, SatiationSystem.Hunger), Is.EqualTo(baseHunger * 0.5f).Within(0.0001f),
                        "hunger drains at half speed in the aura");
                    Assert.That(Rate(entMan, grunt, SatiationSystem.Thirst), Is.EqualTo(baseThirst * 0.5f).Within(0.0001f));
                    Assert.That(RegenMultiplier(entMan, grunt), Is.EqualTo(baseRegen * 1.5f).Within(0.0001f),
                        "the bracer regenerates half again as fast in the aura");
                });

                Server.System<SharedTransformSystem>().SetCoordinates(grunt, map.GridCoords.Offset(new Vector2(90, 0)));
            });

            await Pair.RunSeconds(3);
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                Assert.That(entMan.HasComponent<BadBloodTotemAuraComponent>(grunt), Is.False, "the aura ends at its edge");
                Assert.Multiple(() =>
                {
                    Assert.That(Rate(entMan, grunt, SatiationSystem.Hunger), Is.EqualTo(baseHunger).Within(0.0001f));
                    Assert.That(RegenMultiplier(entMan, grunt), Is.EqualTo(baseRegen).Within(0.0001f));
                });
            });
        }
        finally
        {
            await Cleanup(party, spawned);
        }
    }

    [Test]
    public async Task TotemShowsOnTheMapAfterItsDelayAndItsFallRevealsTheLodge()
    {
        var map = await Pair.CreateTestMap();
        await LayFloor(map, 0, 30, 60);
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        var spawned = new List<EntityUid>();
        EntityUid totem = default;
        EntityUid other = default;
        FixedPoint2 intelBefore = default;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var rules = Server.System<BadBloodCampRuleSystem>();
                var now = Server.ResolveDependency<IGameTiming>().CurTime;

                spawned.Add(entMan.SpawnEntity(Lodge, map.GridCoords));
                totem = entMan.SpawnEntity(Totem, map.GridCoords.Offset(new Vector2(30, 0)));
                other = entMan.SpawnEntity(Totem, map.GridCoords.Offset(new Vector2(60, 0)));
                spawned.Add(totem);
                spawned.Add(other);
                Assert.That(rules.GetTotems(), Has.Count.EqualTo(2));

                Assert.That(entMan.HasComponent<TacticalMapAlwaysVisibleComponent>(totem), Is.False,
                    "a fresh Totem is not on the marine map");

                // the first has stood for its 120 seconds, the second has not. FLOW-604: the clock also waits for
                // the landing, so the marines landed long ago here
                entMan.GetComponent<BadBloodTotemComponent>(totem).PlacedAt = now - TimeSpan.FromSeconds(121);
                Assert.That(rules.TryGetRule(out var rule), Is.True);
                rule.Comp.MarinesLandedAt = now - TimeSpan.FromSeconds(121);
            });

            await Pair.RunSeconds(3);
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var rules = Server.System<BadBloodCampRuleSystem>();
                var camp = Server.System<BadBloodCampSystem>();
                var intel = Server.System<IntelSystem>();

                Assert.That(entMan.TryGetComponent(totem, out TacticalMapAlwaysVisibleComponent? visible), Is.True,
                    "the Totem shows on the marine map after 120 seconds");
                Assert.That(visible!.VisibleToMarines && visible.VisibleToGovfor, Is.True);
                Assert.That(entMan.HasComponent<TacticalMapAlwaysVisibleComponent>(other), Is.False);

                intelBefore = intel.EnsureTechTree("govfor").Comp.Tree.Points;
                var skulls = Count(entMan, Skull);

                // a Totem that is merely deleted gives nothing away
                entMan.DeleteEntity(other);
                Assert.Multiple(() =>
                {
                    Assert.That(rules.GetTotems(), Has.Count.EqualTo(1));
                    Assert.That(camp.IsLodgeRevealed(), Is.False, "a deleted Totem does not reveal the Lodge");
                    Assert.That(intel.EnsureTechTree("govfor").Comp.Tree.Points, Is.EqualTo(intelBefore));
                    Assert.That(Count(entMan, Skull), Is.EqualTo(skulls));
                });

                Assert.That(Server.System<SharedDestructibleSystem>().DestroyEntity(totem), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(rules.GetTotems(), Is.Empty, "a destroyed Totem leaves the clan's list");
                    Assert.That(camp.IsLodgeRevealed(), Is.True, "a destroyed Totem reveals the Lodge");
                    Assert.That(intel.EnsureTechTree("govfor").Comp.Tree.Points - intelBefore, Is.EqualTo(FixedPoint2.New(0.7)),
                        "a destroyed Totem is worth 0.7 intel");
                    Assert.That(Count(entMan, Skull), Is.EqualTo(skulls + 1), "it drops one skull");
                });
            });
        }
        finally
        {
            await Cleanup(party, spawned);
        }
    }

    // FLOW-604: before the drop a raised Totem tells the marines nothing and never pins, however old it is. The landing
    // sends one sensor sweep, and the pin follows 120 seconds after the landing
    [Test]
    public async Task MarinesLearnOfTotemsOnlyOnceTheyLand()
    {
        var map = await Pair.CreateTestMap();
        await LayFloor(map, 0, 25);
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1, startLanded: false);
        var grunt = party.Grunts[0];
        var spawned = new List<EntityUid>();
        EntityUid totem = default;
        var intelBefore = 0;

        try
        {
            await Server.WaitPost(() => spawned.Add(Server.EntMan.SpawnEntity(Lodge, map.GridCoords)));

            // every Bad Blood gets the Raise Totem action once the rule runs
            await Pair.RunSeconds(2);

            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                var totems = Server.System<BadBloodTotemSystem>();
                Assert.That(rules.HaveMarinesLanded(), Is.False, "the marines are still in orbit");

                intelBefore = totems.MarineTotemIntelSent;
                Assert.That(Raise(totems, map, grunt, 25, spawned), Is.True);
                totem = spawned[^1];
                Assert.That(totems.MarineTotemIntelSent, Is.EqualTo(intelBefore), "marines in orbit hear of no raise");

                // it has stood well past its 120 seconds
                var now = Server.ResolveDependency<IGameTiming>().CurTime;
                Server.EntMan.GetComponent<BadBloodTotemComponent>(totem).PlacedAt = now - TimeSpan.FromSeconds(130);
            });

            await Pair.RunSeconds(3);
            await Server.WaitAssertion(() =>
            {
                var totems = Server.System<BadBloodTotemSystem>();
                Assert.That(Server.EntMan.HasComponent<TacticalMapAlwaysVisibleComponent>(totem), Is.False,
                    "no pin before the marines land");

                Assert.That(Server.System<BadBloodCampRuleSystem>().MarkMarinesLanded(), Is.True);
                Assert.That(totems.MarineTotemIntelSent, Is.EqualTo(intelBefore + 1), "the landing sends one sensor sweep");
            });

            await Pair.RunSeconds(3);
            await Server.WaitAssertion(() =>
            {
                Assert.That(Server.EntMan.HasComponent<TacticalMapAlwaysVisibleComponent>(totem), Is.False,
                    "the pin waits 120 seconds from the landing, not from the raise");

                // the landing was 121 seconds ago
                var now = Server.ResolveDependency<IGameTiming>().CurTime;
                Assert.That(Server.System<BadBloodCampRuleSystem>().TryGetRule(out var rule), Is.True);
                rule.Comp.MarinesLandedAt = now - TimeSpan.FromSeconds(121);
            });

            await Pair.RunSeconds(3);
            await Server.WaitAssertion(() =>
            {
                Assert.That(Server.EntMan.HasComponent<TacticalMapAlwaysVisibleComponent>(totem), Is.True,
                    "the Totem pins 120 seconds after the landing");
                Assert.That(Server.System<BadBloodTotemSystem>().MarineTotemIntelSent, Is.EqualTo(intelBefore + 1),
                    "the sweep is sent once");
            });
        }
        finally
        {
            await Cleanup(party, spawned);
        }
    }

    // FLOW-503: a marine desecrates a Totem with the Desecrate verb and a 45 s do-after: not ripe, no income, no
    // aura. A Bad Blood takes it back with Reconsecrate the same way. Standing near it only contests it (FLOW-607)
    [Test]
    public async Task AMarineDesecratesWithTheVerbAndABadBloodReconsecratesIt()
    {
        var map = await Pair.CreateTestMap();
        await LayFloor(map, 0, 29, 30, 31, 33);
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        var grunt = party.Grunts[0];
        var spawned = new List<EntityUid>();
        EntityUid totem = default;
        EntityUid marine = default;
        FixedPoint4 paid = default;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                Server.System<BadBloodTotemProbeSystem>().Clear();

                totem = entMan.SpawnEntity(Totem, map.GridCoords.Offset(new Vector2(30, 0)));
                spawned.Add(totem);
                BadBloodTestHelpers.ForceTotemRipe(Server, totem);

                var comp = entMan.GetComponent<BadBloodTotemComponent>(totem);
                Assert.Multiple(() =>
                {
                    Assert.That(comp.DesecrateTime, Is.EqualTo(TimeSpan.FromSeconds(45)));
                    Assert.That(comp.ReconsecrateTime, Is.EqualTo(TimeSpan.FromSeconds(45)));
                    Assert.That(Server.System<BadBloodTotemSystem>().IsRipe(totem), Is.True);
                });

                // a marine idling three tiles away first, the old desecration range
                marine = BadBloodTestHelpers.SpawnMarineNear(Server, map.GridCoords, new Vector2(33, 0));
                spawned.Add(marine);
            });

            await Pair.RunSeconds(50);
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var totems = Server.System<BadBloodTotemSystem>();
                var ent = new Entity<BadBloodTotemComponent>(totem, entMan.GetComponent<BadBloodTotemComponent>(totem));
                Assert.That(ent.Comp.Desecrated, Is.False, "standing near a Totem no longer desecrates it");
                Assert.That(ent.Comp.Contested, Is.True, "it only contests it");

                Assert.Multiple(() =>
                {
                    Assert.That(totems.GetConsecrationDenial(grunt, ent, true), Is.Not.Null, "a Bad Blood cannot desecrate");
                    Assert.That(totems.GetConsecrationDenial(marine, ent, false), Is.Not.Null, "nor can a marine reconsecrate");
                    Assert.That(totems.GetConsecrationDenial(grunt, ent, false), Is.Not.Null, "a consecrated Totem needs no reconsecration");
                });

                Server.System<SharedTransformSystem>().SetCoordinates(marine, map.GridCoords.Offset(new Vector2(31, 0)));
                Assert.That(totems.TryStartConsecration(marine, totem, true), Is.True, "a marine beside it can start desecrating");
            });

            await Pair.RunSeconds(30);
            await Server.WaitAssertion(() =>
            {
                Assert.That(Server.EntMan.GetComponent<BadBloodTotemComponent>(totem).Desecrated, Is.False, "30 seconds is not enough");
            });

            await Pair.RunSeconds(17);
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var rules = Server.System<BadBloodCampRuleSystem>();
                var totems = Server.System<BadBloodTotemSystem>();
                var comp = entMan.GetComponent<BadBloodTotemComponent>(totem);
                Assert.Multiple(() =>
                {
                    Assert.That(comp.Desecrated, Is.True, "the 45 second do-after desecrates the Totem");
                    Assert.That(totems.IsRipe(totem), Is.False, "a desecrated Totem is not ripe");
                    Assert.That(totems.CountRipeTotems(), Is.Zero);
                    Assert.That(Server.System<BadBloodTotemProbeSystem>().Desecrated, Does.Contain(totem));
                    Assert.That(comp.Contested, Is.False, "a desecrated Totem is not contested");
                });
                AssertLight(totem, null, "a desecrated Totem's light is out");

                paid = comp.PaidIncome;
                var sp = rules.GetSp();
                totems.Accrue(new Entity<BadBloodTotemComponent>(totem, comp), Server.Timing.CurTime, TimeSpan.FromMinutes(10));
                Assert.That(rules.GetSp(), Is.EqualTo(sp), "a desecrated Totem books no income");

                entMan.DeleteEntity(marine);
                Server.System<SharedTransformSystem>().SetCoordinates(grunt, map.GridCoords.Offset(new Vector2(29, 0)));
            });

            await Pair.RunSeconds(4);
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var comp = entMan.GetComponent<BadBloodTotemComponent>(totem);
                Assert.Multiple(() =>
                {
                    Assert.That(comp.Desecrated, Is.True, "a Bad Blood beside it does not reconsecrate it without the verb");
                    Assert.That(comp.PaidIncome, Is.EqualTo(paid), "a desecrated Totem pays nothing");
                    Assert.That(entMan.HasComponent<BadBloodTotemAuraComponent>(grunt), Is.False,
                        "a desecrated Totem has no aura");
                });

                Assert.That(Server.System<BadBloodTotemSystem>().TryStartConsecration(grunt, totem, false), Is.True);
            });

            await Pair.RunSeconds(47);
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var comp = entMan.GetComponent<BadBloodTotemComponent>(totem);
                Assert.Multiple(() =>
                {
                    Assert.That(comp.Desecrated, Is.False, "the 45 second do-after reconsecrates it");
                    Assert.That(Server.System<BadBloodTotemSystem>().IsRipe(totem), Is.True);
                    Assert.That(entMan.HasComponent<BadBloodTotemAuraComponent>(grunt), Is.True, "the aura is back");
                    Assert.That(Server.System<BadBloodTotemProbeSystem>().Reconsecrated, Does.Contain(totem));
                });
            });
        }
        finally
        {
            await Cleanup(party, spawned);
        }
    }

    // a Bad Blood standing beside the Totem no longer pauses anything: the marine is stopped by being moved or hurt
    [Test]
    public async Task ABadBloodNearbyDoesNotPauseTheDesecrationButMovingBreaksIt()
    {
        var map = await Pair.CreateTestMap();
        await LayFloor(map, 0, 29, 30, 31, 32);
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        var grunt = party.Grunts[0];
        var spawned = new List<EntityUid>();
        EntityUid totem = default;
        EntityUid marine = default;

        try
        {
            await Server.WaitAssertion(() =>
            {
                totem = Server.EntMan.SpawnEntity(Totem, map.GridCoords.Offset(new Vector2(30, 0)));
                spawned.Add(totem);
                Server.EntMan.GetComponent<BadBloodTotemComponent>(totem).DesecrateTime = TimeSpan.FromSeconds(6);
                marine = BadBloodTestHelpers.SpawnMarineNear(Server, map.GridCoords, new Vector2(31, 0));
                spawned.Add(marine);
                Server.System<SharedTransformSystem>().SetCoordinates(grunt, map.GridCoords.Offset(new Vector2(29, 0)));

                Assert.That(Server.System<BadBloodTotemSystem>().TryStartConsecration(marine, totem, true), Is.True);
            });

            await Pair.RunSeconds(2);
            await Server.WaitAssertion(() =>
            {
                // stepping away breaks the do-after
                Server.System<SharedTransformSystem>().SetCoordinates(marine, map.GridCoords.Offset(new Vector2(32, 0)));
            });

            await Pair.RunSeconds(7);
            await Server.WaitAssertion(() =>
            {
                Assert.That(Server.EntMan.GetComponent<BadBloodTotemComponent>(totem).Desecrated, Is.False,
                    "moving off breaks the desecration");

                Server.System<SharedTransformSystem>().SetCoordinates(marine, map.GridCoords.Offset(new Vector2(31, 0)));
                Assert.That(Server.System<BadBloodTotemSystem>().TryStartConsecration(marine, totem, true), Is.True);
            });

            await Pair.RunSeconds(8);
            await Server.WaitAssertion(() =>
            {
                Assert.That(Server.EntMan.GetComponent<BadBloodTotemComponent>(totem).Desecrated, Is.True,
                    "a Bad Blood standing beside the Totem does not stop a marine who keeps still");
            });
        }
        finally
        {
            await Cleanup(party, spawned);
        }
    }

    // FLOW-503: a desecrated Totem is not ripe, so desecrating one of three ripe Totems breaks the ship-call hold
    [Test]
    public async Task DesecratingOneOfThreeRipeTotemsBreaksTheRipeHold()
    {
        var map = await Pair.CreateTestMap();
        await LayFloor(map, 0, 30, 31, 60, 90);
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        var spawned = new List<EntityUid>();
        var raised = new List<EntityUid>();

        try
        {
            await Server.WaitAssertion(() =>
            {
                foreach (var column in new[] { 30, 60, 90 })
                {
                    var totem = Server.EntMan.SpawnEntity(Totem, map.GridCoords.Offset(new Vector2(column, 0)));
                    spawned.Add(totem);
                    raised.Add(totem);
                    BadBloodTestHelpers.ForceTotemRipe(Server, totem);
                }

                Assert.That(Server.System<BadBloodTotemSystem>().CountRipeTotems(), Is.EqualTo(3));
            });

            await Pair.RunSeconds(2);
            await Server.WaitAssertion(() =>
            {
                Assert.That(Server.System<BadBloodCampRuleSystem>().TryGetRule(out var rule), Is.True);
                Assert.That(rule.Comp.RipeHoldStartedAt, Is.Not.Null, "three ripe Totems start the hold");

                Server.EntMan.GetComponent<BadBloodTotemComponent>(raised[0]).DesecrateTime = TimeSpan.FromSeconds(2);
                var marine = BadBloodTestHelpers.SpawnMarineNear(Server, map.GridCoords, new Vector2(31, 0));
                spawned.Add(marine);
                Assert.That(Server.System<BadBloodTotemSystem>().TryStartConsecration(marine, raised[0], true), Is.True);
            });

            await Pair.RunSeconds(4);
            await Server.WaitAssertion(() =>
            {
                Assert.That(Server.System<BadBloodCampRuleSystem>().TryGetRule(out var rule), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(Server.EntMan.GetComponent<BadBloodTotemComponent>(raised[0]).Desecrated, Is.True);
                    Assert.That(Server.System<BadBloodTotemSystem>().CountRipeTotems(), Is.EqualTo(2));
                    Assert.That(rule.Comp.RipeHoldStartedAt, Is.Null, "a desecrated Totem breaks the hold");
                });
            });
        }
        finally
        {
            await Cleanup(party, spawned);
        }
    }

    // the Totem is a structure and needs floor to stand on
    private async Task LayFloor(TestMapData map, params int[] columns)
    {
        await Server.WaitPost(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            foreach (var x in columns)
            {
                maps.SetTile(map.Grid, map.Tile.GridIndices + new Vector2i(x, 0), map.Tile.Tile);
            }
        });
    }

    // moves the builder to the column and raises a Totem there, skipping the build time
    private bool Raise(BadBloodTotemSystem totems, TestMapData map, EntityUid user, int column, List<EntityUid> spawned)
    {
        Server.System<SharedTransformSystem>().SetCoordinates(user, map.GridCoords.Offset(new Vector2(column, 0)));
        if (!totems.TryRaiseTotem(user, out var totem))
            return false;

        spawned.Add(totem);
        return true;
    }

    // FLOW-607: the Totem's light is on at this energy, or off for null. call on the server thread
    private void AssertLight(EntityUid totem, float? energy, string message)
    {
        Assert.That(Server.System<SharedPointLightSystem>().TryGetLight(totem, out var light), Is.True, "the Totem has a light");
        if (energy is not { } expected)
        {
            Assert.That(light!.Enabled, Is.False, message);
            return;
        }

        Assert.That(light!.Enabled, Is.True, message);
        Assert.That(light.Energy, Is.EqualTo(expected).Within(0.0001f), message);
    }

    private static void Refresh(IEntityManager entMan, EntityUid uid)
    {
        var ent = new Entity<SatiationComponent>(uid, entMan.GetComponent<SatiationComponent>(uid));
        var satiation = entMan.System<SatiationSystem>();
        satiation.ModifyValue(ent, SatiationSystem.Hunger, 0f);
        satiation.ModifyValue(ent, SatiationSystem.Thirst, 0f);
    }

    private static float Rate(IEntityManager entMan, EntityUid uid, ProtoId<SatiationTypePrototype> type)
    {
        var satiation = entMan.GetComponent<SatiationComponent>(uid).GetOrNull(type);
        Assert.That(satiation, Is.Not.Null);
        return satiation!.ActualChangeRate;
    }

    private static float RegenMultiplier(IEntityManager entMan, EntityUid user)
    {
        var ev = new YautjaBracerRegenModifyEvent(user);
        entMan.EventBus.RaiseLocalEvent(user, ref ev);
        return ev.Multiplier;
    }

    private static int Count(IEntityManager entMan, string prototype)
    {
        var count = 0;
        var query = entMan.AllEntityQueryEnumerator<MetaDataComponent>();
        while (query.MoveNext(out _, out var meta))
        {
            if (meta.EntityPrototype?.ID == prototype && meta.EntityLifeStage < EntityLifeStage.Terminating)
                count++;
        }

        return count;
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
