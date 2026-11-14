using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Yautja.BadBlood;
using Content.Shared._RMC14.Campfire;
using Content.Shared._RMC14.Synth;
using Content.Shared.Body;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Tag;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// FLOW-110: the Lodge spit turns prey into the food Bad Bloods eat and pays the clan once per body
[TestFixture]
public sealed class CampSpitTest : GameTest
{
    private const string Spit = "CMUBadBloodCampSpit";
    private const string Human = "CMMobHuman";
    private const string Hunter = "CMUMobYautja";
    private const string RawHuman = "FoodMeatHuman";
    private const string CookedHuman = "CMUFoodMeatPreyHumanCooked";

    private static readonly ProtoId<TagPrototype> PreyCookedTag = "CMUYautjaPreyCooked";

    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task ACorpseCooksIntoEdiblePreyAndCreditsOnce()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        var spawned = new List<EntityUid>();

        EntityUid spit = default;
        EntityUid prey = default;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var camp = Server.System<BadBloodCampSystem>();
                var rules = Server.System<BadBloodCampRuleSystem>();

                spit = Spawn(entMan, spawned, Spit, map.GridCoords);
                var cook = new Entity<BadBloodCampCookComponent>(spit, entMan.GetComponent<BadBloodCampCookComponent>(spit));
                Assert.That(camp.IsSpitLit(spit), Is.True, "the Lodge fire burns from the start");

                prey = Spawn(entMan, spawned, Human, map.GridCoords);
                GivePlayerMind(prey);

                Assert.That(camp.CanCookCorpse(cook, prey, out var alive), Is.False, "living prey does not go on the spit");
                Assert.That(alive, Is.EqualTo("cmu-bad-blood-spit-not-dead"));

                Server.System<MobStateSystem>().ChangeMobState(prey, MobState.Dead);
                Assert.That(camp.CanCookCorpse(cook, prey, out _), Is.True);
                Assert.That(rules.GetCookRemaining(prey), Is.EqualTo(FixedPoint4.New(3)));

                Assert.That(camp.TryStartCookCorpse(cook, party.Leader, prey), Is.True);
                Assert.That(entMan.GetComponent<TransformComponent>(prey).ParentUid, Is.EqualTo(spit),
                    "the corpse is on the spit while it cooks");
                Assert.That(rules.GetSp(), Is.EqualTo(FixedPoint4.Zero), "nothing is paid before the corpse is cooked");

                var second = Spawn(entMan, spawned, Human, map.GridCoords);
                Server.System<MobStateSystem>().ChangeMobState(second, MobState.Dead);
                Assert.That(camp.TryStartCookCorpse(cook, party.Leader, second), Is.False, "one corpse at a time");
            });

            await Pair.RunSeconds(27);

            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var camp = Server.System<BadBloodCampSystem>();
                var rules = Server.System<BadBloodCampRuleSystem>();
                var cook = new Entity<BadBloodCampCookComponent>(spit, entMan.GetComponent<BadBloodCampCookComponent>(spit));

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.EntityExists(prey), Is.False, "the fire takes the body");
                    Assert.That(rules.GetSp(), Is.EqualTo(FixedPoint4.New(3)));
                    Assert.That(rules.GetCookRemaining(prey), Is.EqualTo(FixedPoint4.Zero));
                });

                var cuts = FindAll(entMan, CookedHuman);
                spawned.AddRange(cuts);
                Assert.That(cuts, Has.Count.InRange(4, 5), "a human corpse gives 4 to 5 cooked cuts");

                // the output is edible by a Bad Blood
                var body = entMan.System<BodySystem>();
                var stomachs = body.EnumerateOrgans<StomachComponent>(party.Leader).ToList();
                Assert.That(stomachs, Has.Count.EqualTo(1));
                var stomach = new Entity<StomachComponent>(stomachs[0].Owner, stomachs[0].Comp2);
                foreach (var cut in cuts)
                {
                    Assert.That(entMan.System<TagSystem>().HasTag(cut, PreyCookedTag), Is.True);
                    Assert.That(entMan.System<IngestionSystem>().IsDigestibleBy(cut, stomach), Is.True,
                        "a Bad Blood must be able to eat what comes off the spit");
                }

                // a cut butchered off the same body earlier still cooks, but the body has paid already
                var leftover = Meat(entMan, spawned, map.GridCoords, prey);
                Assert.That(camp.TryCookMeat(cook, party.Leader, leftover), Is.True);
                Assert.That(rules.GetSp(), Is.EqualTo(FixedPoint4.New(3)), "a corpse credits once");
            });
        }
        finally
        {
            await BadBloodTestHelpers.EndBadBloodRule(Pair, party);
            await DeleteAll(spawned);
        }
    }

    [Test]
    public async Task YautjaAndSynthCorpsesAreRefused()
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
                var mobState = Server.System<MobStateSystem>();

                var spit = Spawn(entMan, spawned, Spit, map.GridCoords);
                var cook = new Entity<BadBloodCampCookComponent>(spit, entMan.GetComponent<BadBloodCampCookComponent>(spit));

                var hunter = Spawn(entMan, spawned, Hunter, map.GridCoords);
                GivePlayerMind(hunter);
                mobState.ChangeMobState(hunter, MobState.Dead);

                var synth = Spawn(entMan, spawned, Human, map.GridCoords);
                entMan.EnsureComponent<SynthComponent>(synth);
                GivePlayerMind(synth);
                mobState.ChangeMobState(synth, MobState.Dead);

                var prey = Spawn(entMan, spawned, Human, map.GridCoords);
                GivePlayerMind(prey);
                mobState.ChangeMobState(prey, MobState.Dead);

                Assert.Multiple(() =>
                {
                    Assert.That(camp.CanCookCorpse(cook, hunter, out var hunterReason), Is.False);
                    Assert.That(hunterReason, Is.EqualTo("cmu-bad-blood-spit-refuse-yautja"));
                    Assert.That(camp.TryStartCookCorpse(cook, party.Leader, hunter), Is.False);

                    Assert.That(camp.CanCookCorpse(cook, synth, out var synthReason), Is.False);
                    Assert.That(synthReason, Is.EqualTo("cmu-bad-blood-spit-refuse-synth"));
                    Assert.That(camp.TryStartCookCorpse(cook, party.Leader, synth), Is.False);

                    Assert.That(entMan.GetComponent<TransformComponent>(hunter).ParentUid, Is.Not.EqualTo(spit));
                    Assert.That(entMan.GetComponent<TransformComponent>(synth).ParentUid, Is.Not.EqualTo(spit));
                });

                // nobody but a Bad Blood works the spit
                var marine = Spawn(entMan, spawned, Human, map.GridCoords);
                Assert.That(camp.TryStartCookCorpse(cook, marine, prey), Is.False);

                // and not over a dead fire
                Server.System<SharedCampfireSystem>().SetLit((spit, entMan.GetComponent<CampfireComponent>(spit)), false);
                Assert.That(camp.CanCookCorpse(cook, prey, out var unlit), Is.False);
                Assert.That(unlit, Is.EqualTo("cmu-bad-blood-spit-unlit"));

                Assert.That(rules.GetSp(), Is.EqualTo(FixedPoint4.Zero));
            });
        }
        finally
        {
            await BadBloodTestHelpers.EndBadBloodRule(Pair, party);
            await DeleteAll(spawned);
        }
    }

    [Test]
    public async Task LooseMeatSharesTheBodysCookCap()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        var spawned = new List<EntityUid>();

        EntityUid spit = default;
        EntityUid body = default;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                spit = Spawn(entMan, spawned, Spit, map.GridCoords);
                body = Spawn(entMan, spawned, Human, map.GridCoords);

                // the first cut goes through the real interaction
                var meat = Meat(entMan, spawned, map.GridCoords, body);
                var use = new InteractUsingEvent(party.Leader, meat, spit, entMan.GetComponent<TransformComponent>(spit).Coordinates);
                entMan.EventBus.RaiseLocalEvent(spit, use);
                Assert.That(use.Handled, Is.True);

                // the spit has no use for anything else, and a marine gets nothing out of it
                var junk = Spawn(entMan, spawned, "CMUYautjaSmartDisc", map.GridCoords);
                var junkUse = new InteractUsingEvent(party.Leader, junk, spit, entMan.GetComponent<TransformComponent>(spit).Coordinates);
                entMan.EventBus.RaiseLocalEvent(spit, junkUse);
                Assert.That(junkUse.Handled, Is.False);

                var marine = Spawn(entMan, spawned, Human, map.GridCoords);
                var marineMeat = Meat(entMan, spawned, map.GridCoords, body);
                var marineUse = new InteractUsingEvent(marine, marineMeat, spit, entMan.GetComponent<TransformComponent>(spit).Coordinates);
                entMan.EventBus.RaiseLocalEvent(spit, marineUse);
                Assert.That(marineUse.Handled, Is.False);
            });

            await Pair.RunSeconds(5);

            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var camp = Server.System<BadBloodCampSystem>();
                var rules = Server.System<BadBloodCampRuleSystem>();
                var cook = new Entity<BadBloodCampCookComponent>(spit, entMan.GetComponent<BadBloodCampCookComponent>(spit));

                Assert.That(rules.GetSp(), Is.EqualTo(FixedPoint4.New(0.5)), "one cut of killed prey pays half a point");

                // five more cuts fill the body's cap of 3
                for (var i = 0; i < 5; i++)
                {
                    Assert.That(camp.TryCookMeat(cook, party.Leader, Meat(entMan, spawned, map.GridCoords, body)), Is.True);
                }

                Assert.That(rules.GetSp(), Is.EqualTo(FixedPoint4.New(3)));
                Assert.That(rules.GetCookRemaining(body), Is.EqualTo(FixedPoint4.Zero));

                // the seventh still cooks, for nothing
                Assert.That(camp.TryCookMeat(cook, party.Leader, Meat(entMan, spawned, map.GridCoords, body)), Is.True);
                Assert.That(rules.GetSp(), Is.EqualTo(FixedPoint4.New(3)), "the loose-meat cap holds");

                // meat nobody can vouch for cooks and pays nothing
                var unstamped = Spawn(entMan, spawned, RawHuman, map.GridCoords);
                Assert.That(camp.TryCookMeat(cook, party.Leader, unstamped), Is.True);

                Assert.That(rules.GetSp(), Is.EqualTo(FixedPoint4.New(3)));

                // a cut off prey no player ever held pays like any other
                var npc = Meat(entMan, spawned, map.GridCoords, Spawn(entMan, spawned, Human, map.GridCoords), player: false);
                Assert.That(camp.TryCookMeat(cook, party.Leader, npc), Is.True);
                Assert.That(rules.GetSp(), Is.EqualTo(FixedPoint4.New(3.5)), "mindless prey pays too");

                // a Yautja's flesh does not go on the fire at all
                var kin = Meat(entMan, spawned, map.GridCoords, Spawn(entMan, spawned, Human, map.GridCoords), yautja: true);
                Assert.That(camp.CanCookMeat(cook, kin, out var reason), Is.False);
                Assert.That(reason, Is.EqualTo("cmu-bad-blood-spit-refuse-yautja"));
                Assert.That(camp.TryCookMeat(cook, party.Leader, kin), Is.False);

                spawned.AddRange(FindAll(entMan, CookedHuman));
            });
        }
        finally
        {
            await BadBloodTestHelpers.EndBadBloodRule(Pair, party);
            await DeleteAll(spawned);
        }
    }

    private void GivePlayerMind(EntityUid body)
    {
        var minds = Server.System<SharedMindSystem>();
        var mind = minds.CreateMind(null, "prey");
        minds.TransferTo(mind, body);
    }

    private static EntityUid Spawn(IEntityManager entMan, List<EntityUid> spawned, string prototype, EntityCoordinates coords)
    {
        var uid = entMan.SpawnEntity(prototype, coords);
        spawned.Add(uid);
        return uid;
    }

    private static EntityUid Meat(
        IEntityManager entMan,
        List<EntityUid> spawned,
        EntityCoordinates coords,
        EntityUid body,
        bool player = true,
        bool yautja = false)
    {
        var meat = Spawn(entMan, spawned, RawHuman, coords);
        var origin = entMan.EnsureComponent<BadBloodMeatOriginComponent>(meat);
        origin.Body = body;
        origin.WasPlayer = player;
        origin.WasKill = true;
        origin.IsYautja = yautja;
        return meat;
    }

    private static List<EntityUid> FindAll(IEntityManager entMan, string prototype)
    {
        var found = new List<EntityUid>();
        var query = entMan.AllEntityQueryEnumerator<MetaDataComponent>();
        while (query.MoveNext(out var uid, out var meta))
        {
            if (meta.EntityPrototype?.ID == prototype && meta.EntityLifeStage < EntityLifeStage.Terminating)
                found.Add(uid);
        }

        return found;
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
