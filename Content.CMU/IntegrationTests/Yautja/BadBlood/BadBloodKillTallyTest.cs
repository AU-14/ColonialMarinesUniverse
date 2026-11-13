using Content.Server.CMU14.Yautja.BadBlood;
using Content.Shared._RMC14.Synth;
using Content.Shared.Mobs;
using Robust.Shared.GameObjects;
using Robust.UnitTesting;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// STATE-105: the ascension tie-break only counts kills that would earn Superiority Points
[TestFixture]
public sealed class BadBloodKillTallyTest
{
    [Test]
    public async Task OnlyWorthyPreyKillsAreTallied()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var mobState = entMan.System<MobStateSystem>();
            var killer = entMan.SpawnEntity(BadBloodTestHelpers.GruntPrototype, map.GridCoords);

            int Kills() => entMan.TryGetComponent(killer, out BadBloodKillTallyComponent? tally) ? tally.Kills : 0;

            var npc = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            mobState.ChangeMobState(npc, MobState.Dead, origin: killer);
            Assert.That(Kills(), Is.EqualTo(1), "A human no player ever held is prey all the same.");

            var synth = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));
            entMan.EnsureComponent<SynthComponent>(synth);
            GiveMind(server, synth);
            mobState.ChangeMobState(synth, MobState.Dead, origin: killer);
            Assert.That(Kills(), Is.EqualTo(1), "A synth is not a kill.");

            var badBlood = entMan.SpawnEntity(BadBloodTestHelpers.GruntPrototype, map.GridCoords.Offset(new Vector2(3, 0)));
            GiveMind(server, badBlood);
            mobState.ChangeMobState(badBlood, MobState.Dead, origin: killer);
            Assert.That(Kills(), Is.EqualTo(1), "Killing another Bad Blood must not raise the tally.");

            var marine = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(4, 0)));
            GiveMind(server, marine);
            mobState.ChangeMobState(marine, MobState.Dead, origin: killer);
            Assert.That(Kills(), Is.EqualTo(2), "A player marine is a kill.");

            var xeno = entMan.SpawnEntity("CMXenoDrone", map.GridCoords.Offset(new Vector2(5, 0)));
            mobState.ChangeMobState(xeno, MobState.Dead, origin: killer);
            Assert.That(Kills(), Is.EqualTo(3), "A xeno is a kill, with or without a player.");

            var larva = entMan.SpawnEntity("CMXenoLarva", map.GridCoords.Offset(new Vector2(6, 0)));
            mobState.ChangeMobState(larva, MobState.Dead, origin: killer);
            Assert.That(Kills(), Is.EqualTo(3), "A larva is not a kill.");

            // the same rule for hunters who are not Bad Bloods: they keep no tally at all
            var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords.Offset(new Vector2(0, 2)));
            var second = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 2)));
            GiveMind(server, second);
            mobState.ChangeMobState(second, MobState.Dead, origin: hunter);
            Assert.That(entMan.HasComponent<BadBloodKillTallyComponent>(hunter), Is.False);
        });

        await pair.CleanReturnAsync();
    }

    // a mind with no player behind it. call on the server thread
    private static void GiveMind(RobustIntegrationTest.ServerIntegrationInstance server, EntityUid body)
    {
        var minds = server.System<SharedMindSystem>();
        minds.TransferTo(minds.CreateMind(null, "prey"), body);
    }
}
