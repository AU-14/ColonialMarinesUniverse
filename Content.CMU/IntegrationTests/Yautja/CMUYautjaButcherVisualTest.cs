using System.Numerics;
using System.Linq;
using System.Runtime.CompilerServices;
using Content.Client._RMC14.NightVision;
using Content.Client.CMU14.Yautja;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server.CMU14.Yautja;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Moq;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class CMUYautjaButcherVisualTest : InteractionTest
{
    private EntityUid _hunter;

    [TestCase("CMMobHuman", false)]
    [TestCase("CMXenoQueen", true)]
    public async Task InterruptedStagePreservesReplicatedCompletedWounds(string victim, bool xeno)
    {
        await SpawnTarget(victim);
        await Server.WaitAssertion(() =>
        {
            // Keep the fixture's immobile, atmosphere-independent actor; exercise the real corpse and DoAfter.
            // The connected client watches a different actor. Owner-only replication would fail here.
            _hunter = SEntMan.SpawnEntity("InteractionTestMob", SEntMan.GetCoordinates(PlayerCoords));
            SEntMan.EnsureComponent<YautjaComponent>(_hunter);
            SEntMan.System<MobStateSystem>().ChangeMobState(STarget!.Value, MobState.Dead);
            Assert.That(SEntMan.System<YautjaTrophySystem>().TryStartButcher(
                _hunter, STarget.Value, YautjaButcherProcedure.Skin), Is.True);
        });
        await AwaitStage(1, 2);
        await Client.WaitAssertion(() =>
        {
            var visual = CEntMan.GetComponent<CMUYautjaButcherVisualComponent>(CTarget!.Value);
            Assert.That(visual.Xeno, Is.EqualTo(xeno), "Observers must receive the victim-specific treatment.");
        });

        await Server.WaitPost(() => Transform.SetCoordinates(_hunter,
            SEntMan.GetCoordinates(PlayerCoords).Offset(new Vector2(4, 0))));
        await RunTicks(10);
        await AssertStage(1, 0);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<YautjaTrophySourceComponent>(STarget!.Value).ButcheryProgress,
                Is.EqualTo(2), "Movement must cancel the procedure without advancing the next stage.");
            Transform.SetCoordinates(_hunter, SEntMan.GetCoordinates(PlayerCoords));
            Assert.That(SEntMan.System<YautjaTrophySystem>().TryStartButcher(
                _hunter, STarget.Value, YautjaButcherProcedure.Skin), Is.True);
        });
        await AwaitStage(2, 3);
        if (!xeno)
            return;

        await AwaitStage(3, 4);
        // The DoAfter belongs to the remote hunter, not the connected observer.
        await RunTicks(Pair.SecondsToTicks(10));
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(STarget!.Value), Is.True, "Extraction must finish the real corpse procedure.");
            var skulls = 0;
            var pelts = 0;
            var trophies = SEntMan.EntityQueryEnumerator<YautjaTrophyComponent>();
            while (trophies.MoveNext(out var trophy))
            {
                if (trophy.Hunter != _hunter)
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
            CEntMan.System<CMUYautjaVisualsSystem>().FrameUpdate(0);
            var sprite = CEntMan.GetComponent<SpriteComponent>(CTarget.Value);
            Assert.That(CEntMan.System<SpriteSystem>().TryGetPostShader(sprite, "cmu-yautja-butcher", out _), Is.True);
            var transform = CEntMan.GetComponent<TransformComponent>(CTarget.Value);
            var handle = new Mock<DrawingHandleWorld>(MockBehavior.Loose, Texture.White);
            RenderThermal(new NightVisionOverlay(), (CTarget.Value, sprite, transform), transform.MapID,
                handle.Object, Angle.Zero, false, null);
            Assert.That(handle.Invocations.Count(call => call.Method.Name is "DrawTextureRect" or "DrawTextureRectRegion"),
                Is.Zero, "An observer's thermal pass must not cover the cuts or completed wounds with the intact corpse.");
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "Render")]
    private static extern void RenderThermal(NightVisionOverlay overlay, Entity<SpriteComponent, TransformComponent> entity,
        MapId? map, DrawingHandleWorld handle, Angle rotation, bool seeThroughContainers, float? transparency);
}
