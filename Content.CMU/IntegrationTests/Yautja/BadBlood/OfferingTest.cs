using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Yautja.BadBlood;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Destructible;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Stacks;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// FLOW-109: the Lodge takes prepared trophies from worthy prey, two per body, and pays the clan for each
[TestFixture]
public sealed class OfferingTest : GameTest
{
    private const string Lodge = "CMUBadBloodCamp";
    private const string Hide = "CMUYautjaHumanHide";
    private const string Bone = "CMUYautjaHumanLeftArmBoneTrophy";
    private const string Skull = "CMUYautjaHumanSkullTrophy";
    private const string XenoSkull = "CMUYautjaXenoSkullTrophy";
    private const string Human = "CMMobHuman";

    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task LodgeTakesTwoPreparedTrophiesPerWorthyBody()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        var spawned = new List<EntityUid>();

        EntityUid lodge = default;
        EntityUid curedHide = default;
        EntityUid bone = default;
        EntityUid xenoSkull = default;
        EntityUid body = default;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var camp = Server.System<BadBloodCampSystem>();
                var rules = Server.System<BadBloodCampRuleSystem>();
                var stacks = Server.System<SharedStackSystem>();

                lodge = entMan.SpawnEntity(Lodge, map.GridCoords);
                spawned.Add(lodge);
                Assert.That(rules.GetLodge(), Is.EqualTo(lodge));
                Assert.That(entMan.HasComponent<BadBloodCampOfferingComponent>(lodge), Is.True,
                    "a Lodge pitched under the rule takes offerings");

                body = Spawn(entMan, spawned, Human, map.GridCoords);
                var otherBody = Spawn(entMan, spawned, Human, map.GridCoords);

                // an unprepared trophy is refused
                curedHide = Trophy(entMan, spawned, Hide, map.GridCoords, body);
                Assert.That(camp.CanOfferTrophy(curedHide, out var reason), Is.False);
                Assert.That(reason, Is.EqualTo("cmu-bad-blood-offering-unprepared"));
                Assert.That(camp.TryOfferTrophy(party.Leader, curedHide), Is.False);
                Assert.That(rules.GetSp(), Is.EqualTo(FixedPoint4.Zero));

                // a cured hide is accepted
                entMan.GetComponent<YautjaTrophyComponent>(curedHide).Polished = true;
                Assert.That(camp.TryOfferTrophy(party.Grunts[0], curedHide), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(rules.GetSp(), Is.EqualTo(FixedPoint4.New(1)));
                    Assert.That(rules.GetOfferings(body), Is.EqualTo(1));
                    Assert.That(camp.GetOfferedTrophyCount(), Is.EqualTo(1));
                    Assert.That(entMan.GetComponent<TransformComponent>(curedHide).ParentUid, Is.EqualTo(lodge),
                        "the offered trophy hangs on the Lodge");
                    Assert.That(camp.TryOfferTrophy(party.Leader, curedHide), Is.False,
                        "a trophy is only paid for once");
                });

                // an honourable hunter gets nothing out of the Lodge
                var hunter = Spawn(entMan, spawned, "CMUMobYautja", map.GridCoords);
                var hunterBone = Trophy(entMan, spawned, Bone, map.GridCoords, otherBody, polished: true);
                Assert.That(camp.TryOfferTrophy(hunter, hunterBone), Is.False);

                // refused whatever it is: Yautja, synth and larva origin, a part cut off the living. whether a player
                // ever held the body no longer matters
                foreach (var (yautja, synth, larva, player, kill) in new[]
                         {
                             (true, false, false, true, true),
                             (false, true, false, true, true),
                             (false, false, true, true, true),
                             (false, false, false, true, false),
                             (false, false, false, false, false),
                         })
                {
                    var unworthy = Trophy(entMan, spawned, Bone, map.GridCoords, otherBody, polished: true,
                        yautja: yautja, synth: synth, larva: larva, player: player, kill: kill);
                    Assert.That(camp.CanOfferTrophy(unworthy, out var why), Is.False,
                        $"yautja {yautja}, synth {synth}, larva {larva}, player {player}, kill {kill}");
                    Assert.That(why, Is.EqualTo("cmu-bad-blood-offering-unworthy"));
                    Assert.That(camp.TryOfferTrophy(party.Leader, unworthy), Is.False);
                }

                // no stamp, no offering
                var unstamped = Spawn(entMan, spawned, Bone, map.GridCoords);
                entMan.GetComponent<YautjaTrophyComponent>(unstamped).Polished = true;
                Assert.That(camp.TryOfferTrophy(party.Leader, unstamped), Is.False);

                // a stack cannot be offered, however it was put together
                var stack = Trophy(entMan, spawned, Hide, map.GridCoords, otherBody, polished: true);
                stacks.SetCount(stack, 2);
                Assert.That(camp.CanOfferTrophy(stack, out var stackReason), Is.False);
                Assert.That(stackReason, Is.EqualTo("cmu-bad-blood-offering-stack"));

                // a xeno trophy is accepted
                xenoSkull = Trophy(entMan, spawned, XenoSkull, map.GridCoords, otherBody, polished: true, xeno: true);
                Assert.That(camp.TryOfferTrophy(party.Leader, xenoSkull), Is.True);
                Assert.That(rules.GetSp(), Is.EqualTo(FixedPoint4.New(2)));

                // the body's second trophy goes through the real interaction
                bone = Trophy(entMan, spawned, Bone, map.GridCoords, body, polished: true);
                var use = new InteractUsingEvent(party.Leader, bone, lodge, entMan.GetComponent<TransformComponent>(lodge).Coordinates);
                entMan.EventBus.RaiseLocalEvent(lodge, use);
                Assert.That(use.Handled, Is.True);
                Assert.That(rules.GetSp(), Is.EqualTo(FixedPoint4.New(2)), "nothing is paid before the do-after ends");
            });

            await Pair.RunSeconds(4);

            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var camp = Server.System<BadBloodCampSystem>();
                var rules = Server.System<BadBloodCampRuleSystem>();

                Assert.Multiple(() =>
                {
                    Assert.That(rules.GetSp(), Is.EqualTo(FixedPoint4.New(3)));
                    Assert.That(rules.GetOfferings(body), Is.EqualTo(2));
                    Assert.That(entMan.GetComponent<TransformComponent>(bone).ParentUid, Is.EqualTo(lodge));
                });

                // a third trophy from the same body is refused
                var third = Trophy(entMan, spawned, Skull, map.GridCoords, body, polished: true);
                Assert.That(camp.CanOfferTrophy(third, out var reason), Is.False);
                Assert.That(reason, Is.EqualTo("cmu-bad-blood-offering-body-spent"));
                Assert.That(camp.TryOfferTrophy(party.Leader, third), Is.False);
                Assert.That(rules.GetSp(), Is.EqualTo(FixedPoint4.New(3)));

                Assert.That(Server.System<SharedDestructibleSystem>().DestroyEntity(lodge), Is.True);
            });

            await Pair.RunSeconds(2);

            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var rules = Server.System<BadBloodCampRuleSystem>();

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.EntityExists(lodge), Is.False);
                    foreach (var trophy in new[] { curedHide, bone, xenoSkull })
                    {
                        Assert.That(entMan.EntityExists(trophy), Is.True, "the trophies spill out of a destroyed Lodge");
                        Assert.That(entMan.HasComponent<BadBloodOfferedTrophyComponent>(trophy), Is.True,
                            "a spilled trophy cannot be offered again at the next Lodge");
                    }

                    Assert.That(rules.GetSp(), Is.EqualTo(FixedPoint4.New(3)), "the clan keeps what it was paid");
                    Assert.That(rules.GetOfferings(body), Is.EqualTo(2), "the ledger outlives the Lodge");
                });
            });
        }
        finally
        {
            await BadBloodTestHelpers.EndBadBloodRule(Pair, party);
            await DeleteAll(spawned);
        }
    }

    // the reviewed gap: stacking must not hide whose a hide was or pass raw hide off as cured
    [Test]
    public async Task StampedHidesOnlyStackWithUncuredHideOffTheSameBody()
    {
        var map = await Pair.CreateTestMap();
        var spawned = new List<EntityUid>();

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var stacks = Server.System<SharedStackSystem>();

                var bodyA = Spawn(entMan, spawned, Human, map.GridCoords);
                var bodyB = Spawn(entMan, spawned, Human, map.GridCoords);

                var first = Trophy(entMan, spawned, Hide, map.GridCoords, bodyA);
                var second = Trophy(entMan, spawned, Hide, map.GridCoords, bodyA);
                var other = Trophy(entMan, spawned, Hide, map.GridCoords, bodyB);
                var cured = Trophy(entMan, spawned, Hide, map.GridCoords, bodyA, polished: true);
                var plain = Spawn(entMan, spawned, Hide, map.GridCoords);
                var plainToo = Spawn(entMan, spawned, Hide, map.GridCoords);
                var plainThree = Spawn(entMan, spawned, Hide, map.GridCoords);

                Assert.Multiple(() =>
                {
                    Assert.That(stacks.TryMergeStacks(other, first, out _), Is.False,
                        "hides off different bodies must not merge");
                    Assert.That(stacks.TryMergeStacks(first, cured, out _), Is.False,
                        "raw hide must not join a cured hide");
                    Assert.That(stacks.TryMergeStacks(cured, first, out _), Is.False,
                        "a cured hide must not join raw hide");
                    Assert.That(stacks.TryMergeStacks(plain, first, out _), Is.False,
                        "unstamped hide must not join a stamped one");
                    Assert.That(stacks.TryMergeStacks(first, plain, out _), Is.False,
                        "a stamped hide must not join an unstamped one");
                });

                Assert.That(stacks.TryMergeStacks(second, first, out var moved), Is.True,
                    "raw hide off the same body still stacks");
                Assert.That(moved, Is.EqualTo(1));
                Assert.That(stacks.GetCount(first), Is.EqualTo(2));
                Assert.That(entMan.GetComponent<BadBloodTrophyOriginComponent>(first).Body, Is.EqualTo(bodyA));

                Assert.That(stacks.TryMergeStacks(plainToo, plainThree, out _), Is.True,
                    "hide with no stamp stacks the way it always did");
                Assert.That(stacks.GetCount(plainThree), Is.EqualTo(2));
            });
        }
        finally
        {
            await DeleteAll(spawned);
        }
    }

    private static EntityUid Spawn(IEntityManager entMan, List<EntityUid> spawned, string prototype, EntityCoordinates coords)
    {
        var uid = entMan.SpawnEntity(prototype, coords);
        spawned.Add(uid);
        return uid;
    }

    private static EntityUid Trophy(
        IEntityManager entMan,
        List<EntityUid> spawned,
        string prototype,
        EntityCoordinates coords,
        EntityUid body,
        bool polished = false,
        bool yautja = false,
        bool synth = false,
        bool xeno = false,
        bool player = true,
        bool kill = true,
        bool larva = false)
    {
        var trophy = Spawn(entMan, spawned, prototype, coords);
        entMan.GetComponent<YautjaTrophyComponent>(trophy).Polished = polished;

        var origin = entMan.EnsureComponent<BadBloodTrophyOriginComponent>(trophy);
        origin.Body = body;
        origin.WasPlayer = player;
        origin.WasKill = kill;
        origin.IsYautja = yautja;
        origin.IsSynth = synth;
        origin.IsXeno = xeno;
        origin.IsLarva = larva;
        return trophy;
    }

    private async Task DeleteAll(List<EntityUid> spawned)
    {
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
