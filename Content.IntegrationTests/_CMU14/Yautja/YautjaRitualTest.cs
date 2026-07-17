using System.Numerics;
using Content.Server._CMU14.Yautja;
using Content.Shared._CMU14.Yautja;
using Content.Shared.GameTicking;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;
using Robust.UnitTesting;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaRitualTest
{
    [Test]
    public async Task CaptiveTargetDeathClearsRitualWithoutDuelCredit()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid target = default;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var rituals = entMan.System<YautjaRitualSystem>();
                var mobState = entMan.System<MobStateSystem>();

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
                entMan.EnsureComponent<YautjaComponent>(hunter);

                Assert.That(rituals.TryClaimCaptive(hunter, target, bypassControlRequirement: true), Is.True);
                Assert.That(entMan.GetComponent<YautjaRitualDuelComponent>(target).State, Is.EqualTo(YautjaRitualState.Captive));

                mobState.ChangeMobState(target, MobState.Dead);
            });

            await server.WaitRunTicks(1);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;

                Assert.That(entMan.HasComponent<YautjaRitualDuelComponent>(target), Is.False,
                    "A captive dying before the duel starts should clear the local ritual state.");
                var record = entMan.GetComponent<YautjaTrophyRecordComponent>(hunter);
                Assert.That(record.RitualDuelWins, Is.Zero,
                    "Only active duel completions should grant ritual duel wins.");
                Assert.That(record.Score, Is.Zero,
                    "Only active duel completions should grant ritual duel trophy score.");
            });
        }
        finally
        {
            await Delete(server, hunter, target);
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ActiveDuelTargetDeathScoresExactlyOnce()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid target = default;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var rituals = entMan.System<YautjaRitualSystem>();
                var mobState = entMan.System<MobStateSystem>();

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
                entMan.EnsureComponent<YautjaComponent>(hunter);

                Assert.That(rituals.TryClaimCaptive(hunter, target, bypassControlRequirement: true), Is.True);
                Assert.That(rituals.TryBeginDuel(hunter, target), Is.True);

                mobState.ChangeMobState(target, MobState.Dead);
            });

            await server.WaitRunTicks(1);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var record = entMan.GetComponent<YautjaTrophyRecordComponent>(hunter);

                Assert.That(entMan.HasComponent<YautjaRitualDuelComponent>(target), Is.False,
                    "The duel marker should be removed after the target dies so later state churn cannot rescore it.");
                Assert.That(record.RitualDuelWins, Is.EqualTo(1));
                Assert.That(record.Score, Is.EqualTo(5));
            });

            await server.WaitPost(() =>
            {
                var mobState = server.EntMan.System<MobStateSystem>();
                mobState.ChangeMobState(target, MobState.Alive);
                mobState.ChangeMobState(target, MobState.Dead);
            });

            await server.WaitRunTicks(1);

            await server.WaitAssertion(() =>
            {
                var record = server.EntMan.GetComponent<YautjaTrophyRecordComponent>(hunter);

                Assert.That(record.RitualDuelWins, Is.EqualTo(1),
                    "A completed duel should not grant repeated credit after its ritual component is cleared.");
                Assert.That(record.Score, Is.EqualTo(5));
            });
        }
        finally
        {
            await Delete(server, hunter, target);
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RoundRestartClearsRitualDuelState()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid captive = default;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var rituals = entMan.System<YautjaRitualSystem>();

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                captive = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
                entMan.EnsureComponent<YautjaComponent>(hunter);

                Assert.That(rituals.TryClaimCaptive(hunter, captive, bypassControlRequirement: true), Is.True);

                entMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());
            });

            await server.WaitRunTicks(1);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;

                Assert.That(entMan.HasComponent<YautjaRitualDuelComponent>(captive), Is.False,
                    "Round cleanup should clear captive ritual state.");
            });
        }
        finally
        {
            await Delete(server, hunter, captive);
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HunterDeletionClearsRitualDuelStateLikeCmss13CleanData()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid replacementHunter = default;
        EntityUid captive = default;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var rituals = entMan.System<YautjaRitualSystem>();

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                replacementHunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
                captive = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));
                entMan.EnsureComponent<YautjaComponent>(hunter);
                entMan.EnsureComponent<YautjaComponent>(replacementHunter);

                Assert.That(rituals.TryClaimCaptive(hunter, captive, bypassControlRequirement: true), Is.True);

                entMan.DeleteEntity(hunter);
            });

            await server.WaitRunTicks(1);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var rituals = entMan.System<YautjaRitualSystem>();

                Assert.That(entMan.HasComponent<YautjaRitualDuelComponent>(captive), Is.False,
                    "CMSS13 carbon Destroy() calls huntdata.clean_data(); local hunter deletion should clear captive ritual links.");
                Assert.That(rituals.TryClaimCaptive(replacementHunter, captive, bypassControlRequirement: true), Is.True,
                    "A stale ritual component would block another living hunter from claiming the target after the original hunter is deleted.");
            });
        }
        finally
        {
            await Delete(server, hunter, replacementHunter, captive);
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RepeatedCaptiveClaimBySameHunterDoesNotCreateSecondRitual()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid firstCaptive = default;
        EntityUid secondCaptive = default;
        EntityUid otherHunter = default;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var rituals = entMan.System<YautjaRitualSystem>();

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                firstCaptive = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
                secondCaptive = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));
                otherHunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(3, 0)));
                entMan.EnsureComponent<YautjaComponent>(hunter);
                entMan.EnsureComponent<YautjaComponent>(otherHunter);

                Assert.That(rituals.TryClaimCaptive(hunter, firstCaptive, bypassControlRequirement: true), Is.True);
                Assert.That(rituals.TryClaimCaptive(hunter, secondCaptive, bypassControlRequirement: true), Is.False,
                    "Repeated ritual claims by the same hunter should not leave multiple active captive links.");
                Assert.That(entMan.HasComponent<YautjaRitualDuelComponent>(secondCaptive), Is.False);
                Assert.That(rituals.TryClaimCaptive(otherHunter, secondCaptive, bypassControlRequirement: true), Is.True,
                    "A rejected repeated claim must leave the second target available to another hunter.");
            });
        }
        finally
        {
            await Delete(server, hunter, firstCaptive, secondCaptive, otherHunter);
        }

        await pair.CleanReturnAsync();
    }

    private static async Task Delete(RobustIntegrationTest.ServerIntegrationInstance server, params EntityUid[] uids)
    {
        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            foreach (var uid in uids)
            {
                if (uid != default && !entMan.Deleted(uid))
                    entMan.DeleteEntity(uid);
            }
        });
    }
}
