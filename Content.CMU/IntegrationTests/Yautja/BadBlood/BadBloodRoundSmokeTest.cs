#pragma warning disable RA0002 // the test shortens timers and reads state straight off the components

using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Round;
using Content.Server.CMU14.Threats;
using Content.Server.CMU14.Yautja;
using Content.Server.CMU14.Yautja.BadBlood;
using Content.Server.GameTicking;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Ghost.Roles.Events;
using Content.Shared._RMC14.Intel;
using Content.Shared._RMC14.Rules;
using Content.Shared._RMC14.Vendors;
using Content.Shared.CMU14.Threats;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Destructible;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// TEST-102: the Bad Blood round from end to end. One test walks the Lodge loop up to the marine win, two walk the
// clan ship, one seats a forced spawn. Every timer is a component field and is shortened here. The round's
// decision is read off the rule entity: the pooled server runs the dummy ticker and has no round to end.
//
// Left out on purpose, each is covered by its own test:
//   - the butcher pipeline for the hide (30 s of do-afters): the soft trophy here is the scalp
//   - the marine objective payout for a destroyed Totem, which needs a running round (TotemObjectiveTest). The
//     intel payout and the Lodge reveal are asserted
//   - the threat starting its own win conditions: the forced spawn here starts the camp rule by hand, because
//     the threat's other win condition would try to end the round
[TestFixture]
public sealed class BadBloodRoundSmokeTest : GameTest
{
    private const string Lodge = "CMUBadBloodCamp";
    private const string Armory = "CMUBadBloodCampArmory";
    private const string Totem = "CMUBadBloodTotem";
    private const string Human = "CMMobHuman";
    private const string Rag = "CMUYautjaPolishingRag";
    private const string Gel = "CMUYautjaCleanserGelVial";
    private const string Combistick = "CMUYautjaCombistick";
    private const string Glaive = "CMUYautjaWarGlaive";
    private const string IntelTeam = "govfor";
    private const string BracerSlot = "gloves";

    // a Totem keeps 20 tiles from the Lodge. the Leader falls further out still, so the scuttle hurts nobody
    private const int TotemColumn = 25;
    private const int GraveColumn = 40;

    private static readonly ProtoId<ThreatPrototype> BadBloodThreat = "BadBloodClanThreat";

    // squad seats go to a player and the summoning cap is a share of the player count, so one has to be there
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task ClanRisesFromThePitchToTheVoteAndFallsWithItsLeaderAndLodge()
    {
        var map = await Pair.CreateTestMap();
        await LayGround(map);
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 2);
        var session = Pair.Player!;
        var leader = party.Leader;
        var grunt = party.Grunts[0];
        var hunter = party.Grunts[1];

        EntityUid kit = default;
        EntityUid lodge = default;
        EntityUid spit = default;
        EntityUid armory = default;
        EntityUid first = default;
        EntityUid second = default;
        EntityUid skull = default;
        EntityUid scalp = default;
        EntityUid oldBracer = default;
        EntityUid entry = default;
        EntityUid? arrival = null;
        var spAfterCall = FixedPoint4.Zero;
        var fuse = TimeSpan.Zero;

        try
        {
            // ---- the Leader pitches the Lodge ----
            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                var camp = Server.System<BadBloodCampSystem>();
                Assert.That(Server.System<GameTicker>().RunLevel, Is.Not.EqualTo(GameRunLevel.InRound),
                    "this test reads the rule's decision, it must not end a real round");
                Assert.That(rules.TryGetRule(out var rule), Is.True);

                // nobody drives the party's bodies. that must not count as a lost Leader half way through
                rule.Comp.LeaderSsdTimeout = TimeSpan.FromHours(1);

                Assert.Multiple(() =>
                {
                    Assert.That(rules.GetLeader(), Is.EqualTo(leader), "the rule crowns the landing Leader");
                    Assert.That(rules.HasLodge(), Is.False);
                    Assert.That(rules.InFoundingGrace(), Is.True);
                    Assert.That(rules.GetSp(), Is.EqualTo(FixedPoint4.Zero));
                    Assert.That(rules.GetTier(), Is.EqualTo(1));
                });

                Assert.That(camp.TryPitchCamp(hunter), Is.False, "Pitch Camp is the Leader's alone");
                Assert.That(camp.TryPitchCamp(leader), Is.True);
                Assert.That(rule.Comp.Kit, Is.Not.Null);
                kit = rule.Comp.Kit!.Value;
            });

            await Server.WaitPost(() =>
            {
                Server.System<SharedInteractionSystem>().UseInHandInteraction(leader, kit,
                    checkCanUse: false, checkCanInteract: false, checkUseDelay: false);
            });

            // the kit takes 10 seconds to deploy
            await Pair.RunSeconds(12);

            // ---- a kill, and its trophies are taken and prepared ----
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var rules = Server.System<BadBloodCampRuleSystem>();
                var camp = Server.System<BadBloodCampSystem>();
                var ascension = Server.System<BadBloodAscensionSystem>();
                var trophies = Server.System<YautjaTrophySystem>();
                var mobState = Server.System<MobStateSystem>();
                var xforms = Server.System<SharedTransformSystem>();
                Assert.That(rules.TryGetRule(out var rule), Is.True);

                Assert.That(rules.GetLodge(), Is.Not.Null, "the Leader raises the Lodge");
                lodge = rules.GetLodge()!.Value;
                Assert.That(camp.TryGetPart(BadBloodCampPartKind.Spit, out spit), Is.True, "the Lodge comes with its spit");
                Assert.That(camp.TryGetPart(BadBloodCampPartKind.Armory, out armory), Is.True, "and its armory");
                Assert.That(camp.TryGetPart(BadBloodCampPartKind.AscensionStone, out _), Is.True, "and its Ascension Stone");
                Assert.Multiple(() =>
                {
                    Assert.That(rule.Comp.FirstCampPitched, Is.True);
                    Assert.That(rule.Comp.KitOut, Is.False, "the kit is used up");
                    Assert.That(rule.Comp.FreeTotems, Is.EqualTo(1), "the first Lodge grants one free Totem");
                    Assert.That(rule.Comp.FoundingSquadOpened, Is.True, "the founding squad is offered once, to the reserve");
                    Assert.That(rule.Comp.SquadCalls, Is.Empty, "with nobody in the reserve no seat opens");
                    Assert.That(rules.InFoundingGrace(), Is.False, "founding grace ends with the first Lodge");
                    Assert.That(entMan.HasComponent<BadBloodCampOfferingComponent>(lodge), Is.True);
                    Assert.That(camp.IsLodgeRevealed(), Is.False, "the Lodge is hidden from the marines");
                });

                // two marines with players behind them fall to the same hunter
                first = SpawnPrey(map, 3);
                second = SpawnPrey(map, 4);
                mobState.ChangeMobState(first, MobState.Dead, origin: hunter);
                mobState.ChangeMobState(second, MobState.Dead, origin: hunter);
                Assert.Multiple(() =>
                {
                    Assert.That(ascension.GetKills(hunter), Is.EqualTo(2), "worthy kills are tallied for the killer");
                    Assert.That(ascension.GetKills(grunt), Is.Zero);
                });

                // a hard trophy and a soft one off the first body, on the ground beside whoever prepares them
                Assert.That(trophies.TryHarvestTrophy(hunter, first, YautjaTrophyKind.HumanSkull, out skull), Is.True);
                scalp = trophies.SpawnRuntimeScalp(first, hunter);
                xforms.DropNextTo(skull, hunter);
                xforms.DropNextTo(scalp, grunt);

                var origin = entMan.GetComponent<BadBloodTrophyOriginComponent>(skull);
                Assert.Multiple(() =>
                {
                    Assert.That(origin.Body, Is.EqualTo(first), "the trophy remembers its body");
                    Assert.That(origin.WasKill, Is.True);
                    Assert.That(origin.WasPlayer, Is.True);
                    Assert.That(entMan.GetComponent<BadBloodTrophyOriginComponent>(scalp).Body, Is.EqualTo(first));
                });

                Assert.That(camp.CanOfferTrophy(skull, out var raw), Is.False);
                Assert.That(raw, Is.EqualTo("cmu-bad-blood-offering-unprepared"), "a raw trophy is refused");

                Assert.That(UseOn(hunter, SpawnBeside(Rag, hunter), skull), Is.True, "the rag polishes the skull");
                Assert.That(UseOn(grunt, SpawnBeside(Gel, grunt), scalp), Is.True, "the gel cures the scalp");
            });

            await Pair.RunSeconds(6);

            // ---- the first trophy is offered through the real interaction ----
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var rules = Server.System<BadBloodCampRuleSystem>();
                var xforms = Server.System<SharedTransformSystem>();

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.GetComponent<YautjaTrophyComponent>(skull).Polished, Is.True);
                    Assert.That(entMan.GetComponent<MetaDataComponent>(skull).EntityName, Does.StartWith("polished "));
                    Assert.That(entMan.GetComponent<YautjaTrophyComponent>(scalp).Polished, Is.True);
                    Assert.That(entMan.GetComponent<MetaDataComponent>(scalp).EntityName, Does.StartWith("cured "));
                });

                // the hunter carries the skull to the Lodge: the offering needs the trophy within reach all the way
                xforms.SetCoordinates(hunter, SpotBeside(lodge));
                xforms.DropNextTo(skull, hunter);
                Assert.That(UseOn(hunter, skull, lodge), Is.True);
                Assert.That(rules.GetSp(), Is.EqualTo(FixedPoint4.Zero), "nothing is paid before the offering is finished");
            });

            await Pair.RunSeconds(4);

            // ---- the second is offered, and the body goes on the spit ----
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var rules = Server.System<BadBloodCampRuleSystem>();
                var camp = Server.System<BadBloodCampSystem>();
                var xforms = Server.System<SharedTransformSystem>();

                Assert.Multiple(() =>
                {
                    Assert.That(rules.GetSp(), Is.EqualTo(FixedPoint4.New(1)), "an offered trophy pays the clan 1 SP");
                    Assert.That(rules.GetOfferings(first), Is.EqualTo(1));
                    Assert.That(entMan.GetComponent<TransformComponent>(skull).ParentUid, Is.EqualTo(lodge),
                        "the offered trophy hangs on the Lodge");
                });

                Assert.That(camp.TryOfferTrophy(grunt, scalp), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(rules.GetSp(), Is.EqualTo(FixedPoint4.New(2)));
                    Assert.That(rules.GetOfferings(first), Is.EqualTo(2));
                    Assert.That(rules.CanOffer(first), Is.False, "a body gives two trophies and no more");
                });

                // trophies first, the fire takes the body
                var cook = new Entity<BadBloodCampCookComponent>(spit, entMan.GetComponent<BadBloodCampCookComponent>(spit));
                cook.Comp.CorpseCookTime = TimeSpan.FromSeconds(2);
                xforms.SetCoordinates(grunt, SpotBeside(spit));
                Assert.That(camp.TryStartCookCorpse(cook, grunt, first), Is.True);
            });

            await Pair.RunSeconds(3.5f);

            // ---- tier I: the armory holds its tiers. the second body follows the first ----
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var rules = Server.System<BadBloodCampRuleSystem>();
                var camp = Server.System<BadBloodCampSystem>();
                var trophies = Server.System<YautjaTrophySystem>();
                var xforms = Server.System<SharedTransformSystem>();

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.EntityExists(first), Is.False, "the fire takes the body");
                    Assert.That(rules.GetSp(), Is.EqualTo(FixedPoint4.New(5)), "a cooked corpse pays the clan 3 SP");
                    Assert.That(rules.GetLifetimeSp(), Is.EqualTo(FixedPoint4.New(5)));
                    Assert.That(rules.GetTier(), Is.EqualTo(1));
                });

                var armoryEnt = new Entity<BadBloodCampArmoryComponent>(armory,
                    entMan.GetComponent<BadBloodCampArmoryComponent>(armory));
                camp.PrepareArmoryBuyer(armoryEnt, hunter);
                Vend(armory, hunter, Combistick);
                Assert.That(rules.GetSp(), Is.EqualTo(FixedPoint4.New(5)), "a tier II ware is refused at tier I");

                // the second body's trophies. the rag and the gel were shown above, here they are just marked
                Assert.That(trophies.TryHarvestTrophy(hunter, second, YautjaTrophyKind.HumanSkull, out var secondSkull), Is.True);
                var secondScalp = trophies.SpawnRuntimeScalp(second, hunter);
                xforms.DropNextTo(secondSkull, hunter);
                entMan.GetComponent<YautjaTrophyComponent>(secondSkull).Polished = true;
                entMan.GetComponent<YautjaTrophyComponent>(secondScalp).Polished = true;
                Assert.That(camp.TryOfferTrophy(hunter, secondSkull), Is.True);
                Assert.That(camp.TryOfferTrophy(grunt, secondScalp), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(rules.GetSp(), Is.EqualTo(FixedPoint4.New(7)));
                    Assert.That(rules.GetTier(), Is.EqualTo(1), "7 lifetime SP is still tier I");
                    Assert.That(camp.GetOfferedTrophyCount(), Is.EqualTo(4));
                });

                var cook = new Entity<BadBloodCampCookComponent>(spit, entMan.GetComponent<BadBloodCampCookComponent>(spit));
                Assert.That(camp.TryStartCookCorpse(cook, grunt, second), Is.True);
            });

            await Pair.RunSeconds(3.5f);

            // ---- tier II unlocks and a purchase spends the pool. a Totem is raised, pays and falls ----
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var rules = Server.System<BadBloodCampRuleSystem>();
                var camp = Server.System<BadBloodCampSystem>();
                var totems = Server.System<BadBloodTotemSystem>();
                var intel = Server.System<IntelSystem>();
                var xforms = Server.System<SharedTransformSystem>();
                var now = Server.Timing.CurTime;
                Assert.That(rules.TryGetRule(out var rule), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(rules.GetSp(), Is.EqualTo(FixedPoint4.New(10)));
                    Assert.That(rules.GetLifetimeSp(), Is.EqualTo(FixedPoint4.New(10)));
                    Assert.That(rules.GetTier(), Is.EqualTo(1), "two bodies are 10 lifetime SP, tier II needs 12");
                });

                // the free Totem, raised away from the Lodge
                Assert.That(entMan.HasComponent<BadBloodTotemBuilderComponent>(grunt), Is.True,
                    "every Bad Blood can raise a Totem while the rule runs");
                var home = entMan.GetComponent<TransformComponent>(grunt).Coordinates;
                xforms.SetCoordinates(grunt, map.GridCoords.Offset(new Vector2(TotemColumn, 0)));
                Assert.That(totems.TryRaiseTotem(grunt, out var totem), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(rule.Comp.FreeTotems, Is.Zero);
                    Assert.That(rules.GetSp(), Is.EqualTo(FixedPoint4.New(10)), "the first Totem is free");
                    Assert.That(rules.GetTotems(), Has.Count.EqualTo(1));
                });

                // twenty uncontested minutes of a ripe Totem: 14 SP to spend, half of it towards the tiers
                var comp = entMan.GetComponent<BadBloodTotemComponent>(totem);
                comp.PlacedAt = now - TimeSpan.FromMinutes(21);
                Assert.That(totems.IsRipe(totem), Is.True);
                var sp = rules.GetSp();
                var lifetime = rules.GetLifetimeSp();
                totems.Accrue((totem, comp), now, TimeSpan.FromMinutes(20));
                Assert.Multiple(() =>
                {
                    Assert.That((rules.GetSp() - sp).Double(), Is.EqualTo(14.0).Within(0.011), "a Totem pays the clan");
                    Assert.That((rules.GetLifetimeSp() - lifetime).Double(), Is.EqualTo(7.0).Within(0.011),
                        "Totem income counts 50% towards tiers");
                    Assert.That(rules.GetTier(), Is.EqualTo(2), "17 lifetime SP unlock tier II");
                });

                // tier II: a purchase spends the pool and never lowers the tier
                var armoryEnt = new Entity<BadBloodCampArmoryComponent>(armory,
                    entMan.GetComponent<BadBloodCampArmoryComponent>(armory));
                camp.PrepareArmoryBuyer(armoryEnt, hunter);
                var beforeBuy = rules.GetSp();
                var lifetimeBeforeBuy = rules.GetLifetimeSp();
                var combistickPrice = FindWare(armory, Combistick).Entry.Points;
                Assert.That(combistickPrice, Is.Not.Null);
                Vend(armory, hunter, Combistick);
                Assert.Multiple(() =>
                {
                    Assert.That(rules.GetSp(), Is.EqualTo(beforeBuy - FixedPoint4.New(combistickPrice!.Value)),
                        "the combi-stick costs the clan its price");
                    Assert.That(rules.GetLifetimeSp(), Is.EqualTo(lifetimeBeforeBuy), "spending does not lower the tier");
                    Assert.That(rules.GetTier(), Is.EqualTo(2));
                });

                // a marine takes it down: the marines are paid in intel and the Lodge shows on their map
                var intelBefore = intel.EnsureTechTree(IntelTeam).Comp.Tree.Points;
                Assert.That(camp.IsLodgeRevealed(), Is.False);
                Assert.That(Server.System<SharedDestructibleSystem>().DestroyEntity(totem), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(rules.GetTotems(), Is.Empty);
                    Assert.That(camp.IsLodgeRevealed(), Is.True, "a destroyed Totem reveals the Lodge");
                    Assert.That(intel.EnsureTechTree(IntelTeam).Comp.Tree.Points - intelBefore, Is.EqualTo(FixedPoint2.New(0.7)),
                        "a destroyed Totem is worth 0.7 intel");
                });

                xforms.SetCoordinates(grunt, home);

                // ---- a squad is bought and its seats open ----
                // the test server has next to no players: lift the population caps so the purchase is the thing tested
                rule.Comp.ThreatBudgetFraction = 1000f;
                rule.Comp.HeldVoteSeats = 10;
                rule.Comp.SquadSpawnTimeout = TimeSpan.FromSeconds(6);
                rule.Comp.ReserveQueue.Clear();
                rule.Comp.DeadBadBloods.Clear();
                Server.PlayerMan.SetAttachedEntity(session, null);

                var beforeCall = rules.GetSp();
                Assert.That(camp.TryBuySquad(grunt, 2), Is.False, "a grunt cannot call a squad while the Leader lives");
                Assert.That(camp.TryBuySquad(leader, 2), Is.True);
                Assert.That(rules.GetSp(), Is.EqualTo(beforeCall - rule.Comp.SquadOfTwoCost), "a squad of two costs the clan its price");
                Assert.That(rule.Comp.SquadCalls, Has.Count.EqualTo(1));
                var call = rule.Comp.SquadCalls[0];
                Assert.That(camp.GetPendingSquadSeats(), Is.EqualTo(2));
                Assert.That(camp.TryGetSquadSeats(call, out entry), Is.True);
                Assert.That(entMan.HasComponent<GhostRoleComponent>(entry), Is.True, "the seats are a ghost role");

                // the raffle draws the player: they hold a seat until the squad arrives
                var take = new TakeGhostRoleEvent(session);
                entMan.EventBus.RaiseLocalEvent(entry, ref take);
                Assert.That(take.TookRole, Is.True);
                Assert.That(call.Claimed, Is.EquivalentTo(new[] { session.UserId }));
                spAfterCall = rules.GetSp();
            });

            await Pair.RunSeconds(8);

            // ---- the squad arrives. the Leader walks off and dies ----
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var rules = Server.System<BadBloodCampRuleSystem>();
                var xforms = Server.System<SharedTransformSystem>();
                Assert.That(rules.TryGetRule(out var rule), Is.True);

                arrival = session.AttachedEntity;
                Assert.That(arrival, Is.Not.Null, "the seat holder arrives when the call times out");
                Assert.Multiple(() =>
                {
                    Assert.That(entMan.GetComponent<MetaDataComponent>(arrival!.Value).EntityPrototype?.ID,
                        Is.EqualTo(BadBloodTestHelpers.GruntPrototype), "arrivals are grunts");
                    Assert.That(entMan.HasComponent<YautjaBadBloodComponent>(arrival.Value), Is.True);
                    Assert.That(entMan.TryGetComponent(arrival.Value, out ThreatComponent? threat), Is.True);
                    Assert.That(threat?.ObjectiveJob, Is.EqualTo(BadBloodTestHelpers.MemberJob));
                    Assert.That(rule.Comp.SquadCalls, Is.Empty);
                    Assert.That(rules.GetSp(), Is.EqualTo(spAfterCall + FixedPoint4.New(6)),
                        "the seat nobody took gives its 6 SP back");
                    Assert.That(rules.GetRoundOutcome(), Is.EqualTo(BadBloodRoundOutcome.None));
                });

                Assert.That(Server.System<InventorySystem>().TryGetSlotEntity(leader, BracerSlot, out var worn), Is.True);
                oldBracer = worn!.Value;
                Assert.That(entMan.HasComponent<BadBloodLeaderBracerComponent>(oldBracer), Is.True);

                xforms.SetCoordinates(leader, map.GridCoords.Offset(new Vector2(GraveColumn, 0)));
            });

            await BadBloodTestHelpers.Kill(Pair, leader);

            // ---- the ascension vote: level on votes, the kill count decides ----
            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                var ascension = Server.System<BadBloodAscensionSystem>();

                Assert.Multiple(() =>
                {
                    Assert.That(rules.IsLeaderGone(), Is.True);
                    Assert.That(rules.IsMarineVictoryDue(), Is.False, "a dead Leader does not end the round while the Lodge stands");
                    Assert.That(ascension.IsVoteOpen(), Is.True, "the vote opens the moment the Leader falls");
                    Assert.That(ascension.GetVote()?.FromBracer, Is.False, "with a Lodge the vote is held at the Stone");
                });

                Assert.That(ascension.TryStandAsCandidate(grunt), Is.True);
                Assert.That(ascension.TryStandAsCandidate(hunter), Is.True);
                Assert.That(ascension.TryVote(leader, grunt), Is.False, "the dead do not vote");
                Assert.That(ascension.TryVote(grunt, grunt), Is.True);
                Assert.That(ascension.TryVote(hunter, hunter), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(ascension.CountVotes(grunt), Is.EqualTo(1));
                    Assert.That(ascension.CountVotes(hunter), Is.EqualTo(1));
                    Assert.That(ascension.GetFrontRunner(), Is.EqualTo(hunter),
                        "one vote each: the two kills beat the grunt who stood first");
                });

                // let the clock close it
                ascension.GetVote()!.EndsAt = Server.Timing.CurTime;
            });

            await Pair.RunSeconds(1);

            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var rules = Server.System<BadBloodCampRuleSystem>();
                var ascension = Server.System<BadBloodAscensionSystem>();

                Assert.Multiple(() =>
                {
                    Assert.That(ascension.IsVoteOpen(), Is.False, "the vote closes by itself when its time is up");
                    Assert.That(rules.GetLeader(), Is.EqualTo(hunter));
                    Assert.That(rules.IsLeaderGone(), Is.False);
                    Assert.That(entMan.HasComponent<BadBloodLeaderComponent>(hunter), Is.True);
                    Assert.That(entMan.HasComponent<BadBloodLeaderComponent>(leader), Is.False);
                    Assert.That(entMan.GetComponent<ThreatComponent>(hunter).ObjectiveJob, Is.EqualTo(BadBloodTestHelpers.LeaderJob),
                        "the kill-the-leader objective follows the new Leader");
                    Assert.That(entMan.GetComponent<ThreatComponent>(leader).ObjectiveJob, Is.EqualTo(BadBloodTestHelpers.MemberJob));
                    Assert.That(rules.GetRoundOutcome(), Is.EqualTo(BadBloodRoundOutcome.None));
                });

                Assert.That(entMan.TryGetComponent(oldBracer, out BadBloodScuttleComponent? scuttle), Is.True,
                    "the fallen Leader's bracer scuttles");
                fuse = scuttle!.Fuse;

                // the winner's own bracer is promoted, never scuttled
                if (Server.System<InventorySystem>().TryGetSlotEntity(hunter, BracerSlot, out var own))
                {
                    Assert.That(entMan.HasComponent<BadBloodLeaderBracerComponent>(own.Value), Is.True);
                    Assert.That(entMan.HasComponent<BadBloodScuttleComponent>(own.Value), Is.False);
                }
            });

            await Pair.RunSeconds((float) fuse.TotalSeconds + 2f);

            // ---- the Lodge falls, then the new Leader: marines win ----
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var rules = Server.System<BadBloodCampRuleSystem>();
                var camp = Server.System<BadBloodCampSystem>();
                Assert.That(rules.TryGetRule(out var rule), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.Deleted(oldBracer), Is.True, "the old leader bracer is gone after its countdown");
                    Assert.That(Server.System<MobStateSystem>().IsAlive(hunter), Is.True);
                    Assert.That(rules.GetLodge(), Is.EqualTo(lodge));
                    Assert.That(rules.GetRoundOutcome(), Is.EqualTo(BadBloodRoundOutcome.None));
                });

                Assert.That(Server.System<SharedDestructibleSystem>().DestroyEntity(lodge), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(rules.HasLodge(), Is.False);
                    Assert.That(rule.Comp.NextCampAt, Is.Not.Null, "the rebuild cooldown runs");
                    Assert.That(camp.TryPitchCamp(hunter), Is.False, "no new Lodge before the cooldown is over");
                    Assert.That(rules.IsMarineVictoryDue(), Is.False, "the Leader lives, so losing the Lodge is not the end");
                });
            });

            await Pair.RunSeconds(2);

            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                Assert.Multiple(() =>
                {
                    Assert.That(rules.GetRoundOutcome(), Is.EqualTo(BadBloodRoundOutcome.None));
                    Assert.That(rules.GetSp(), Is.EqualTo(spAfterCall + FixedPoint4.New(6)), "the clan's points outlive the Lodge");
                });
            });

            await BadBloodTestHelpers.Kill(Pair, hunter);
            await Pair.RunSeconds(2);

            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                Assert.That(rules.TryGetRule(out var rule), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(rules.GetRoundOutcome(), Is.EqualTo(BadBloodRoundOutcome.MarinesLeaderAndLodge),
                        "Leader gone and no Lodge: marines win, with Bad Bloods still alive");
                    Assert.That(Server.EntMan.GetComponent<BadBloodCampEndComponent>(rule.Owner).DecidedAt, Is.Not.Null);
                    Assert.That(rules.CountLivingBadBloods(), Is.GreaterThan(0));
                    Assert.That(rules.TryDecideRound(BadBloodRoundOutcome.BadBloodDeparture), Is.False, "a round is decided once");
                });
            });
        }
        finally
        {
            await Cleanup(map, party);
        }
    }

    [Test]
    public async Task ShipIsCalledArrivesSummonsAndTheStandingLodgeWinsForTheClan()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        var session = Pair.Player!;
        var grunt = party.Grunts[0];
        EntityUid lodge = default;
        EntityUid armory = default;
        var living = 0;
        var left = TimeSpan.Zero;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                Assert.That(Server.System<GameTicker>().RunLevel, Is.Not.EqualTo(GameRunLevel.InRound));
                Assert.That(rules.TryGetRule(out var rule), Is.True);
                Assert.That(rules.GetPlayerCount(), Is.GreaterThan(0), "the summoning cap is a share of the player count");

                rule.Comp.LeaderSsdTimeout = TimeSpan.FromHours(1);
                rule.Comp.ShipInboundTime = TimeSpan.FromSeconds(4);
                rule.Comp.SummonInterval = TimeSpan.FromSeconds(1);
                rule.Comp.SquadSpawnTimeout = TimeSpan.FromHours(1);
                rule.Comp.DepartureTime = TimeSpan.FromSeconds(20);
                rule.Comp.ReserveQueue.Clear();
                rule.Comp.DeadBadBloods.Clear();
                Server.PlayerMan.SetAttachedEntity(session, null);

                // the budget leaves room for two more than the clan and its hounds
                living = rules.CountLivingBadBloods();
                var bodies = living + Server.System<BadBloodHoundSystem>().CountHounds();
                rule.Comp.ThreatBudgetFraction = (bodies + 2f) / rules.GetPlayerCount();
                Assert.That(rules.GetThreatBudget(), Is.EqualTo(bodies + 2));

                // three ripe Totems have been held and the lock has lifted, but there is no Lodge to call from.
                // they pay nothing here, so the SP below stays what the test put in
                rule.Comp.ShipEarliestCall = TimeSpan.Zero;
                rule.Comp.RipeHoldForCall = TimeSpan.Zero;
                for (var i = 1; i <= rule.Comp.RipeTotemsForCall; i++)
                {
                    var totem = Server.EntMan.SpawnEntity(Totem, map.GridCoords.Offset(new Vector2(30 * i, 0)));
                    var totemComp = Server.EntMan.GetComponent<BadBloodTotemComponent>(totem);
                    totemComp.RipeAfter = TimeSpan.Zero;
                    totemComp.YoungIncome = 0f;
                    totemComp.GrowingIncome = 0f;
                    totemComp.RipeIncome = 0f;
                }

                Assert.That(Server.System<BadBloodTotemSystem>().CountRipeTotems(), Is.EqualTo(rule.Comp.RipeTotemsForCall));
                Assert.That(rules.TryCallShip(), Is.False);
                Assert.That(rules.GetDepartureRemaining(), Is.Null);
            });

            await Pair.RunSeconds(2);

            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                Assert.That(rules.GetShipState(), Is.EqualTo(BadBloodShipState.None), "the call needs a standing Lodge");

                lodge = Server.EntMan.SpawnEntity(Lodge, map.GridCoords);
                armory = Server.EntMan.SpawnEntity(Armory, map.GridCoords);
                Assert.That(rules.GetLodge(), Is.EqualTo(lodge));
                Assert.That(rules.AddSp(FixedPoint4.New(5)), Is.True);
            });

            await Pair.RunSeconds(2);

            // ---- the call ----
            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                var camp = Server.System<BadBloodCampSystem>();
                Assert.Multiple(() =>
                {
                    Assert.That(rules.IsShipInbound(), Is.True, "the held Totems call the ship once a Lodge stands");
                    Assert.That(rules.GetShipTimeLeft(), Is.Not.Null);
                    Assert.That(camp.IsLodgeRevealed(), Is.True, "the Lodge is on the marine map while the ship is inbound");
                    Assert.That(rules.GetTier(), Is.EqualTo(1));
                    Assert.That(rules.GetDepartureRemaining(), Is.Null, "no departure timer before the ship is here");
                    Assert.That(camp.GetPendingSquadSeats(), Is.Zero, "nobody is summoned before the ship is here");
                    Assert.That(rules.TryCallShip(), Is.False, "the ship is called once");
                });
            });

            await Pair.RunSeconds(9);

            // ---- arrival: free seats up to the cap, tier IV ----
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var rules = Server.System<BadBloodCampRuleSystem>();
                var camp = Server.System<BadBloodCampSystem>();
                Assert.That(rules.TryGetRule(out var rule), Is.True);

                Assert.That(rules.IsShipArrived(), Is.True);
                var remaining = rules.GetDepartureRemaining();
                Assert.That(remaining, Is.Not.Null);
                Assert.Multiple(() =>
                {
                    Assert.That(rule.Comp.TierFourUnlocked, Is.True);
                    Assert.That(rules.GetTier(), Is.EqualTo(4), "tier IV unlocks on arrival");
                    Assert.That(remaining!.Value, Is.LessThan(TimeSpan.FromSeconds(20)), "the departure timer runs while the Lodge stands");
                    Assert.That(remaining.Value, Is.GreaterThan(TimeSpan.FromSeconds(5)));
                    Assert.That(camp.GetPendingSquadSeats(), Is.EqualTo(2), "a free seat opens every interval, up to the cap");
                    Assert.That(rules.GetSp(), Is.EqualTo(FixedPoint4.New(5)), "summoned seats cost the clan nothing");
                    Assert.That(rules.GetRoundOutcome(), Is.EqualTo(BadBloodRoundOutcome.None));
                    Assert.That(rules.HasLodge(), Is.True);
                });

                // tier IV at the armory: the war glaive, a tier III ware, at half price
                var armoryEnt = new Entity<BadBloodCampArmoryComponent>(armory,
                    entMan.GetComponent<BadBloodCampArmoryComponent>(armory));
                Assert.That(FindWare(armory, Glaive).Entry.Points, Is.EqualTo(4), "the armory turns over to tier IV prices");
                camp.PrepareArmoryBuyer(armoryEnt, grunt);
                Vend(armory, grunt, Glaive);
                Assert.That(rules.GetSp(), Is.EqualTo(FixedPoint4.New(1)), "the glaive is half price at tier IV");

                // a player takes one of the summoned seats
                Assert.That(camp.TryGetSquadSeats(rule.Comp.SquadCalls[0], out var entry), Is.True);
                var take = new TakeGhostRoleEvent(session);
                entMan.EventBus.RaiseLocalEvent(entry, ref take);
                Assert.That(take.TookRole, Is.True);
            });

            await Pair.RunSeconds(3);

            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var rules = Server.System<BadBloodCampRuleSystem>();
                var camp = Server.System<BadBloodCampSystem>();

                var summoned = session.AttachedEntity;
                Assert.That(summoned, Is.Not.Null, "a full seat arrives at once");
                Assert.Multiple(() =>
                {
                    Assert.That(entMan.GetComponent<MetaDataComponent>(summoned!.Value).EntityPrototype?.ID,
                        Is.EqualTo(BadBloodTestHelpers.GruntPrototype));
                    Assert.That(entMan.HasComponent<YautjaBadBloodComponent>(summoned.Value), Is.True);
                    Assert.That(rules.CountLivingBadBloods(), Is.EqualTo(living + 1));
                    Assert.That(camp.GetPendingSquadSeats(), Is.EqualTo(1), "the cap holds: the arrival took one of the two places");
                    Assert.That(rules.GetRoundOutcome(), Is.EqualTo(BadBloodRoundOutcome.None));
                });

                left = rules.GetDepartureRemaining()!.Value;
                Assert.That(left, Is.GreaterThan(TimeSpan.Zero));
            });

            // ---- the Lodge stands until the ship leaves ----
            await Pair.RunSeconds((float) left.TotalSeconds + 2f);

            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                Assert.That(rules.TryGetRule(out var rule), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(rules.HasLodge(), Is.True);
                    Assert.That(rules.GetDepartureRemaining(), Is.EqualTo(TimeSpan.Zero));
                    Assert.That(rules.GetRoundOutcome(), Is.EqualTo(BadBloodRoundOutcome.BadBloodDeparture),
                        "the Lodge stood until the ship left: the Bad Bloods win");
                    Assert.That(Server.EntMan.GetComponent<BadBloodCampEndComponent>(rule.Owner).DecidedAt, Is.Not.Null);
                    Assert.That(rules.TryDecideRound(BadBloodRoundOutcome.MarinesLeaderAndLodge), Is.False, "a round is decided once");
                });
            });
        }
        finally
        {
            await Cleanup(map, party);
        }
    }

    [Test]
    public async Task DepartureTimerPausesWithoutALodgeAndMarinesStillWinInTheShipPhase()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        EntityUid lodge = default;
        var paused = TimeSpan.Zero;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                Assert.That(Server.System<GameTicker>().RunLevel, Is.Not.EqualTo(GameRunLevel.InRound));
                Assert.That(rules.TryGetRule(out var rule), Is.True);

                rule.Comp.LeaderSsdTimeout = TimeSpan.FromHours(1);
                rule.Comp.ShipInboundTime = TimeSpan.FromSeconds(1);
                rule.Comp.SummonInterval = TimeSpan.FromHours(1);
                rule.Comp.DepartureTime = TimeSpan.FromSeconds(15);

                lodge = Server.EntMan.SpawnEntity(Lodge, map.GridCoords);
                Assert.That(rules.TryCallShip(), Is.True);
            });

            await Pair.RunSeconds(4);

            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                Assert.That(rules.TryGetRule(out var rule), Is.True);
                Assert.That(rules.IsShipArrived(), Is.True);

                var running = rules.GetDepartureRemaining();
                Assert.That(running, Is.Not.Null);
                Assert.That(running!.Value, Is.LessThan(TimeSpan.FromSeconds(15)), "the timer runs while the Lodge stands");
                Assert.That(running.Value, Is.GreaterThan(TimeSpan.FromSeconds(5)));

                // the Lodge is as destructible in the ship phase as before, and the timer stops where it is
                Assert.That(Server.System<SharedDestructibleSystem>().DestroyEntity(lodge), Is.True);
                paused = rules.GetDepartureRemaining()!.Value;
                Assert.Multiple(() =>
                {
                    Assert.That(rules.HasLodge(), Is.False);
                    Assert.That(rules.IsShipArrived(), Is.True, "the ship stays in orbit");
                    Assert.That(rule.Comp.NextSummonAt, Is.Null, "nobody is summoned to a ruin");
                });
            });

            await Pair.RunSeconds(4);

            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                Assert.Multiple(() =>
                {
                    Assert.That(rules.GetDepartureRemaining(), Is.EqualTo(paused), "no Lodge, no countdown");
                    Assert.That(rules.GetRoundOutcome(), Is.EqualTo(BadBloodRoundOutcome.None), "the Leader lives, the round goes on");
                });

                // re-pitched: it resumes from what was left
                lodge = Server.EntMan.SpawnEntity(Lodge, map.GridCoords);
                Assert.That(rules.GetLodge(), Is.EqualTo(lodge));
            });

            await Pair.RunSeconds(3);

            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                var resumed = rules.GetDepartureRemaining();
                Assert.That(resumed, Is.Not.Null);
                Assert.Multiple(() =>
                {
                    Assert.That(resumed!.Value, Is.LessThan(paused), "the timer runs again");
                    Assert.That(resumed.Value, Is.GreaterThan(TimeSpan.Zero));
                    Assert.That(rules.GetRoundOutcome(), Is.EqualTo(BadBloodRoundOutcome.None));
                });

                // the marines take the Lodge again, and this time the Leader with it
                Assert.That(Server.System<SharedDestructibleSystem>().DestroyEntity(lodge), Is.True);
                paused = rules.GetDepartureRemaining()!.Value;
            });

            await BadBloodTestHelpers.Kill(Pair, party.Leader);
            await Pair.RunSeconds(2);

            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                Assert.Multiple(() =>
                {
                    Assert.That(rules.IsShipArrived(), Is.True);
                    Assert.That(rules.GetDepartureRemaining(), Is.EqualTo(paused), "the clan never got its departure");
                    Assert.That(rules.GetRoundOutcome(), Is.EqualTo(BadBloodRoundOutcome.MarinesLeaderAndLodge),
                        "Leader dead and Lodge destroyed ends the round in the ship phase too");
                    Assert.That(rules.TryDecideRound(BadBloodRoundOutcome.BadBloodDeparture), Is.False);
                });
            });
        }
        finally
        {
            await Cleanup(map, party);
        }
    }

    // the landing: a forced spawn puts the volunteer in the Leader's body, and the rule crowns that body
    [Test]
    public async Task ForcedSpawnSeatsTheVolunteerAsTheLeaderTheRuleCrowns()
    {
        var map = await Pair.CreateTestMap();
        var session = Pair.Player!;
        var party = new BadBloodTestParty();

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;

                // seating a player books them into the current round. a fresh pooled server has never started one,
                // and round 0 has no row in the database: give it one the way StartRound does
                var ticker = Server.System<GameTicker>();
                if (ticker.RoundId == 0)
                {
                    typeof(GameTicker)
                        .GetMethod("IncrementRoundNumber", BindingFlags.Instance | BindingFlags.NonPublic)!
                        .Invoke(ticker, null);
                }

                Assert.That(ticker.RoundId, Is.Not.Zero);

                Server.PlayerMan.SetAttachedEntity(session, null);
                entMan.SpawnEntity("threatleaderspawnmarker", map.GridCoords);
                for (var i = 0; i < 3; i++)
                {
                    entMan.SpawnEntity("threatmemberspawnmarker", map.GridCoords);
                }

                // the win conditions are left off: the camp rule is started below, the way the threat would
                Assert.That(Server.System<ThreatSystem>().ForceSpawnThreat(BadBloodThreat, map.Grid, false), Is.True);
                var interest = Server.System<ForceInterestSystem>();
                var id = interest.GetForces(session).Max(force => force.Identifier);
                interest.SetInterest(session, id, true);
                Assert.That(interest.GetForces(session).Single(force => force.Identifier == id).Interested, Is.True);

                // one volunteer is not a quorum for four roles, so staff deploy it by hand
                interest.CompleteForce(session, id);
            });

            await Pair.RunSeconds(4);

            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var seated = session.AttachedEntity;
                Assert.That(seated, Is.Not.Null, "a ghost who registered interest must be put into a body");
                party.Leader = seated!.Value;

                var clan = entMan.EntityQuery<YautjaBadBloodComponent>()
                    .Select(comp => comp.Owner)
                    .Where(uid => entMan.GetComponent<TransformComponent>(uid).MapID == map.MapId)
                    .ToList();

                Assert.Multiple(() =>
                {
                    Assert.That(clan, Has.Count.EqualTo(4), "the base party is one Leader and three grunts");
                    Assert.That(entMan.GetComponent<MetaDataComponent>(seated.Value).EntityPrototype?.ID,
                        Is.EqualTo(BadBloodTestHelpers.LeaderPrototype), "volunteers fill the Leader body first");
                    Assert.That(entMan.GetComponent<ThreatComponent>(seated.Value).ObjectiveJob,
                        Is.EqualTo(BadBloodTestHelpers.LeaderJob));
                });
            });

            await BadBloodTestHelpers.StartBadBloodRule(Pair, party);
            await Pair.RunSeconds(3);

            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var rules = Server.System<BadBloodCampRuleSystem>();
                Assert.That(party.Rule, Is.Not.Null);
                Assert.That(rules.TryGetRule(out var rule), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(rules.GetLeader(), Is.EqualTo(party.Leader), "the rule crowns the seated Leader");
                    Assert.That(rule.Comp.LandingParty, Is.GreaterThanOrEqualTo(4));
                    Assert.That(rules.HasLivingLeader(), Is.True);
                    Assert.That(rule.Comp.LeaderDisconnectedAt, Is.Null, "a player is driving the Leader");
                    Assert.That(entMan.GetComponent<BadBloodLeaderComponent>(party.Leader).PitchCampActionEntity, Is.Not.Null,
                        "the seated Leader holds the Pitch Camp action");
                    Assert.That(rules.GetRoundOutcome(), Is.EqualTo(BadBloodRoundOutcome.None));
                });
            });
        }
        finally
        {
            await Cleanup(map, party);
        }
    }

    #region Helpers

    // the kit only deploys on a planet, and the Lodge, its parts, the Totem and everyone walking need floor
    private async Task LayGround(TestMapData map)
    {
        await Server.WaitPost(() =>
        {
            Server.EntMan.EnsureComponent<RMCPlanetComponent>(map.Grid);
            var maps = Server.System<SharedMapSystem>();
            for (var x = -3; x <= 5; x++)
            {
                for (var y = -3; y <= 5; y++)
                {
                    maps.SetTile(map.Grid, map.Tile.GridIndices + new Vector2i(x, y), map.Tile.Tile);
                }
            }

            maps.SetTile(map.Grid, map.Tile.GridIndices + new Vector2i(TotemColumn, 0), map.Tile.Tile);
            maps.SetTile(map.Grid, map.Tile.GridIndices + new Vector2i(GraveColumn, 0), map.Tile.Tile);
        });
    }

    // a marine with a mind, so the kill is a worthy one. call on the server thread
    private EntityUid SpawnPrey(TestMapData map, int column)
    {
        var body = Server.EntMan.SpawnEntity(Human, map.GridCoords.Offset(new Vector2(column, 0)));
        var minds = Server.System<SharedMindSystem>();
        minds.TransferTo(minds.CreateMind(null, "prey"), body);
        return body;
    }

    private EntityUid SpawnBeside(string prototype, EntityUid at)
    {
        return Server.EntMan.SpawnEntity(prototype, Server.EntMan.GetComponent<TransformComponent>(at).Coordinates);
    }

    // the click a player makes with an item on a target
    private bool UseOn(EntityUid user, EntityUid used, EntityUid target)
    {
        var entMan = Server.EntMan;
        var use = new InteractUsingEvent(user, used, target, entMan.GetComponent<TransformComponent>(target).Coordinates);
        entMan.EventBus.RaiseLocalEvent(target, use);
        return use.Handled;
    }

    // a free tile next to the target, clear of the Lodge, its parts and the sleeping hounds, so whoever works there
    // is in reach and is not pushed about
    private EntityCoordinates SpotBeside(EntityUid target)
    {
        var entMan = Server.EntMan;
        var xforms = Server.System<SharedTransformSystem>();
        var taken = new List<Vector2>();

        var parts = entMan.EntityQueryEnumerator<BadBloodCampPartComponent, TransformComponent>();
        while (parts.MoveNext(out _, out _, out var xform))
        {
            taken.Add(xforms.GetWorldPosition(xform));
        }

        var lodges = entMan.EntityQueryEnumerator<BadBloodCampComponent, TransformComponent>();
        while (lodges.MoveNext(out _, out _, out var xform))
        {
            taken.Add(xforms.GetWorldPosition(xform));
        }

        var hounds = entMan.EntityQueryEnumerator<BadBloodSleepingHoundComponent, TransformComponent>();
        while (hounds.MoveNext(out _, out _, out var xform))
        {
            taken.Add(xforms.GetWorldPosition(xform));
        }

        var origin = entMan.GetComponent<TransformComponent>(target).Coordinates;
        foreach (var offset in new[] { new Vector2(0, 1), new Vector2(0, -1), new Vector2(1, 0), new Vector2(-1, 0) })
        {
            var spot = origin.Offset(offset);
            var world = xforms.ToMapCoordinates(spot).Position;
            if (taken.All(position => (position - world).Length() > 0.6f))
                return spot;
        }

        Assert.Fail($"There is no free tile beside {target}.");
        return origin;
    }

    private (int Section, int Index, CMVendorEntry Entry) FindWare(EntityUid vendor, string id)
    {
        var sections = Server.EntMan.GetComponent<CMAutomatedVendorComponent>(vendor).Sections;
        for (var section = 0; section < sections.Count; section++)
        {
            for (var entry = 0; entry < sections[section].Entries.Count; entry++)
            {
                if (sections[section].Entries[entry].Id.Id == id)
                    return (section, entry, sections[section].Entries[entry]);
            }
        }

        Assert.Fail($"The vendor does not sell {id}.");
        return default;
    }

    // the vend message the vendor window sends, so the purchase goes through the vendor's own points logic
    private void Vend(EntityUid vendor, EntityUid user, string id)
    {
        var (section, entry, _) = FindWare(vendor, id);
        Server.EntMan.EventBus.RaiseLocalEvent(vendor, new CMVendorVendBuiMsg(section, entry, new())
        {
            Actor = user,
            UiKey = CMAutomatedVendorUI.Key,
        });
    }

    // pooled servers are shared: end the rule, clear the selected threat, and take everything off the test map,
    // the party, the Lodge and its parts, open seats, corpses, trophies and vended wares alike
    private async Task Cleanup(TestMapData map, BadBloodTestParty party)
    {
        await BadBloodTestHelpers.EndBadBloodRule(Pair, party);
        await Server.WaitPost(() =>
        {
            var entMan = Server.EntMan;
            if (Pair.Player is { } session)
                Server.PlayerMan.SetAttachedEntity(session, null);

            Server.System<AuRoundSystem>().SetSelectedThreat(null);

            var grid = map.Grid.Owner;
            var doomed = new List<EntityUid>();
            var query = entMan.AllEntityQueryEnumerator<TransformComponent>();
            while (query.MoveNext(out var uid, out var xform))
            {
                if (uid != grid && (xform.ParentUid == grid || xform.ParentUid == map.MapUid))
                    doomed.Add(uid);
            }

            foreach (var uid in doomed)
            {
                if (entMan.EntityExists(uid))
                    entMan.DeleteEntity(uid);
            }
        });
    }

    #endregion
}
