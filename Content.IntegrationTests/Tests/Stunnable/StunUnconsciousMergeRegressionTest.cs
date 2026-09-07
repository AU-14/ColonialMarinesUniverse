using Content.IntegrationTests.Fixtures;
using Content.Server.Stunnable;
using Content.Shared._RMC14.Fireman;
using Content.Shared._RMC14.ShakeStun;
using Content.Shared._RMC14.Stun;
using Content.Shared.Bed.Sleep;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.EntityEffects;
using Content.Shared.EntityEffects.Effects.StatusEffects;
using Content.Shared.Hands;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Standing;
using Content.Shared.StatusEffect;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using LegacyStatusEffectsSystem = Content.Shared.StatusEffect.StatusEffectQuerySystem;
using NewStatusEffectsSystem = Content.Shared.StatusEffectNew.StatusEffectsSystem;

namespace Content.IntegrationTests.Tests.Stunnable;

[TestFixture]
[TestOf(typeof(RMCSizeStunSystem))]
public sealed class StunUnconsciousMergeRegressionTest : GameTest
{
    private static readonly EntProtoId ParalyzeId = SharedStunSystem.ParalyzeId;
    private static readonly EntProtoId RmcUnconsciousId = SharedStunSystem.RMCUnconsciousId;
    private static readonly EntProtoId RmcUnconsciousMutedId = "StatusEffectRMCUnconsciousMuted";

    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          parent: MobHuman
          id: StunUnconsciousMergeTarget
          components:
          - type: StatusEffects
            allowed:
            - Unconscious
            - Pacified
          - type: StunUnconsciousMergeProbe
          - type: StunShakeable
            durationRemoved: 2

        - type: entity
          parent: MobHuman
          id: StunUnconsciousMergeShaker
          components:
          - type: StunShakeableUser
            cooldown: 0

        - type: entity
          parent: MobHuman
          id: StunUnconsciousMergeWorm
          components:
          - type: Worm

        - type: entity
          parent: CMXenoDrone
          id: StunUnconsciousMergeCarriedXeno
          components:
          - type: FiremanCarriable
            beingCarried: true
        """;

    [Test]
    public async Task StatusPrototypesKeepPureStunParalysisAndSilentUnconsciousOwnershipDistinct()
    {
        await Server.WaitAssertion(() =>
        {
            var stunned = SProtoMan.Index<EntityPrototype>(SharedStunSystem.StunId);
            var paralyzed = SProtoMan.Index<EntityPrototype>(ParalyzeId);
            var unconscious = SProtoMan.Index<EntityPrototype>(RmcUnconsciousId);

            Assert.Multiple(() =>
            {
                Assert.That(stunned.Components.ContainsKey("StunnedStatusEffect"), Is.True);
                Assert.That(stunned.Components.ContainsKey("KnockdownStatusEffect"), Is.False,
                    "pure stun must never acquire knockdown ownership through inheritance");

                Assert.That(paralyzed.Parents, Is.EqualTo(new[] { SharedStunSystem.StunId.Id }));
                Assert.That(paralyzed.Components.ContainsKey("StunnedStatusEffect"), Is.True);
                Assert.That(paralyzed.Components.ContainsKey("KnockdownStatusEffect"), Is.True);
                var directKnockdown = (KnockdownStatusEffectComponent)
                    paralyzed.Components["KnockdownStatusEffect"].Component;
                Assert.That(directKnockdown.Silent, Is.False);
                Assert.That(directKnockdown.Drop, Is.True);

                Assert.That(unconscious.Parents, Is.EqualTo(new[] { "MobStatusEffectBase" }));
                Assert.That(unconscious.Components.ContainsKey("StunnedStatusEffect"), Is.True);
                Assert.That(unconscious.Components.ContainsKey("KnockdownStatusEffect"), Is.True);
                Assert.That(unconscious.Components.ContainsKey("StatusEffectAlert"), Is.False);
                Assert.That(unconscious.Components.ContainsKey("ExaminableStatusEffect"), Is.False,
                    "the silent RMC mirror must not duplicate direct stun presentation");
                var unconsciousKnockdown = (KnockdownStatusEffectComponent)
                    unconscious.Components["KnockdownStatusEffect"].Component;
                Assert.That(unconsciousKnockdown.Silent, Is.True);
                Assert.That(unconsciousKnockdown.Drop, Is.False);
            });
        });
    }

    [Test]
    public async Task PublicParalyzeFirstApplyAndExistingRefreshKeepLegacyEventParity()
    {
        await Server.WaitAssertion(() =>
        {
            _ = Server.System<StunUnconsciousMergeProbeSystem>();
            var status = Server.System<NewStatusEffectsSystem>();
            var stun = Server.System<StunSystem>();
            var target = SSpawn("StunUnconsciousMergeTarget");
            var probe = SEntMan.GetComponent<StunUnconsciousMergeProbeComponent>(target);

            Assert.That(stun.TryParalyze(target, TimeSpan.FromSeconds(2), refresh: true), Is.True);
            Assert.That(status.TryGetTime(target, ParalyzeId, out var initial), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(probe.KnockedDownEvents, Is.EqualTo(1));
                Assert.That(probe.StunnedEvents, Is.EqualTo(1));
                Assert.That(probe.DropEvents, Is.EqualTo(2),
                    "fresh paralysis drops once while creating knockdown and once through OnStunnedSuccessfully");
            });

            Assert.That(stun.TryParalyze(target, TimeSpan.FromSeconds(2), refresh: true), Is.True);
            Assert.That(status.TryGetTime(target, ParalyzeId, out var refreshed), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(refreshed.EffectEnt, Is.EqualTo(initial.EffectEnt));
                Assert.That(probe.KnockedDownEvents, Is.EqualTo(2));
                Assert.That(probe.StunnedEvents, Is.EqualTo(2));
                Assert.That(probe.DropEvents, Is.EqualTo(3));
            });

            Assert.That(stun.TryParalyze(target, TimeSpan.FromSeconds(2), refresh: false), Is.True);
            Assert.That(status.TryGetTime(target, ParalyzeId, out var stacked), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(stacked.EffectEnt, Is.EqualTo(initial.EffectEnt));
                Assert.That(probe.KnockedDownEvents, Is.EqualTo(3));
                Assert.That(probe.StunnedEvents, Is.EqualTo(3));
                Assert.That(probe.DropEvents, Is.EqualTo(4),
                    "existing refresh and stack each retain the one-drop legacy raw API path");
            });
        });
    }

    [Test]
    public async Task KnockoutOwnsSuccessorTimerAndStackRefreshHasNoRepeatedSideEffects()
    {
        EntityUid target = default;
        EntityUid successor = default;
        TimeSpan successorEnd = default;

        await Server.WaitAssertion(() =>
        {
            _ = Server.System<StunUnconsciousMergeProbeSystem>();
            var sizeStun = Server.System<RMCSizeStunSystem>();
            var legacy = Server.System<LegacyStatusEffectsSystem>();
            var successorStatus = Server.System<NewStatusEffectsSystem>();
            target = SSpawn("StunUnconsciousMergeTarget");
            var start = Server.Timing.CurTime;

            Assert.That(sizeStun.TryKnockOut(target, TimeSpan.FromSeconds(1), refresh: true), Is.True);
            Assert.That(legacy.TryGetTime(target, "Unconscious", out var legacyTime), Is.True);
            Assert.That(successorStatus.TryGetTime(target, RmcUnconsciousId, out var newTime), Is.True);
            Assert.That(successorStatus.TryGetTime(target, RmcUnconsciousMutedId, out var muteTime), Is.True);
            successor = newTime.EffectEnt;
            successorEnd = newTime.EndEffectTime!.Value;
            var probe = SEntMan.GetComponent<StunUnconsciousMergeProbeComponent>(target);

            Assert.Multiple(() =>
            {
                Assert.That(legacyTime!.Value.Item2, Is.EqualTo(start + TimeSpan.FromSeconds(1)));
                Assert.That(newTime.EndEffectTime, Is.EqualTo(start + TimeSpan.FromSeconds(1)));
                Assert.That(muteTime.EndEffectTime, Is.EqualTo(newTime.EndEffectTime));
                Assert.That(successorStatus.HasStatusEffect(target, SharedStunSystem.StunId), Is.False);
                Assert.That(successorStatus.HasStatusEffect(target, ParalyzeId), Is.False);
                Assert.That(SEntMan.HasComponent<RMCUnconsciousComponent>(target), Is.True);
                Assert.That(SEntMan.HasComponent<StunnedComponent>(target), Is.True);
                Assert.That(SEntMan.HasComponent<KnockedDownComponent>(target), Is.True);
                Assert.That(probe.StunnedEvents, Is.Zero);
                Assert.That(probe.KnockedDownEvents, Is.Zero);
                Assert.That(probe.DropEvents, Is.Zero,
                    "synchronizing the legacy owner must not replay stun/drop/log side effects");
            });

            Assert.That(sizeStun.TryKnockOut(target, TimeSpan.FromSeconds(1), refresh: false), Is.True);
            Assert.That(legacy.TryGetTime(target, "Unconscious", out var stackedLegacy), Is.True);
            Assert.That(successorStatus.TryGetTime(target, RmcUnconsciousId, out var stackedNew), Is.True);
            Assert.That(successorStatus.TryGetTime(target, RmcUnconsciousMutedId, out var stackedMute), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(stackedLegacy!.Value.Item2, Is.EqualTo(start + TimeSpan.FromSeconds(2)));
                Assert.That(stackedNew.EffectEnt, Is.EqualTo(successor),
                    "stack refresh must update the existing successor status entity");
                Assert.That(stackedNew.EndEffectTime, Is.EqualTo(start + TimeSpan.FromSeconds(2)));
                Assert.That(stackedMute.EffectEnt, Is.EqualTo(muteTime.EffectEnt));
                Assert.That(stackedMute.EndEffectTime, Is.EqualTo(stackedNew.EndEffectTime));
                Assert.That(stackedNew.EndEffectTime, Is.GreaterThan(successorEnd));
                Assert.That(probe.StunnedEvents, Is.Zero);
                Assert.That(probe.KnockedDownEvents, Is.Zero);
                Assert.That(probe.DropEvents, Is.Zero);
            });

            Assert.That(
                legacy.TryAddStatusEffect<PacifiedComponent>(
                    target,
                    "Pacified",
                    TimeSpan.FromSeconds(10),
                    refresh: true),
                Is.True);
            Assert.That(legacy.TryRemoveStatusEffect(target, "Pacified"), Is.True);
            Assert.That(successorStatus.TryGetTime(target, RmcUnconsciousId, out var afterUnrelatedEnd), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(afterUnrelatedEnd.EffectEnt, Is.EqualTo(successor));
                Assert.That(afterUnrelatedEnd.EndEffectTime, Is.EqualTo(start + TimeSpan.FromSeconds(2)),
                    "ending an unrelated legacy status must not refresh or replace unconsciousness paralysis");
                Assert.That(probe.StunnedEvents, Is.Zero);
                Assert.That(probe.KnockedDownEvents, Is.Zero);
                Assert.That(probe.DropEvents, Is.Zero,
                    "an unrelated StatusEffectEnded event must not replay knockout side effects");
            });

            var stun = Server.System<StunSystem>();
            stun.TryClearStunAndKnockdown(target);
            Assert.That(successorStatus.TryGetTime(target, RmcUnconsciousId, out var afterClear), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(afterClear.EffectEnt, Is.EqualTo(successor));
                Assert.That(afterClear.EndEffectTime, Is.EqualTo(start + TimeSpan.FromSeconds(2)),
                    "generic clearing must preserve active legacy unconsciousness ownership");
                Assert.That(SEntMan.HasComponent<StunnedComponent>(target), Is.True);
                Assert.That(SEntMan.HasComponent<KnockedDownComponent>(target), Is.True);
                Assert.That(probe.StunnedEvents, Is.Zero);
                Assert.That(probe.KnockedDownEvents, Is.Zero);
                Assert.That(probe.DropEvents, Is.Zero);
            });
        });

        await Pair.RunSeconds(2.1f);
        await Pair.RunTicksSync(3);
        await Server.WaitAssertion(() =>
        {
            var legacy = Server.System<LegacyStatusEffectsSystem>();
            var successorStatus = Server.System<NewStatusEffectsSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(legacy.HasStatusEffect(target, "Unconscious"), Is.False);
                Assert.That(successorStatus.HasStatusEffect(target, RmcUnconsciousId), Is.False);
                Assert.That(successorStatus.HasStatusEffect(target, RmcUnconsciousMutedId), Is.False);
                Assert.That(SEntMan.HasComponent<RMCUnconsciousComponent>(target), Is.False);
                Assert.That(SEntMan.HasComponent<StunnedComponent>(target), Is.False);
                Assert.That(SEntMan.HasComponent<KnockedDownComponent>(target), Is.False,
                    "the timer-owned knockdown must stand after its matching stun expires");
            });
        });
    }

    [Test]
    public async Task DirectLegacyStartupSynchronizesAndShortKnockoutPreservesLongerParalysis()
    {
        EntityUid direct = default;
        EntityUid overlap = default;
        TimeSpan longerEnd = default;

        await Server.WaitAssertion(() =>
        {
            _ = Server.System<StunUnconsciousMergeProbeSystem>();
            var legacy = Server.System<LegacyStatusEffectsSystem>();
            var successorStatus = Server.System<NewStatusEffectsSystem>();
            var stun = Server.System<StunSystem>();
            var sizeStun = Server.System<RMCSizeStunSystem>();

            direct = SSpawn("StunUnconsciousMergeTarget");
            var directStart = Server.Timing.CurTime;
            Assert.That(
                legacy.TryAddStatusEffect<RMCUnconsciousComponent>(
                    direct,
                    "Unconscious",
                    TimeSpan.FromSeconds(1),
                    refresh: true),
                Is.True);
            Assert.That(successorStatus.TryGetTime(direct, RmcUnconsciousId, out var directTime), Is.True,
                "direct legacy chemistry/component startup must acquire timer-owned successor paralysis");
            Assert.Multiple(() =>
            {
                Assert.That(directTime.EndEffectTime, Is.EqualTo(directStart + TimeSpan.FromSeconds(1)));
                Assert.That(SEntMan.HasComponent<StunnedComponent>(direct), Is.True);
                Assert.That(SEntMan.HasComponent<KnockedDownComponent>(direct), Is.True);
            });

            overlap = SSpawn("StunUnconsciousMergeTarget");
            var overlapStart = Server.Timing.CurTime;
            Assert.That(stun.TryParalyze(overlap, TimeSpan.FromSeconds(5), refresh: true), Is.True);
            Assert.That(successorStatus.TryGetTime(overlap, ParalyzeId, out var initial), Is.True);
            longerEnd = initial.EndEffectTime!.Value;
            Assert.That(longerEnd, Is.EqualTo(overlapStart + TimeSpan.FromSeconds(5)));

            Assert.That(sizeStun.TryKnockOut(overlap, TimeSpan.FromSeconds(1)), Is.True);
            Assert.That(successorStatus.TryGetTime(overlap, ParalyzeId, out var afterKnockout), Is.True);
            Assert.That(successorStatus.TryGetTime(overlap, RmcUnconsciousId, out var unconscious), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(afterKnockout.EndEffectTime, Is.EqualTo(longerEnd),
                    "a short unconscious owner must not replace or shorten direct paralysis");
                Assert.That(unconscious.EndEffectTime, Is.EqualTo(overlapStart + TimeSpan.FromSeconds(1)));
            });
        });

        await Pair.RunSeconds(1.1f);
        await Pair.RunTicksSync(3);
        await Server.WaitAssertion(() =>
        {
            var legacy = Server.System<LegacyStatusEffectsSystem>();
            var successorStatus = Server.System<NewStatusEffectsSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(legacy.HasStatusEffect(direct, "Unconscious"), Is.False);
                Assert.That(successorStatus.HasStatusEffect(direct, RmcUnconsciousId), Is.False);
                Assert.That(legacy.HasStatusEffect(overlap, "Unconscious"), Is.False);
                Assert.That(successorStatus.HasStatusEffect(overlap, RmcUnconsciousId), Is.False);
                Assert.That(successorStatus.TryGetTime(overlap, ParalyzeId, out var remaining), Is.True);
                Assert.That(remaining.EndEffectTime, Is.EqualTo(longerEnd));
                Assert.That(SEntMan.HasComponent<StunnedComponent>(overlap), Is.True);
                Assert.That(SEntMan.HasComponent<KnockedDownComponent>(overlap), Is.True,
                    "unconscious shutdown must not manually delete a longer stun/knockdown owner");
            });
        });
    }

    [Test]
    public async Task CombinedClearAndReduceLeaveTheIndependentUnconsciousMirrorUntouched()
    {
        EntityUid target = default;
        EntityUid unconsciousEffect = default;
        TimeSpan unconsciousEnd = default;

        await Server.WaitAssertion(() =>
        {
            var status = Server.System<NewStatusEffectsSystem>();
            var stun = Server.System<StunSystem>();
            var sizeStun = Server.System<RMCSizeStunSystem>();
            target = SSpawn("StunUnconsciousMergeTarget");
            var start = Server.Timing.CurTime;
            Assert.That(stun.TryStun(target, TimeSpan.FromSeconds(10), refresh: true), Is.True);
            Assert.That(stun.TryParalyze(target, TimeSpan.FromSeconds(10), refresh: true), Is.True);
            Assert.That(sizeStun.TryKnockOut(target, TimeSpan.FromSeconds(10)), Is.True);
            Assert.That(status.TryGetTime(target, SharedStunSystem.StunId, out var pureStun), Is.True);
            Assert.That(status.TryGetTime(target, ParalyzeId, out var paralysis), Is.True);
            Assert.That(status.TryGetTime(target, RmcUnconsciousId, out var unconscious), Is.True);
            unconsciousEffect = unconscious.EffectEnt;
            unconsciousEnd = unconscious.EndEffectTime!.Value;
            Assert.Multiple(() =>
            {
                Assert.That(pureStun.EndEffectTime, Is.EqualTo(start + TimeSpan.FromSeconds(10)));
                Assert.That(paralysis.EndEffectTime, Is.EqualTo(start + TimeSpan.FromSeconds(10)));
                Assert.That(unconscious.EndEffectTime, Is.EqualTo(start + TimeSpan.FromSeconds(10)));
            });

            Assert.That(stun.TryRemoveStunAndKnockdownTime(target, TimeSpan.FromSeconds(2)), Is.True);
            Assert.That(status.TryGetTime(target, SharedStunSystem.StunId, out var reducedStun), Is.True);
            Assert.That(status.TryGetTime(target, ParalyzeId, out var reducedParalysis), Is.True);
            Assert.That(status.TryGetTime(target, RmcUnconsciousId, out var sameUnconscious), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(reducedStun.EndEffectTime, Is.EqualTo(start + TimeSpan.FromSeconds(8)));
                Assert.That(reducedParalysis.EndEffectTime, Is.EqualTo(start + TimeSpan.FromSeconds(8)));
                Assert.That(sameUnconscious.EffectEnt, Is.EqualTo(unconscious.EffectEnt));
                Assert.That(sameUnconscious.EndEffectTime, Is.EqualTo(unconscious.EndEffectTime),
                    "the broad fork reduction helper must not mutate the independent unconscious mirror");
            });

            Assert.That(stun.TryClearStunAndKnockdown(target), Is.True);
        });

        await Pair.RunTicksSync(3);
        await Server.WaitAssertion(() =>
        {
            var status = Server.System<NewStatusEffectsSystem>();
            Assert.That(status.TryGetTime(target, RmcUnconsciousId, out var afterClear), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(status.HasStatusEffect(target, SharedStunSystem.StunId), Is.False);
                Assert.That(status.HasStatusEffect(target, ParalyzeId), Is.False);
                Assert.That(afterClear.EffectEnt, Is.EqualTo(unconsciousEffect));
                Assert.That(afterClear.EndEffectTime, Is.EqualTo(unconsciousEnd));
                Assert.That(SEntMan.HasComponent<StunnedComponent>(target), Is.True);
                Assert.That(SEntMan.HasComponent<KnockedDownComponent>(target), Is.True,
                    "the remaining unconscious status still owns the shared paralysis components");
            });
        });
    }

    [Test]
    public async Task IndependentOwnersComposeAcrossEarlyRemovalAndNaturalExpiry()
    {
        EntityUid removedUnconscious = default;
        EntityUid expiringUnconscious = default;
        EntityUid soleUnconscious = default;
        EntityUid pureStun = default;
        TimeSpan removedParalyzeEnd = default;
        TimeSpan expiringParalyzeEnd = default;
        TimeSpan pureStunEnd = default;

        await Server.WaitAssertion(() =>
        {
            var status = Server.System<NewStatusEffectsSystem>();
            var stun = Server.System<StunSystem>();
            var sizeStun = Server.System<RMCSizeStunSystem>();
            var start = Server.Timing.CurTime;

            removedUnconscious = SSpawn("StunUnconsciousMergeTarget");
            Assert.That(stun.TryParalyze(removedUnconscious, TimeSpan.FromSeconds(5), refresh: true), Is.True);
            Assert.That(sizeStun.TryKnockOut(removedUnconscious, TimeSpan.FromSeconds(10)), Is.True);
            Assert.That(status.TryGetTime(removedUnconscious, ParalyzeId, out var removedParalyze), Is.True);
            removedParalyzeEnd = removedParalyze.EndEffectTime!.Value;
            Assert.That(removedParalyzeEnd, Is.EqualTo(start + TimeSpan.FromSeconds(5)));

            expiringUnconscious = SSpawn("StunUnconsciousMergeTarget");
            Assert.That(stun.TryParalyze(expiringUnconscious, TimeSpan.FromSeconds(10), refresh: true), Is.True);
            Assert.That(sizeStun.TryKnockOut(expiringUnconscious, TimeSpan.FromSeconds(5)), Is.True);
            Assert.That(status.TryGetTime(expiringUnconscious, ParalyzeId, out var expiringParalyze), Is.True);
            expiringParalyzeEnd = expiringParalyze.EndEffectTime!.Value;

            soleUnconscious = SSpawn("StunUnconsciousMergeTarget");
            Assert.That(sizeStun.TryKnockOut(soleUnconscious, TimeSpan.FromSeconds(10)), Is.True);

            pureStun = SSpawn("StunUnconsciousMergeTarget");
            Assert.That(stun.TryStun(pureStun, TimeSpan.FromSeconds(5), refresh: true), Is.True);
            Assert.That(sizeStun.TryKnockOut(pureStun, TimeSpan.FromSeconds(10)), Is.True);
            Assert.That(status.TryGetTime(pureStun, SharedStunSystem.StunId, out var stunTime), Is.True);
            pureStunEnd = stunTime.EndEffectTime!.Value;
        });

        await Pair.RunSeconds(2.1f);
        await Server.WaitAssertion(() =>
        {
            var legacy = Server.System<LegacyStatusEffectsSystem>();
            Assert.That(legacy.TryRemoveStatusEffect(removedUnconscious, "Unconscious"), Is.True);
            Assert.That(legacy.TryRemoveStatusEffect(soleUnconscious, "Unconscious"), Is.True);
            Assert.That(legacy.TryRemoveStatusEffect(pureStun, "Unconscious"), Is.True);
        });

        await Pair.RunTicksSync(3);
        await Server.WaitAssertion(() =>
        {
            var status = Server.System<NewStatusEffectsSystem>();
            Assert.That(status.TryGetTime(removedUnconscious, ParalyzeId, out var remainingParalyze), Is.True);
            Assert.That(status.TryGetTime(pureStun, SharedStunSystem.StunId, out var remainingStun), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(status.HasStatusEffect(removedUnconscious, RmcUnconsciousId), Is.False);
                Assert.That(remainingParalyze.EndEffectTime, Is.EqualTo(removedParalyzeEnd));
                Assert.That(SEntMan.HasComponent<StunnedComponent>(removedUnconscious), Is.True);
                Assert.That(SEntMan.HasComponent<KnockedDownComponent>(removedUnconscious), Is.True);

                Assert.That(status.HasStatusEffect(soleUnconscious, RmcUnconsciousId), Is.False);
                Assert.That(status.HasStatusEffect(soleUnconscious, RmcUnconsciousMutedId), Is.False);
                Assert.That(SEntMan.HasComponent<StunnedComponent>(soleUnconscious), Is.False);
                Assert.That(SEntMan.HasComponent<KnockedDownComponent>(soleUnconscious), Is.False,
                    "removing the sole unconscious owner must release both shared components immediately");

                Assert.That(status.HasStatusEffect(pureStun, RmcUnconsciousId), Is.False);
                Assert.That(remainingStun.EndEffectTime, Is.EqualTo(pureStunEnd));
                Assert.That(SEntMan.HasComponent<StunnedComponent>(pureStun), Is.True);
                Assert.That(SEntMan.HasComponent<KnockedDownComponent>(pureStun), Is.False,
                    "pure Stun retains immobility but does not own knockdown after unconsciousness ends");
            });
        });

        await Pair.RunSeconds(3.1f);
        await Pair.RunTicksSync(3);
        await Server.WaitAssertion(() =>
        {
            var status = Server.System<NewStatusEffectsSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(status.HasStatusEffect(expiringUnconscious, RmcUnconsciousId), Is.False);
                Assert.That(status.TryGetTime(expiringUnconscious, ParalyzeId, out var remaining), Is.True);
                Assert.That(remaining.EndEffectTime, Is.EqualTo(expiringParalyzeEnd));
                Assert.That(SEntMan.HasComponent<StunnedComponent>(expiringUnconscious), Is.True);
                Assert.That(SEntMan.HasComponent<KnockedDownComponent>(expiringUnconscious), Is.True,
                    "natural unconscious expiry must not tear down the longer direct paralysis owner");
            });
        });
    }

    [Test]
    public async Task SleepingOwnersSurviveClearingTheDirectParalysisEffect()
    {
        var targets = new List<EntityUid>();
        EntityUid rawSleep = default;

        await Server.WaitAssertion(() =>
        {
            var status = Server.System<NewStatusEffectsSystem>();
            var stun = Server.System<StunSystem>();
            foreach (var owner in new[] { "StatusEffectForcedSleeping", "StatusEffectSSDSleeping" })
            {
                var target = SSpawn("StunUnconsciousMergeTarget");
                targets.Add(target);
                Assert.That(
                    status.TryUpdateStatusEffectDuration(target, owner, TimeSpan.FromSeconds(10)),
                    Is.True);
                Assert.That(stun.TryParalyze(target, TimeSpan.FromSeconds(5), refresh: true, force: true), Is.True);
                Assert.That(status.HasStatusEffect(target, ParalyzeId), Is.True);
                Assert.That(status.HasStatusEffect(target, owner), Is.True);

                stun.TryClearStunAndKnockdown(target);
            }

            rawSleep = SSpawn("StunUnconsciousMergeTarget");
            SEntMan.EnsureComponent<SleepingComponent>(rawSleep);
            Assert.That(stun.TryParalyze(rawSleep, TimeSpan.FromSeconds(5), refresh: true, force: true), Is.True);
            Assert.That(status.HasStatusEffect(rawSleep, ParalyzeId), Is.True);
            stun.TryClearStunAndKnockdown(rawSleep);
        });

        await Pair.RunTicksSync(3);
        await Server.WaitAssertion(() =>
        {
            var status = Server.System<NewStatusEffectsSystem>();
            var owners = new[] { "StatusEffectForcedSleeping", "StatusEffectSSDSleeping" };
            for (var i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                Assert.Multiple(() =>
                {
                    Assert.That(status.HasStatusEffect(target, ParalyzeId), Is.False,
                        "clearing removes only the directly owned ParalyzeId effect");
                    Assert.That(status.HasStatusEffect(target, owners[i]), Is.True);
                    Assert.That(SEntMan.HasComponent<StunnedComponent>(target), Is.True);
                    Assert.That(SEntMan.HasComponent<KnockedDownComponent>(target), Is.True,
                        "another successor status owner must retain shared paralysis components");
                });
            }

            Assert.Multiple(() =>
            {
                Assert.That(status.HasStatusEffect(rawSleep, ParalyzeId), Is.False);
                Assert.That(SEntMan.HasComponent<SleepingComponent>(rawSleep), Is.True);
                Assert.That(SEntMan.HasComponent<StunnedComponent>(rawSleep), Is.True);
                Assert.That(SEntMan.HasComponent<KnockedDownComponent>(rawSleep), Is.True,
                    "raw SleepingComponent owns the shared components independently of successor statuses");
            });
        });
    }

    [Test]
    public async Task ShakeReducesUnconsciousAndSuccessorExactlyOnce()
    {
        await Server.WaitAssertion(() =>
        {
            var legacy = Server.System<LegacyStatusEffectsSystem>();
            var successorStatus = Server.System<NewStatusEffectsSystem>();
            var sizeStun = Server.System<RMCSizeStunSystem>();
            var target = SSpawn("StunUnconsciousMergeTarget");
            var user = SSpawn("StunUnconsciousMergeShaker");
            var start = Server.Timing.CurTime;
            Assert.That(sizeStun.TryKnockOut(target, TimeSpan.FromSeconds(10)), Is.True);

            var interact = new InteractHandEvent(user, target);
            SEntMan.EventBus.RaiseLocalEvent(target, interact);
            Assert.That(interact.Handled, Is.True);
            Assert.That(legacy.TryGetTime(target, "Unconscious", out var legacyTime), Is.True);
            Assert.That(successorStatus.TryGetTime(target, RmcUnconsciousId, out var successorTime), Is.True);
            Assert.That(successorStatus.TryGetTime(target, RmcUnconsciousMutedId, out var muteTime), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(legacyTime!.Value.Item2, Is.EqualTo(start + TimeSpan.FromSeconds(8)));
                Assert.That(successorTime.EndEffectTime, Is.EqualTo(start + TimeSpan.FromSeconds(8)),
                    "shake must reduce the legacy owner and its independent mirror exactly once");
                Assert.That(muteTime.EndEffectTime, Is.EqualTo(start + TimeSpan.FromSeconds(8)),
                    "the separately owned mute mirror must track the reduced unconsciousness timer");
                Assert.That(SEntMan.HasComponent<StunnedComponent>(target), Is.True);
                Assert.That(SEntMan.HasComponent<KnockedDownComponent>(target), Is.True);
            });
        });
    }

    [Test]
    public async Task ExplicitClearStandsOnlyUnownedAndActuallyStandableKnockdowns()
    {
        EntityUid ordinary = default;
        EntityUid worm = default;
        EntityUid carried = default;

        await Server.WaitAssertion(() =>
        {
            var standing = Server.System<StandingStateSystem>();
            var stun = Server.System<StunSystem>();

            ordinary = SSpawn("StunUnconsciousMergeTarget");
            Assert.That(stun.TryParalyze(ordinary, TimeSpan.FromSeconds(10), refresh: true), Is.True);
            Assert.That(stun.TryClearStunAndKnockdown(ordinary), Is.True);

            worm = SSpawn("StunUnconsciousMergeWorm");
            var wormKnocked = SEntMan.GetComponent<KnockedDownComponent>(worm);
            Assert.That(wormKnocked.AutoStand, Is.False);
            Assert.That(stun.TryParalyze(worm, TimeSpan.FromSeconds(10), refresh: true), Is.True);
            Assert.That(stun.TryClearStunAndKnockdown(worm), Is.True);

            carried = SSpawn("StunUnconsciousMergeCarriedXeno");
            Assert.That(SEntMan.GetComponent<FiremanCarriableComponent>(carried).BeingCarried, Is.True);
            Assert.That(standing.Down(carried, playSound: false, dropHeldItems: false, force: true), Is.True);
            Assert.That(stun.TryParalyze(carried, TimeSpan.FromSeconds(10), refresh: true), Is.True);
            Assert.That(stun.TryClearStunAndKnockdown(carried), Is.True);
        });

        await Pair.RunTicksSync(4);
        await Server.WaitAssertion(() =>
        {
            var status = Server.System<NewStatusEffectsSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(status.HasStatusEffect(ordinary, SharedStunSystem.StunId), Is.False);
                Assert.That(status.HasStatusEffect(ordinary, ParalyzeId), Is.False);
                Assert.That(SEntMan.HasComponent<StunnedComponent>(ordinary), Is.False);
                Assert.That(SEntMan.HasComponent<KnockedDownComponent>(ordinary), Is.False,
                    "an ordinary crawler with no other owner must use the safe immediate stand path");
                Assert.That(SEntMan.GetComponent<StandingStateComponent>(ordinary).Standing, Is.True);

                Assert.That(status.HasStatusEffect(worm, SharedStunSystem.StunId), Is.False);
                Assert.That(status.HasStatusEffect(worm, ParalyzeId), Is.False);
                Assert.That(SEntMan.HasComponent<StunnedComponent>(worm), Is.False);
                Assert.That(SEntMan.HasComponent<KnockedDownComponent>(worm), Is.True);
                Assert.That(SEntMan.GetComponent<KnockedDownComponent>(worm).AutoStand, Is.False);

                Assert.That(status.HasStatusEffect(carried, SharedStunSystem.StunId), Is.False);
                Assert.That(status.HasStatusEffect(carried, ParalyzeId), Is.False);
                Assert.That(SEntMan.HasComponent<StunnedComponent>(carried), Is.False);
                Assert.That(SEntMan.HasComponent<KnockedDownComponent>(carried), Is.True);
                Assert.That(SEntMan.GetComponent<StandingStateComponent>(carried).Standing, Is.False);
            });
        });
    }

    [Test]
    public async Task ModifyParalysisTargetsOnlyTheDirectParalyzeContribution()
    {
        EntityUid cleared = default;

        await Server.WaitAssertion(() =>
        {
            var effects = Server.System<SharedEntityEffectsSystem>();
            var status = Server.System<NewStatusEffectsSystem>();
            var stun = Server.System<StunSystem>();
            var sizeStun = Server.System<RMCSizeStunSystem>();

            var cases = new (StatusEffectMetabolismType Type, TimeSpan? Time, TimeSpan? Expected)[]
            {
                (StatusEffectMetabolismType.Add, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4)),
                (StatusEffectMetabolismType.Update, TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(4)),
                (StatusEffectMetabolismType.Set, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1)),
                (StatusEffectMetabolismType.Remove, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1)),
                (StatusEffectMetabolismType.Remove, null, null),
            };

            foreach (var test in cases)
            {
                var target = SSpawn("StunUnconsciousMergeTarget");
                var start = Server.Timing.CurTime;
                Assert.That(stun.TryStun(target, TimeSpan.FromSeconds(12), refresh: true), Is.True);
                Assert.That(stun.TryParalyze(target, TimeSpan.FromSeconds(2), refresh: true), Is.True);
                Assert.That(sizeStun.TryKnockOut(target, TimeSpan.FromSeconds(10)), Is.True);
                Assert.That(status.TryGetTime(target, SharedStunSystem.StunId, out var pureStun), Is.True);
                Assert.That(status.TryGetTime(target, RmcUnconsciousId, out var unconscious), Is.True);

                effects.ApplyEffect(target, new ModifyParalysis
                {
                    Type = test.Type,
                    Time = test.Time,
                });

                Assert.That(status.TryGetTime(target, SharedStunSystem.StunId, out var sameStun), Is.True);
                Assert.That(status.TryGetTime(target, RmcUnconsciousId, out var sameUnconscious), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(sameStun.EffectEnt, Is.EqualTo(pureStun.EffectEnt));
                    Assert.That(sameStun.EndEffectTime, Is.EqualTo(pureStun.EndEffectTime));
                    Assert.That(sameUnconscious.EffectEnt, Is.EqualTo(unconscious.EffectEnt));
                    Assert.That(sameUnconscious.EndEffectTime, Is.EqualTo(unconscious.EndEffectTime),
                        $"ModifyParalysis {test.Type} must not mutate the independent unconscious owner");
                    Assert.That(SEntMan.HasComponent<StunnedComponent>(target), Is.True);
                    Assert.That(SEntMan.HasComponent<KnockedDownComponent>(target), Is.True);
                });

                if (test.Expected is { } expected)
                {
                    Assert.That(status.TryGetTime(target, ParalyzeId, out var paralysis), Is.True);
                    Assert.That(paralysis.EndEffectTime, Is.EqualTo(start + expected));
                }
                else
                {
                    cleared = target;
                }
            }
        });

        await Pair.RunTicksSync(3);
        await Server.WaitAssertion(() =>
        {
            var status = Server.System<NewStatusEffectsSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(status.HasStatusEffect(cleared, ParalyzeId), Is.False,
                    "a null Remove clears only the direct paralysis status");
                Assert.That(status.HasStatusEffect(cleared, SharedStunSystem.StunId), Is.True);
                Assert.That(status.HasStatusEffect(cleared, RmcUnconsciousId), Is.True);
                Assert.That(SEntMan.HasComponent<StunnedComponent>(cleared), Is.True);
                Assert.That(SEntMan.HasComponent<KnockedDownComponent>(cleared), Is.True);
            });
        });
    }

    [Test]
    public async Task ModifyParalysisSetCreatesAndUpdatesItsOwnerSilently()
    {
        await Server.WaitAssertion(() =>
        {
            _ = Server.System<StunUnconsciousMergeProbeSystem>();
            var effects = Server.System<SharedEntityEffectsSystem>();
            var status = Server.System<NewStatusEffectsSystem>();
            var target = SSpawn("StunUnconsciousMergeTarget");
            var probe = SEntMan.GetComponent<StunUnconsciousMergeProbeComponent>(target);
            var start = Server.Timing.CurTime;

            effects.ApplyEffect(target, new ModifyParalysis
            {
                Type = StatusEffectMetabolismType.Set,
                Time = TimeSpan.FromSeconds(2),
            });
            Assert.That(status.TryGetTime(target, ParalyzeId, out var initial), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(initial.EndEffectTime, Is.EqualTo(start + TimeSpan.FromSeconds(2)));
                Assert.That(SEntMan.HasComponent<StunnedComponent>(target), Is.True);
                Assert.That(SEntMan.HasComponent<KnockedDownComponent>(target), Is.True);
                Assert.That(probe.StunnedEvents, Is.Zero);
                Assert.That(probe.KnockedDownEvents, Is.Zero);
                Assert.That(probe.DropEvents, Is.Zero,
                    "fresh Set assigns direct paralysis without replaying application side effects");
            });

            effects.ApplyEffect(target, new ModifyParalysis
            {
                Type = StatusEffectMetabolismType.Set,
                Time = TimeSpan.FromSeconds(4),
            });
            Assert.That(status.TryGetTime(target, ParalyzeId, out var updated), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(updated.EffectEnt, Is.EqualTo(initial.EffectEnt));
                Assert.That(updated.EndEffectTime, Is.EqualTo(start + TimeSpan.FromSeconds(4)));
                Assert.That(probe.StunnedEvents, Is.Zero);
                Assert.That(probe.KnockedDownEvents, Is.Zero);
                Assert.That(probe.DropEvents, Is.Zero,
                    "existing Set updates the owner without replaying application side effects");
            });
        });
    }

    [TestCase(MobState.Critical)]
    [TestCase(MobState.Dead)]
    public async Task IncapacitationClearsDirectParalysis(MobState newState)
    {
        EntityUid target = default;

        await Server.WaitAssertion(() =>
        {
            var stun = Server.System<StunSystem>();
            target = SSpawn("StunUnconsciousMergeTarget");
            Assert.That(stun.TryParalyze(target, TimeSpan.FromSeconds(10), refresh: true), Is.True);

            Server.System<MobStateSystem>().ChangeMobState(target, newState);
        });

        await Pair.RunTicksSync(3);
        await Server.WaitAssertion(() =>
        {
            var status = Server.System<NewStatusEffectsSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(status.HasStatusEffect(target, SharedStunSystem.StunId), Is.False);
                Assert.That(status.HasStatusEffect(target, ParalyzeId), Is.False);
                Assert.That(SEntMan.HasComponent<StunnedComponent>(target), Is.False);
                Assert.That(SEntMan.HasComponent<KnockedDownComponent>(target), Is.False,
                    $"entering {newState} must safely clear direct successor paralysis");
            });
        });
    }
}

[RegisterComponent]
public sealed partial class StunUnconsciousMergeProbeComponent : Component
{
    public int StunnedEvents;
    public int KnockedDownEvents;
    public int DropEvents;
}

public sealed partial class StunUnconsciousMergeProbeSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StunUnconsciousMergeProbeComponent, StunnedEvent>(OnStunned);
        SubscribeLocalEvent<StunUnconsciousMergeProbeComponent, KnockedDownEvent>(OnKnockedDown);
        SubscribeLocalEvent<StunUnconsciousMergeProbeComponent, DropHandItemsEvent>(OnDrop);
    }

    private void OnStunned(Entity<StunUnconsciousMergeProbeComponent> ent, ref StunnedEvent args)
    {
        ent.Comp.StunnedEvents++;
    }

    private void OnKnockedDown(Entity<StunUnconsciousMergeProbeComponent> ent, ref KnockedDownEvent args)
    {
        ent.Comp.KnockedDownEvents++;
    }

    private void OnDrop(Entity<StunUnconsciousMergeProbeComponent> ent, ref DropHandItemsEvent args)
    {
        ent.Comp.DropEvents++;
    }
}
