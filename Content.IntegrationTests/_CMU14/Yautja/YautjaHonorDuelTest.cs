using System.Linq;
using System.Numerics;
using Content.Server.CMU14.Yautja;
using Content.Shared._RMC14.Dialog;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Verbs;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaHonorDuelTest
{
    // challenger: true means the challenger wins, false means the hunter who was called out wins
    [TestCase(true)]
    [TestCase(false)]
    public async Task HuntersCanChallengeEachOtherAndTheWinnerGetsDuelCredit(bool challengerWins)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var verbs = entMan.System<SharedVerbSystem>();
            var challenger = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            entMan.EnsureComponent<YautjaComponent>(challenger);
            entMan.EnsureComponent<YautjaComponent>(target);

            // used to fall through to the prey-only captive claim, which refuses yautja, so no verb at all
            var challenge = verbs.GetLocalVerbs(target, challenger, typeof(AlternativeVerb))
                .SingleOrDefault(verb => verb.Text == Loc.GetString("cmu-yautja-honor-duel-challenge-verb"));
            Assert.That(challenge, Is.Not.Null);
            challenge!.Act!.Invoke();

            Assert.That(entMan.HasComponent<YautjaRitualDuelComponent>(target), Is.False,
                "nothing starts until the challenged hunter accepts");
            entMan.EventBus.RaiseLocalEvent(target, new DialogConfirmBuiMsg
            {
                Actor = target,
                UiKey = DialogUiKey.Key,
            });

            var ritual = entMan.GetComponent<YautjaRitualDuelComponent>(target);
            Assert.Multiple(() =>
            {
                Assert.That(ritual.HonorDuel, Is.True);
                Assert.That(ritual.Hunter, Is.EqualTo(challenger));
                Assert.That(ritual.State, Is.EqualTo(YautjaRitualState.DuelActive));
            });

            var winner = challengerWins ? challenger : target;
            var loser = challengerWins ? target : challenger;
            entMan.System<MobStateSystem>().ChangeMobState(loser, MobState.Dead, null, winner);

            Assert.That(entMan.GetComponent<YautjaTrophyRecordComponent>(winner).RitualDuelWins, Is.EqualTo(1));
            Assert.That(entMan.TryGetComponent<YautjaTrophyRecordComponent>(loser, out var loserRecord) &&
                        loserRecord.RitualDuelWins > 0, Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task UnacceptedChallengeStartsNothingAndPreyStillUsesTheCaptiveRitual()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var verbs = entMan.System<SharedVerbSystem>();
            var ritual = entMan.System<YautjaRitualSystem>();
            var challenger = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            var prey = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(0, 1)));
            entMan.EnsureComponent<YautjaComponent>(challenger);
            entMan.EnsureComponent<YautjaComponent>(target);

            Assert.That(ritual.TryChallengeHonorDuel(challenger, target), Is.True);
            // never confirmed, which is what closing or declining the prompt leaves you with
            Assert.That(entMan.HasComponent<YautjaRitualDuelComponent>(target), Is.False);

            var preyVerbs = verbs.GetLocalVerbs(prey, challenger, typeof(AlternativeVerb));
            Assert.That(preyVerbs.Any(verb => verb.Text == Loc.GetString("cmu-yautja-honor-duel-challenge-verb")), Is.False);
            Assert.That(preyVerbs.Any(verb => verb.Text == Loc.GetString("cmu-yautja-ritual-claim-verb")), Is.True);
        });

        await pair.CleanReturnAsync();
    }
}
