using System.Reflection;
using Content.Server.CMU14.Yautja;
using Content.Shared._RMC14.UniformAccessories;
using Content.Shared._RMC14.Vendors;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Interaction;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// STATE-104 and ECS-109: Bad Bloods bank no honour, and the new trophy kinds give honourable hunters nothing new
// and take nothing away from the scalp
[TestFixture]
public sealed class BadBloodHonourGateTest
{
    private const string Rag = "CMUYautjaPolishingRag";
    private const string Scalp = "CMUYautjaScalp";
    private const string Hunter = "CMUMobYautja";

    [Test]
    public async Task HonourBankLeavesOutBadBloods()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hunterUser = new NetUserId(Guid.NewGuid());
            var badBloodUser = new NetUserId(Guid.NewGuid());
            var hunter = SpawnScorer(server, Hunter, map.GridCoords, hunterUser);
            var badBlood = SpawnScorer(server, BadBloodTestHelpers.GruntPrototype, map.GridCoords, badBloodUser);

            var scores = entMan.System<YautjaHonorBankSystem>().CollectScores();
            Assert.Multiple(() =>
            {
                Assert.That(scores.GetValueOrDefault(hunterUser), Is.EqualTo(5),
                    "an honourable hunter's score must reach the bank");
                Assert.That(scores.ContainsKey(badBloodUser), Is.False,
                    "a Bad Blood's score must never reach the bank");
            });

            entMan.DeleteEntity(hunter);
            entMan.DeleteEntity(badBlood);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PolishingASpineOrTorsoPaysNoHonourAndAScalpStillPays()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        var prototypes = new[] { "CMUYautjaHumanSpine", "CMUYautjaHumanTorso", Scalp };
        var hunters = new List<EntityUid>();
        var trophies = new List<EntityUid>();
        var before = new List<int>();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            for (var i = 0; i < prototypes.Length; i++)
            {
                // one polisher each, a do-after belongs to its user
                var coords = map.GridCoords.Offset(new Vector2(i * 2, 0));
                var hunter = entMan.SpawnEntity(Hunter, coords);
                var trophy = entMan.SpawnEntity(prototypes[i], coords);
                hunters.Add(hunter);
                trophies.Add(trophy);
                before.Add(Score(entMan, hunter));
                Assert.That(Use(entMan, hunter, entMan.SpawnEntity(Rag, coords), trophy), Is.True);
            }
        });

        await pair.RunTicksSync(pair.SecondsToTicks(5.5f));

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Assert.Multiple(() =>
            {
                for (var i = 0; i < prototypes.Length; i++)
                {
                    Assert.That(entMan.GetComponent<YautjaTrophyComponent>(trophies[i]).Polished, Is.True,
                        $"{prototypes[i]} must polish.");
                }

                Assert.That(Score(entMan, hunters[0]), Is.EqualTo(before[0]), "a polished spine must pay no honour");
                Assert.That(Score(entMan, hunters[1]), Is.EqualTo(before[1]), "a polished torso must pay no honour");
                Assert.That(Score(entMan, hunters[2]), Is.EqualTo(before[2] + 1),
                    "a hunter polishing a scalp must still earn the point it always did");
            });

            foreach (var uid in hunters.Concat(trophies))
                entMan.DeleteEntity(uid);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ScalpStillMountsOnTheGlaiveAndKeepsItsAccessoryGate()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hunter = entMan.SpawnEntity(Hunter, map.GridCoords);
            var human = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var glaive = entMan.SpawnEntity("CMUYautjaCleavingGlaive", map.GridCoords);
            var mounted = entMan.SpawnEntity(Scalp, map.GridCoords);
            var worn = entMan.SpawnEntity(Scalp, map.GridCoords);

            Assert.That(Use(entMan, hunter, mounted, glaive), Is.True);
            Assert.That(entMan.GetComponent<YautjaCleavingGlaiveComponent>(glaive).SkullAttached, Is.True,
                "a scalp must still mount on the cleaving glaive");

            var byHuman = new UniformAccessoryInsertAttemptEvent(human, human);
            entMan.EventBus.RaiseLocalEvent(worn, byHuman);
            var byHunter = new UniformAccessoryInsertAttemptEvent(hunter, hunter);
            entMan.EventBus.RaiseLocalEvent(worn, byHunter);
            Assert.Multiple(() =>
            {
                Assert.That(byHuman.Cancelled, Is.True, "a scalp must stay a hunter-only accessory");
                Assert.That(byHunter.Cancelled, Is.False);
            });

            foreach (var uid in new[] { hunter, human, glaive, worn })
                entMan.DeleteEntity(uid);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task StartingHuntingKitCarriesAWaterFlask()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var bundle = server.ProtoMan.Index<EntityPrototype>("CMUYautjaBadBloodHuntingEquipmentBundle");
            Assert.That(bundle.TryGetComponent(out CMVendorBundleComponent? contents, server.EntMan.ComponentFactory),
                Is.True);
            Assert.That(contents!.Bundle.Select(id => id.Id), Does.Contain("CMUYautjaWaterFlask"),
                "a Bad Blood drinks only water, so the starting kit must carry a flask");
        });

        await pair.CleanReturnAsync();
    }

    // a body with a trophy score and a mind that remembers who owned it, the way a played hunter ends the round
    private static EntityUid SpawnScorer(
        Robust.UnitTesting.RobustIntegrationTest.ServerIntegrationInstance server,
        string prototype,
        EntityCoordinates coords,
        NetUserId user)
    {
        var entMan = server.EntMan;
        var body = entMan.SpawnEntity(prototype, coords);
        entMan.EnsureComponent<YautjaTrophyRecordComponent>(body).Score = 5;

        var mind = BadBloodTestHelpers.AttachThreatMind(server, body);
        // no session stands behind the id, and only the mind system may write it, so it is set the long way
        typeof(MindComponent).GetProperty("OriginalOwnerUserId", BindingFlags.Instance | BindingFlags.Public)!
            .SetValue(entMan.GetComponent<MindComponent>(mind), (NetUserId?) user);
        return body;
    }

    private static int Score(IEntityManager entMan, EntityUid uid)
    {
        return entMan.TryGetComponent(uid, out YautjaTrophyRecordComponent? record) ? record.Score : 0;
    }

    private static bool Use(IEntityManager entMan, EntityUid user, EntityUid used, EntityUid target)
    {
        var use = new InteractUsingEvent(user, used, target, entMan.GetComponent<TransformComponent>(target).Coordinates);
        entMan.EventBus.RaiseLocalEvent(target, use);
        return use.Handled;
    }
}
