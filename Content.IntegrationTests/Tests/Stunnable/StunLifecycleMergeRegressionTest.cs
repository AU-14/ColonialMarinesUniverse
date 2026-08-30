using Content.IntegrationTests.Fixtures;
using Content.Server.Stunnable;
using Content.Shared._RMC14.Slow;
using Content.Shared._RMC14.Stun;
using Content.Shared._RMC14.Xenonids.Collision;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.StatusEffect;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using LegacyStatusEffectsSystem = Content.Shared.StatusEffect.StatusEffectQuerySystem;
using NewStatusEffectsSystem = Content.Shared.StatusEffectNew.StatusEffectsSystem;

namespace Content.IntegrationTests.Tests.Stunnable;

[TestFixture]
[TestOf(typeof(SharedStunSystem))]
public sealed class StunLifecycleMergeRegressionTest : GameTest
{
    private static readonly EntProtoId ParalyzeId = SharedStunSystem.ParalyzeId;

    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          parent: MobHuman
          id: StunLifecycleMergeTarget
          components:
          - type: RMCSpeciesSlowdownModifier
          - type: StatusEffects
            allowed:
            - Unconscious
          - type: StunFriendlyXenoOnStep
          - type: StunHostilesOnStep
          - type: StunLifecycleMergeProbe

        - type: entity
          parent: MobHuman
          id: StunLifecycleMergeLegacyTarget
          components:
          - type: StatusEffects
            allowed:
            - Pacified
          - type: StunFriendlyXenoOnStep
            disableStatus: Pacified
        """;

    [Test]
    public async Task SuccessorShutdownBridgesRestoreCollisionAndImmobileVisuals()
    {
        EntityUid stunned = default;
        EntityUid paralyzed = default;

        await Server.WaitAssertion(() =>
        {
            _ = Server.System<StunLifecycleMergeProbeSystem>();
            var stun = Server.System<StunSystem>();
            var status = Server.System<NewStatusEffectsSystem>();

            stunned = SSpawn("StunLifecycleMergeTarget");
            Assert.That(stun.TryStun(stunned, TimeSpan.FromSeconds(10), refresh: true), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<StunnedComponent>(stunned), Is.True);
                Assert.That(SEntMan.HasComponent<XenoImmobileVisualsComponent>(stunned), Is.True,
                    "standing RMC species acquire immobile visuals from the successor Stunned event");
                Assert.That(SEntMan.GetComponent<StunFriendlyXenoOnStepComponent>(stunned).Enabled, Is.True);
                Assert.That(SEntMan.GetComponent<StunHostilesOnStepComponent>(stunned).Enabled, Is.True);
            });
            Assert.That(status.TryRemoveStatusEffect(stunned, SharedStunSystem.StunId), Is.True);

            paralyzed = SSpawn("StunLifecycleMergeTarget");
            Assert.That(stun.TryParalyze(paralyzed, TimeSpan.FromSeconds(10), refresh: true), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<StunnedComponent>(paralyzed), Is.True);
                Assert.That(SEntMan.HasComponent<KnockedDownComponent>(paralyzed), Is.True);
                Assert.That(SEntMan.GetComponent<StunFriendlyXenoOnStepComponent>(paralyzed).Enabled, Is.False);
                Assert.That(SEntMan.GetComponent<StunHostilesOnStepComponent>(paralyzed).Enabled, Is.False,
                    "both collision stunners must use the live successor knockdown component");
            });
            Assert.That(status.TryRemoveStatusEffect(paralyzed, ParalyzeId), Is.True);
        });

        await Pair.RunSeconds(2.1f);
        await Pair.RunTicksSync(3);
        await Server.WaitAssertion(() =>
        {
            var stunnedProbe = SEntMan.GetComponent<StunLifecycleMergeProbeComponent>(stunned);
            var paralyzedProbe = SEntMan.GetComponent<StunLifecycleMergeProbeComponent>(paralyzed);
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<StunnedComponent>(stunned), Is.False);
                Assert.That(SEntMan.HasComponent<XenoImmobileVisualsComponent>(stunned), Is.False,
                    "the keyed Stun shutdown bridge must clear RMC immobile visuals");
                Assert.That(stunnedProbe.EndedKeys, Is.EqualTo(new[] { "Stun" }));

                Assert.That(SEntMan.HasComponent<StunnedComponent>(paralyzed), Is.False);
                Assert.That(SEntMan.HasComponent<KnockedDownComponent>(paralyzed), Is.False);
                Assert.That(SEntMan.GetComponent<StunFriendlyXenoOnStepComponent>(paralyzed).Enabled, Is.True);
                Assert.That(SEntMan.GetComponent<StunHostilesOnStepComponent>(paralyzed).Enabled, Is.True);
                Assert.That(paralyzedProbe.EndedKeys, Is.EqualTo(new[] { "Stun", "KnockedDown" }),
                    "successor cleanup must retain the legacy keyed end-event contract in order");
            });
        });
    }

    [Test]
    public async Task ArbitraryLegacyDisableStatusStillUsesLegacyFallback()
    {
        await Server.WaitAssertion(() =>
        {
            var legacy = Server.System<LegacyStatusEffectsSystem>();
            var target = SSpawn("StunLifecycleMergeLegacyTarget");
            Assert.That(
                legacy.TryAddStatusEffect<PacifiedComponent>(
                    target,
                    "Pacified",
                    TimeSpan.FromSeconds(10),
                    refresh: true),
                Is.True);

            var refresh = new StunnedEvent();
            SEntMan.EventBus.RaiseLocalEvent(target, ref refresh);
            Assert.That(SEntMan.GetComponent<StunFriendlyXenoOnStepComponent>(target).Enabled, Is.False,
                "non-successor DisableStatus values must keep using the legacy status query");

            Assert.That(legacy.TryRemoveStatusEffect(target, "Pacified"), Is.True);
            Assert.That(SEntMan.GetComponent<StunFriendlyXenoOnStepComponent>(target).Enabled, Is.True,
                "the ordinary legacy StatusEffectEnded event must restore collision behavior");
        });
    }

    [Test]
    public async Task RecursiveDeletionDoesNotEmitSuccessorEndBridges()
    {
        EntityUid terminating = default;

        await Server.WaitAssertion(() =>
        {
            var probe = Server.System<StunLifecycleMergeProbeSystem>();
            var stun = Server.System<StunSystem>();
            var sizeStun = Server.System<RMCSizeStunSystem>();

            terminating = SSpawn("StunLifecycleMergeTarget");
            probe.Reset(terminating);
            Assert.That(stun.TryParalyze(terminating, TimeSpan.FromSeconds(10), refresh: true), Is.True);
            Assert.That(sizeStun.TryKnockOut(terminating, TimeSpan.FromSeconds(10)), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<StunnedComponent>(terminating), Is.True);
                Assert.That(SEntMan.HasComponent<KnockedDownComponent>(terminating), Is.True);
                Assert.That(SEntMan.HasComponent<RMCUnconsciousComponent>(terminating), Is.True);
                Assert.That(SEntMan.HasComponent<XenoImmobileVisualsComponent>(terminating), Is.False,
                    "knockdown removes the standing-only visual before deletion; the old shutdown bridge tried to re-add it");
            });

            SEntMan.DeleteEntity(terminating);
            Assert.That(probe.EndedKeys(terminating), Is.Empty,
                "component shutdown during recursive deletion must not emit successor end bridges");
        });

        await Pair.RunTicksSync(3);
        await Server.WaitAssertion(() =>
        {
            var probe = Server.System<StunLifecycleMergeProbeSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.Deleted(terminating), Is.True);
                Assert.That(probe.EndedKeys(terminating), Is.Empty);
            });
        });
    }
}

[RegisterComponent]
public sealed partial class StunLifecycleMergeProbeComponent : Component
{
    public readonly List<string> EndedKeys = new();
}

public sealed partial class StunLifecycleMergeProbeSystem : EntitySystem
{
    private readonly Dictionary<EntityUid, List<string>> _endedKeys = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StunLifecycleMergeProbeComponent, StatusEffectEndedEvent>(OnEnded);
    }

    private void OnEnded(Entity<StunLifecycleMergeProbeComponent> ent, ref StatusEffectEndedEvent args)
    {
        ent.Comp.EndedKeys.Add(args.Key);
        if (!_endedKeys.TryGetValue(ent.Owner, out var keys))
        {
            keys = new List<string>();
            _endedKeys[ent.Owner] = keys;
        }

        keys.Add(args.Key);
    }

    public IReadOnlyList<string> EndedKeys(EntityUid uid)
    {
        return _endedKeys.GetValueOrDefault(uid) ?? [];
    }

    public void Reset(EntityUid uid)
    {
        _endedKeys.Remove(uid);
    }
}
