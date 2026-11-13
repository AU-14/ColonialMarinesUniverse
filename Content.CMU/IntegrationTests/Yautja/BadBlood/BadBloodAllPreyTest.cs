using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Yautja.BadBlood;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Synth;
using Content.Shared.CMU14.Yautja;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.NPC.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// FLOW-301: the Lodge pays for all prey. Any human or xeno body, of any faction and with or without a player behind
// it, gives trophies the Lodge takes. Yautja, synths, larvae and cuts off the living are still refused, and a body
// still gives two trophies at most.
[TestFixture]
public sealed class BadBloodAllPreyTest : GameTest
{
    private const string Lodge = "CMUBadBloodCamp";
    private const string Human = "CMMobHuman";
    private const string Xeno = "CMXenoDrone";
    private const string Larva = "CMXenoLarva";
    private const string Hunter = "CMUMobYautja";
    private const string Bone = "CMUYautjaHumanLeftArmBoneTrophy";
    private const string Skull = "CMUYautjaHumanSkullTrophy";
    private const string Hide = "CMUYautjaHumanHide";
    private const string XenoSkull = "CMUYautjaXenoSkullTrophy";
    private const string InsurgentFaction = "CLF";

    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task TrophiesFromEveryKindOfPreyAreTakenAndPaid()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        var spawned = new List<EntityUid>();

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var camp = Server.System<BadBloodCampSystem>();
                var rules = Server.System<BadBloodCampRuleSystem>();
                var provenance = Server.System<BadBloodProvenanceSystem>();
                var mobState = Server.System<MobStateSystem>();
                var factions = Server.System<NpcFactionSystem>();
                Assert.That(rules.TryGetRule(out var rule), Is.True);

                spawned.Add(entMan.SpawnEntity(Lodge, map.GridCoords));
                Assert.That(rules.HasLodge(), Is.True);

                EntityUid Body(string prototype, int column)
                {
                    var body = entMan.SpawnEntity(prototype, map.GridCoords.Offset(new Vector2(column, 2)));
                    spawned.Add(body);
                    return body;
                }

                // a trophy stamped the way every harvest path stamps it, then prepared
                EntityUid Trophy(string prototype, EntityUid body)
                {
                    var trophy = entMan.SpawnEntity(prototype, map.GridCoords);
                    spawned.Add(trophy);
                    provenance.StampTrophy(trophy, body);
                    entMan.GetComponent<YautjaTrophyComponent>(trophy).Polished = true;
                    return trophy;
                }

                // a marine with a player behind it
                var marine = Body(Human, 0);
                entMan.EnsureComponent<MarineComponent>(marine);
                GiveMind(marine);

                // a human of another faction, also played
                var insurgent = Body(Human, 1);
                factions.ClearFactions(insurgent, false);
                factions.AddFaction(insurgent, InsurgentFaction);
                GiveMind(insurgent);

                // a human nobody ever played: a colonist, a survivor, an admin-spawned body
                var mindless = Body(Human, 2);

                // a xeno nobody ever played
                var xeno = Body(Xeno, 3);

                var prey = new[]
                {
                    (marine, Bone, "a marine"),
                    (insurgent, Bone, "a human of another faction"),
                    (mindless, Bone, "a human no player ever held"),
                    (xeno, XenoSkull, "a xeno"),
                };

                foreach (var (body, _, what) in prey)
                {
                    mobState.ChangeMobState(body, MobState.Dead);
                    Assert.That(provenance.IsWorthyPrey(body), Is.True, $"{what} is prey");
                }

                var expected = rules.GetSp();
                foreach (var (body, prototype, what) in prey)
                {
                    var trophy = Trophy(prototype, body);
                    Assert.That(camp.CanOfferTrophy(trophy, out var reason), Is.True, $"{what}: refused with {reason}");
                    Assert.That(camp.TryOfferTrophy(party.Leader, trophy), Is.True, $"the Lodge takes a trophy off {what}");

                    expected += rule.Comp.OfferingSp;
                    Assert.That(rules.GetSp(), Is.EqualTo(expected), $"a trophy off {what} pays the clan");
                    Assert.That(rules.GetOfferings(body), Is.EqualTo(1));
                }

                Assert.That(rule.Comp.OfferingSp, Is.EqualTo(FixedPoint4.New(1)), "a trophy is worth 1 SP");

                // the cap of two a body still holds, for prey without a player as for any other
                Assert.That(camp.TryOfferTrophy(party.Grunts[0], Trophy(Hide, mindless)), Is.True);
                expected += rule.Comp.OfferingSp;
                var third = Trophy(Skull, mindless);
                Assert.Multiple(() =>
                {
                    Assert.That(rules.GetOfferings(mindless), Is.EqualTo(2));
                    Assert.That(camp.CanOfferTrophy(third, out var spent), Is.False);
                    Assert.That(spent, Is.EqualTo("cmu-bad-blood-offering-body-spent"));
                    Assert.That(camp.TryOfferTrophy(party.Leader, third), Is.False);
                    Assert.That(rules.GetSp(), Is.EqualTo(expected), "a third trophy off one body pays nothing");
                });

                // still refused: a Yautja, a synth, a larva, and anything cut off the living
                var hunter = Body(Hunter, 4);
                var synth = Body(Human, 5);
                entMan.EnsureComponent<SynthComponent>(synth);
                GiveMind(synth);
                var larva = Body(Larva, 6);
                var living = Body(Human, 7);
                GiveMind(living);

                foreach (var body in new[] { hunter, synth, larva })
                {
                    mobState.ChangeMobState(body, MobState.Dead);
                }

                foreach (var (body, what) in new[]
                         {
                             (hunter, "a Yautja"),
                             (synth, "a synth"),
                             (larva, "a larva"),
                             (living, "a living body"),
                         })
                {
                    var trophy = Trophy(Bone, body);
                    Assert.Multiple(() =>
                    {
                        Assert.That(camp.CanOfferTrophy(trophy, out var why), Is.False, $"a trophy off {what} is refused");
                        Assert.That(why, Is.EqualTo("cmu-bad-blood-offering-unworthy"));
                        Assert.That(camp.TryOfferTrophy(party.Leader, trophy), Is.False);
                    });
                }

                Assert.Multiple(() =>
                {
                    Assert.That(provenance.IsWorthyPrey(hunter), Is.False);
                    Assert.That(provenance.IsWorthyPrey(synth), Is.False);
                    Assert.That(provenance.IsWorthyPrey(larva), Is.False);
                    Assert.That(rules.GetSp(), Is.EqualTo(expected), "nothing refused was paid for");
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

    // a mind with no player behind it. call on the server thread
    private void GiveMind(EntityUid body)
    {
        var minds = Server.System<SharedMindSystem>();
        minds.TransferTo(minds.CreateMind(null, "prey"), body);
    }
}
