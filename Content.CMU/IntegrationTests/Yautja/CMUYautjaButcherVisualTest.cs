using System.Numerics;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server.CMU14.Yautja;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class CMUYautjaButcherVisualTest : InteractionTest
{
    [TestCase("CMMobHuman", false)]
    [TestCase("CMXenoQueen", true)]
    public async Task InterruptedStagePreservesReplicatedCompletedWounds(string victim, bool xeno)
    {
        await SpawnTarget(victim);
        await Server.WaitAssertion(() =>
        {
            // Keep the fixture's immobile, atmosphere-independent actor; exercise the real corpse and DoAfter.
            SEntMan.EnsureComponent<YautjaComponent>(SPlayer);
            SEntMan.System<MobStateSystem>().ChangeMobState(STarget!.Value, MobState.Dead);
            Assert.That(SEntMan.System<YautjaTrophySystem>().TryStartButcher(
                SPlayer, STarget.Value, YautjaButcherProcedure.Skin), Is.True);
        });
        await AwaitStage(1, 2);
        await Client.WaitAssertion(() =>
        {
            var visual = CEntMan.GetComponent<CMUYautjaButcherVisualComponent>(CTarget!.Value);
            Assert.That(visual.Xeno, Is.EqualTo(xeno), "Observers must receive the victim-specific treatment.");
        });

        await Server.WaitPost(() => Transform.SetCoordinates(SPlayer,
            SEntMan.GetCoordinates(PlayerCoords).Offset(new Vector2(4, 0))));
        await RunTicks(10);
        await AssertStage(1, 0);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<YautjaTrophySourceComponent>(STarget!.Value).ButcheryProgress,
                Is.EqualTo(2), "Movement must cancel the procedure without advancing the next stage.");
            Transform.SetCoordinates(SPlayer, SEntMan.GetCoordinates(PlayerCoords));
            Assert.That(SEntMan.System<YautjaTrophySystem>().TryStartButcher(
                SPlayer, STarget.Value, YautjaButcherProcedure.Skin), Is.True);
        });
        await AwaitStage(2, 3);
        if (!xeno)
            return;

        await AwaitStage(3, 4);
        await AwaitDoAfters();
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(STarget!.Value), Is.True, "Extraction must finish the real corpse procedure.");
            var skulls = 0;
            var pelts = 0;
            var trophies = SEntMan.EntityQueryEnumerator<YautjaTrophyComponent>();
            while (trophies.MoveNext(out var trophy))
            {
                if (trophy.Hunter != SPlayer)
                    continue;
                skulls += trophy.Kind == YautjaTrophyKind.XenoSkull ? 1 : 0;
                pelts += trophy.Kind == YautjaTrophyKind.XenoPelt ? 1 : 0;
            }
            Assert.That(skulls, Is.EqualTo(1), "The visual extraction must correspond to one actual skull trophy.");
            Assert.That(pelts, Is.EqualTo(1));
        });
    }

    private async Task AwaitStage(int completed, int active)
    {
        // Skinning chains automatically; inspect each transition without restarting the action.
        for (var ticks = 0; ticks < Pair.SecondsToTicks(20); ticks += 10)
        {
            var reached = false;
            await Server.WaitPost(() => reached =
                SEntMan.GetComponent<CMUYautjaButcherVisualComponent>(STarget!.Value).CompletedStage >= completed);
            if (reached)
                break;
            await RunTicks(10);
        }
        await RunTicks(5);
        await AssertStage(completed, active);
    }

    private async Task AssertStage(int completed, int active)
    {
        await Server.WaitAssertion(() =>
        {
            var visual = SEntMan.GetComponent<CMUYautjaButcherVisualComponent>(STarget!.Value);
            Assert.That(visual.CompletedStage, Is.EqualTo(completed));
            Assert.That(visual.ActiveStage, Is.EqualTo(active));
        });
        await Client.WaitAssertion(() =>
        {
            var visual = CEntMan.GetComponent<CMUYautjaButcherVisualComponent>(CTarget!.Value);
            Assert.That(visual.CompletedStage, Is.EqualTo(completed), "The victim's wounds must be replicated.");
            Assert.That(visual.ActiveStage, Is.EqualTo(active), "Cancelled cuts must stop on the client.");
        });
    }
}
