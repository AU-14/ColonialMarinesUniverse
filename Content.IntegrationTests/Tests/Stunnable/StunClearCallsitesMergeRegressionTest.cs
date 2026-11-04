using Content.IntegrationTests.Fixtures;
using Content.Server.Stunnable;
using Content.Shared._RMC14.Pulling;
using Content.Shared._RMC14.Xenonids.Lunge;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Stunnable;

[TestFixture]
[TestOf(typeof(SharedStunSystem))]
public sealed class StunClearCallsitesMergeRegressionTest : GameTest
{
    private static readonly EntProtoId ParalyzeId = SharedStunSystem.ParalyzeId;

    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          parent: MobHuman
          id: StunClearMergeSynthMover
          components:
          - type: SynthStunCancelOnMove

        - type: entity
          parent: MobHuman
          id: StunClearMergeLungeTarget
          components:
          - type: XenoLungeStunned
            effects:
            - Stun
            - KnockedDown
        """;

    [Test]
    public async Task SynthMovementCancelsSuccessorParalysisWithoutLegacyStatusOwnership()
    {
        EntityUid target = default;

        await Server.WaitAssertion(() =>
        {
            var stun = Server.System<StunSystem>();
            target = SSpawn("StunClearMergeSynthMover");
            var input = SEntMan.GetComponent<InputMoverComponent>(target);
            input.HeldMoveButtons = MoveButtons.Up;

            Assert.That(stun.TryParalyze(target, TimeSpan.FromSeconds(10), refresh: true), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<StunnedComponent>(target), Is.True);
                Assert.That(SEntMan.HasComponent<KnockedDownComponent>(target), Is.True);
                Assert.That(SEntMan.HasComponent<SynthStunCancelOnMoveComponent>(target), Is.True);
            });
        });

        await Pair.RunTicksSync(3);
        await Server.WaitAssertion(() =>
        {
            var status = Server.System<StatusEffectsSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(status.HasStatusEffect(target, SharedStunSystem.StunId), Is.False);
                Assert.That(status.HasStatusEffect(target, ParalyzeId), Is.False);
                Assert.That(SEntMan.HasComponent<StunnedComponent>(target), Is.False);
                Assert.That(SEntMan.HasComponent<KnockedDownComponent>(target), Is.False);
                Assert.That(SEntMan.HasComponent<SynthStunCancelOnMoveComponent>(target), Is.False);
            });
        });
    }

    [Test]
    public async Task LungePullStopClearsBothSuccessorParalysisComponents()
    {
        EntityUid target = default;

        await Server.WaitAssertion(() =>
        {
            var stun = Server.System<StunSystem>();
            target = SSpawn("StunClearMergeLungeTarget");
            Assert.That(stun.TryParalyze(target, TimeSpan.FromSeconds(10), refresh: true), Is.True);

            var stopped = new PullStoppedMessage(target, target);
            SEntMan.EventBus.RaiseLocalEvent(target, stopped);
        });

        await Pair.RunTicksSync(3);
        await Server.WaitAssertion(() =>
        {
            var status = Server.System<StatusEffectsSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(status.HasStatusEffect(target, SharedStunSystem.StunId), Is.False);
                Assert.That(status.HasStatusEffect(target, ParalyzeId), Is.False);
                Assert.That(SEntMan.HasComponent<StunnedComponent>(target), Is.False);
                Assert.That(SEntMan.HasComponent<KnockedDownComponent>(target), Is.False);
                Assert.That(SEntMan.HasComponent<XenoLungeStunnedComponent>(target), Is.False);
            });
        });
    }
}
