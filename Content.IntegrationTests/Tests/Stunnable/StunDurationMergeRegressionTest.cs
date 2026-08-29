using Content.IntegrationTests.Fixtures;
using Content.Server.Stunnable;
using Content.Shared._CMU14.Chemistry.Effects;
using Content.Shared._RMC14.StatusEffect;
using Content.Shared.Hands.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.StatusEffect;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using NewStatusEffectsSystem = Content.Shared.StatusEffectNew.StatusEffectsSystem;

namespace Content.IntegrationTests.Tests.Stunnable;

[TestFixture]
[TestOf(typeof(SharedStunSystem))]
public sealed class StunDurationMergeRegressionTest : GameTest
{
    private static readonly EntProtoId ParalyzeId = SharedStunSystem.ParalyzeId;

    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          parent: MobHuman
          id: StunDurationMergeTarget
          components:
          - type: StunDurationMergeProbe

        - type: entity
          parent: StunDurationMergeTarget
          id: StunDurationMergeImmune
          components:
          - type: Tag
            tags:
            - StunImmune

        - type: entity
          parent:
          - BaseMob
          - MobDamageable
          id: StunDurationMergeNoHands
          components:
          - type: StandingState
        """;

    [Test]
    public async Task ExistingStatusPreflightForceAndPrototypeFiltersRemainOrdered()
    {
        await Server.WaitAssertion(() =>
        {
            _ = Server.System<StunDurationMergeProbeSystem>();
            var stun = Server.System<StunSystem>();
            var status = Server.System<NewStatusEffectsSystem>();
            var target = SSpawn("StunDurationMergeTarget");
            var immune = SSpawn("StunDurationMergeImmune");
            var probe = SEntMan.GetComponent<StunDurationMergeProbeComponent>(target);
            probe.CancelStatus = true;

            Assert.That(stun.TryStun(target, TimeSpan.FromSeconds(2), refresh: true), Is.False,
                "normal application must obey BeforeStatusEffectAddedEvent");
            Assert.That(status.HasStatusEffect(target, SharedStunSystem.StunId), Is.False);

            Assert.That(stun.TryStun(target, TimeSpan.FromSeconds(2), refresh: true, force: true), Is.True,
                "force bypasses only the attempt event");
            Assert.That(status.TryGetTime(target, SharedStunSystem.StunId, out var initial), Is.True);

            Assert.That(stun.TryUpdateStunDuration(target, TimeSpan.FromSeconds(4)), Is.False,
                "existing-effect refresh must still perform the normal preflight");
            Assert.That(status.TryGetTime(target, SharedStunSystem.StunId, out var cancelled), Is.True);
            Assert.That(cancelled.EndEffectTime, Is.EqualTo(initial.EndEffectTime));

            var beforeForce = Server.Timing.CurTime;
            Assert.That(stun.TryUpdateStunDuration(target, TimeSpan.FromSeconds(4), force: true), Is.True);
            Assert.That(status.TryGetTime(target, SharedStunSystem.StunId, out var forced), Is.True);
            Assert.That(forced.EndEffectTime, Is.EqualTo(beforeForce + TimeSpan.FromSeconds(4)));

            Assert.That(stun.TryStun(immune, TimeSpan.FromSeconds(2), refresh: true, force: true), Is.False,
                "force must not bypass the status prototype's StunImmune blacklist");
            Assert.That(status.HasStatusEffect(immune, SharedStunSystem.StunId), Is.False);
            Assert.Multiple(() =>
            {
                Assert.That(probe.BeforeStatusCalls, Is.EqualTo(2),
                    "force bypasses BeforeStatusEffectAddedEvent instead of invoking a cancelled attempt");
                Assert.That(SEntMan.HasComponent<StunnedComponent>(target), Is.True);
            });
        });
    }

    [Test]
    public async Task PublicParalysisApisHonorKnockdownImmunityAndForceOnlyThatAttempt()
    {
        await Server.WaitAssertion(() =>
        {
            _ = Server.System<StunDurationMergeProbeSystem>();
            var stun = Server.System<StunSystem>();
            var status = Server.System<NewStatusEffectsSystem>();

            void AssertApi(
                Func<EntityUid, bool, bool> apply,
                string api)
            {
                var target = SSpawn("StunDurationMergeTarget");
                SEntMan.RemoveComponent<CrawlerComponent>(target);
                var probe = SEntMan.GetComponent<StunDurationMergeProbeComponent>(target);
                probe.CancelKnockdown = true;

                Assert.That(apply(target, false), Is.False, $"{api} must honor KnockDownAttemptEvent");
                Assert.That(status.HasStatusEffect(target, ParalyzeId), Is.False);
                Assert.That(SEntMan.HasComponent<KnockedDownComponent>(target), Is.False);

                Assert.That(apply(target, true), Is.True, $"{api} force should bypass KnockDownAttemptEvent");
                Assert.That(status.HasStatusEffect(target, ParalyzeId), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(probe.KnockdownAttemptCalls, Is.EqualTo(2));
                    Assert.That(probe.BeforeStatusCalls, Is.Zero,
                        "the rejected knockdown stops before status preflight and force bypasses that preflight");
                });
            }

            AssertApi(
                (uid, force) => stun.TryParalyze(uid, TimeSpan.FromSeconds(2), refresh: true, force),
                nameof(stun.TryParalyze));
            AssertApi(
                (uid, force) => stun.TryAddParalyzeDuration(uid, TimeSpan.FromSeconds(2), force: force),
                nameof(stun.TryAddParalyzeDuration));
            AssertApi(
                (uid, force) => stun.TryUpdateParalyzeDuration(uid, TimeSpan.FromSeconds(2), force: force),
                nameof(stun.TryUpdateParalyzeDuration));

            var noHands = SSpawn("StunDurationMergeNoHands");
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<HandsComponent>(noHands), Is.False);
                Assert.That(SEntMan.HasComponent<StatusEffectsComponent>(noHands), Is.False,
                    "the successor path must not require the legacy status container");
            });
            Assert.That(stun.TryParalyze(noHands, TimeSpan.FromSeconds(2), refresh: true), Is.True,
                "drop/disarm side effects must tolerate a mob with no hands");
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<StunnedComponent>(noHands), Is.True);
                Assert.That(SEntMan.HasComponent<KnockedDownComponent>(noHands), Is.True);
            });
        });
    }

    [Test]
    public async Task ChemicalAndRmcTransformsApplyOnceWithMaxRefreshAndStackContracts()
    {
        await Server.WaitAssertion(() =>
        {
            _ = Server.System<StunDurationMergeProbeSystem>();
            var stun = Server.System<StunSystem>();
            var status = Server.System<NewStatusEffectsSystem>();

            var paralyzed = SSpawn("StunDurationMergeTarget");
            SEntMan.RemoveComponent<CrawlerComponent>(paralyzed);
            var paralyzeProbe = SEntMan.GetComponent<StunDurationMergeProbeComponent>(paralyzed);
            paralyzeProbe.ChemicalMultiplier = 0.5f;
            paralyzeProbe.StunMultiplier = 2f;
            paralyzeProbe.KnockdownMultiplier = 3f;
            var paralyzeStart = Server.Timing.CurTime;

            Assert.That(stun.TryParalyze(paralyzed, TimeSpan.FromSeconds(2), refresh: true), Is.True);
            Assert.That(status.TryGetTime(paralyzed, ParalyzeId, out var paralyzeTime), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(paralyzeTime.EndEffectTime, Is.EqualTo(paralyzeStart + TimeSpan.FromSeconds(3)),
                    "paralysis uses max(independent Stun, KnockedDown) after one chemical transform");
                Assert.That(paralyzeProbe.ChemicalCalls, Is.EqualTo(1));
                Assert.That(paralyzeProbe.StunCalls, Is.EqualTo(1));
                Assert.That(paralyzeProbe.KnockdownCalls, Is.EqualTo(1));
            });

            var crawler = SSpawn("StunDurationMergeTarget");
            SEntMan.EnsureComponent<CrawlerComponent>(crawler);
            var crawlerProbe = SEntMan.GetComponent<StunDurationMergeProbeComponent>(crawler);
            SetTransform(crawlerProbe);
            var crawlerStart = Server.Timing.CurTime;
            Assert.That(stun.TryKnockdown(crawler, TimeSpan.FromSeconds(2)), Is.True);
            var crawlerDown = SEntMan.GetComponent<KnockedDownComponent>(crawler);
            Assert.Multiple(() =>
            {
                Assert.That(crawlerDown.NextUpdate, Is.EqualTo(crawlerStart + TimeSpan.FromSeconds(3)));
                AssertTransformCounts(crawlerProbe, chemical: 1, stun: 0, knocked: 1);
                Assert.That(status.HasStatusEffect(crawler, ParalyzeId), Is.False,
                    "a crawler uses its native knockdown timer rather than paralysis");
            });

            var nonCrawler = SSpawn("StunDurationMergeTarget");
            SEntMan.RemoveComponent<CrawlerComponent>(nonCrawler);
            var nonCrawlerProbe = SEntMan.GetComponent<StunDurationMergeProbeComponent>(nonCrawler);
            SetTransform(nonCrawlerProbe);
            var nonCrawlerStart = Server.Timing.CurTime;
            Assert.That(stun.TryKnockdown(nonCrawler, TimeSpan.FromSeconds(2)), Is.True);
            Assert.That(status.TryGetTime(nonCrawler, ParalyzeId, out var nonCrawlerTime), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(nonCrawlerTime.EndEffectTime, Is.EqualTo(nonCrawlerStart + TimeSpan.FromSeconds(3)));
                AssertTransformCounts(nonCrawlerProbe, chemical: 1, stun: 0, knocked: 1);
            });

            var stacked = SSpawn("StunDurationMergeTarget");
            var start = Server.Timing.CurTime;
            Assert.That(stun.TryStun(stacked, TimeSpan.FromSeconds(4), refresh: true), Is.True);
            Assert.That(stun.TryStun(stacked, TimeSpan.FromSeconds(2), refresh: true), Is.True);
            Assert.That(status.TryGetTime(stacked, SharedStunSystem.StunId, out var refreshed), Is.True);
            Assert.That(refreshed.EndEffectTime, Is.EqualTo(start + TimeSpan.FromSeconds(4)),
                "refresh takes the maximum remaining duration");
            Assert.That(stun.TryStun(stacked, TimeSpan.FromSeconds(2), refresh: false), Is.True);
            Assert.That(status.TryGetTime(stacked, SharedStunSystem.StunId, out var added), Is.True);
            Assert.That(added.EndEffectTime, Is.EqualTo(start + TimeSpan.FromSeconds(6)),
                "refresh=false adds to the existing expiry");

            var nonPositive = SSpawn("StunDurationMergeTarget");
            Assert.That(stun.TryStun(nonPositive, TimeSpan.FromSeconds(5), refresh: true), Is.True);
            Assert.That(status.TryGetTime(nonPositive, SharedStunSystem.StunId, out var original), Is.True);
            var nonPositiveProbe = SEntMan.GetComponent<StunDurationMergeProbeComponent>(nonPositive);
            nonPositiveProbe.ChemicalMultiplier = 0f;
            Assert.Multiple(() =>
            {
                Assert.That(stun.TryAddStunDuration(nonPositive, TimeSpan.FromSeconds(2)), Is.False);
                Assert.That(stun.TryUpdateStunDuration(nonPositive, TimeSpan.FromSeconds(2)), Is.False);
                Assert.That(stun.TryParalyze(nonPositive, TimeSpan.FromSeconds(2), refresh: true), Is.False);
            });
            Assert.That(status.TryGetTime(nonPositive, SharedStunSystem.StunId, out var unchanged), Is.True);
            Assert.That(unchanged.EndEffectTime, Is.EqualTo(original.EndEffectTime),
                "a post-transform nonpositive duration must not shorten an existing effect");

            var noCreate = SSpawn("StunDurationMergeTarget");
            SEntMan.RemoveComponent<CrawlerComponent>(noCreate);
            SEntMan.GetComponent<StunDurationMergeProbeComponent>(noCreate).ChemicalMultiplier = 0f;
            Assert.That(stun.TryParalyze(noCreate, TimeSpan.FromSeconds(2), refresh: true), Is.False);
            Assert.Multiple(() =>
            {
                Assert.That(status.HasStatusEffect(noCreate, ParalyzeId), Is.False);
                Assert.That(SEntMan.HasComponent<KnockedDownComponent>(noCreate), Is.False);
            });
        });
    }

    private static void SetTransform(StunDurationMergeProbeComponent probe)
    {
        probe.ChemicalMultiplier = 0.5f;
        probe.StunMultiplier = 2f;
        probe.KnockdownMultiplier = 3f;
    }

    private static void AssertTransformCounts(
        StunDurationMergeProbeComponent probe,
        int chemical,
        int stun,
        int knocked)
    {
        Assert.Multiple(() =>
        {
            Assert.That(probe.ChemicalCalls, Is.EqualTo(chemical));
            Assert.That(probe.StunCalls, Is.EqualTo(stun));
            Assert.That(probe.KnockdownCalls, Is.EqualTo(knocked));
        });
    }
}

[RegisterComponent]
public sealed partial class StunDurationMergeProbeComponent : Component
{
    public float ChemicalMultiplier = 1f;
    public float StunMultiplier = 1f;
    public float KnockdownMultiplier = 1f;
    public bool CancelStatus;
    public bool CancelKnockdown;
    public int ChemicalCalls;
    public int StunCalls;
    public int KnockdownCalls;
    public int BeforeStatusCalls;
    public int KnockdownAttemptCalls;
}

public sealed partial class StunDurationMergeProbeSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StunDurationMergeProbeComponent, GetChemicalStunTimeMultiplierEvent>(OnChemical);
        SubscribeLocalEvent<StunDurationMergeProbeComponent, RMCStatusEffectTimeEvent>(OnRmcDuration);
        SubscribeLocalEvent<StunDurationMergeProbeComponent, BeforeStatusEffectAddedEvent>(OnBeforeStatus);
        SubscribeLocalEvent<StunDurationMergeProbeComponent, KnockDownAttemptEvent>(OnKnockdownAttempt);
    }

    private void OnChemical(
        Entity<StunDurationMergeProbeComponent> ent,
        ref GetChemicalStunTimeMultiplierEvent args)
    {
        ent.Comp.ChemicalCalls++;
        args.Multiplier *= ent.Comp.ChemicalMultiplier;
    }

    private void OnRmcDuration(
        Entity<StunDurationMergeProbeComponent> ent,
        ref RMCStatusEffectTimeEvent args)
    {
        switch (args.Key)
        {
            case "Stun":
                ent.Comp.StunCalls++;
                args.Duration *= ent.Comp.StunMultiplier;
                break;
            case "KnockedDown":
                ent.Comp.KnockdownCalls++;
                args.Duration *= ent.Comp.KnockdownMultiplier;
                break;
        }
    }

    private void OnBeforeStatus(
        Entity<StunDurationMergeProbeComponent> ent,
        ref BeforeStatusEffectAddedEvent args)
    {
        ent.Comp.BeforeStatusCalls++;
        if (ent.Comp.CancelStatus)
            args.Cancelled = true;
    }

    private void OnKnockdownAttempt(
        Entity<StunDurationMergeProbeComponent> ent,
        ref KnockDownAttemptEvent args)
    {
        ent.Comp.KnockdownAttemptCalls++;
        if (ent.Comp.CancelKnockdown)
            args.Cancelled = true;
    }
}
