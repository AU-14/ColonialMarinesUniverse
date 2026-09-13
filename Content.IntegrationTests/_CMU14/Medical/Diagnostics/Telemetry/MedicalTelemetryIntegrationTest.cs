using System.Linq;
using Content.Shared.CMU14.Medical.Anatomy.Bones;
using Content.Shared.CMU14.Medical.Anatomy.Organs;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Events;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Liver;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Injuries.Shrapnel;
using Content.Shared._RMC14.Medical.Defibrillator;
using Content.Shared._RMC14.Medical.Surgery;
using Content.Shared.Body.Systems;
using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.GameTicking;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Medical.Diagnostics.Telemetry;

[TestFixture]
public sealed class MedicalTelemetryIntegrationTest
{
    [Test]
    public async Task FractureEscalationAndRecoveryCountOneBreakPerEpisode()
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var patient = entities.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            try
            {
                var index = entities.System<CMUMedicalBodyIndexSystem>();
                Assert.That(index.TryGetBodyPart(patient,
                    new CMUMedicalBodyPartKey(BodyPartType.Arm, BodyPartSymmetry.Left), out var arm), Is.True);
                var bones = entities.System<SharedBoneSystem>();
                var fractures = entities.System<SharedFractureSystem>();
                const string label = "round-end-summary-window-stat-bones-broken";
                var initial = GetStatValue(GetSummaryStats(entities).InjuryStats, label);

                Assert.That(bones.SeedFracture(arm, FractureSeverity.Hairline), Is.True);
                AssertStatValue(GetSummaryStats(entities).InjuryStats, label, initial + 1);
                Assert.That(bones.SeedFracture(arm, FractureSeverity.Compound), Is.True);
                Assert.That(bones.SeedFracture(arm, FractureSeverity.Shattered), Is.True);
                AssertStatValue(GetSummaryStats(entities).InjuryStats, label, initial + 1);

                var fracture = entities.GetComponent<FractureComponent>(arm);
                fractures.SetSeverity((arm, fracture), FractureSeverity.Simple, forceUpgrade: false);
                fractures.SetSeverity((arm, fracture), FractureSeverity.None);
                Assert.That(entities.HasComponent<FractureComponent>(arm), Is.False);
                AssertStatValue(GetSummaryStats(entities).InjuryStats, label, initial + 1);
                Assert.That(bones.SeedFracture(arm, FractureSeverity.Shattered), Is.True);
                AssertStatValue(GetSummaryStats(entities).InjuryStats, label, initial + 2);
            }
            finally
            {
                entities.DeleteEntity(patient);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task OrganRecoveryAndEscalationDoNotCreateAnotherCrisis()
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var patient = entities.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            try
            {
                Assert.That(entities.System<CMUMedicalBodyIndexSystem>().TryGetOrgan<LiverComponent>(patient, out var liver), Is.True);
                var organs = entities.System<SharedOrganHealthSystem>();
                var health = entities.GetComponent<OrganHealthComponent>(liver);
                const string label = "round-end-summary-window-stat-organ-crises";
                var initial = GetStatValue(GetSummaryStats(entities).InjuryStats, label);

                InjureTo(entities, patient, liver, OrganDamageStage.Bruised);
                InjureTo(entities, patient, liver, OrganDamageStage.Damaged);
                AssertStatValue(GetSummaryStats(entities).InjuryStats, label, initial);
                InjureTo(entities, patient, liver, OrganDamageStage.Failing);
                AssertStatValue(GetSummaryStats(entities).InjuryStats, label, initial + 1);
                InjureTo(entities, patient, liver, OrganDamageStage.Dead);
                organs.HealOrgan(liver, patient, health.StageThresholds[OrganDamageStage.Failing]);
                Assert.That(health.Stage, Is.EqualTo(OrganDamageStage.Failing));
                AssertStatValue(GetSummaryStats(entities).InjuryStats, label, initial + 1);
                organs.HealOrgan(liver, patient, health.Max);
                Assert.That(health.Stage, Is.EqualTo(OrganDamageStage.Healthy));
                AssertStatValue(GetSummaryStats(entities).InjuryStats, label, initial + 1);
                InjureTo(entities, patient, liver, OrganDamageStage.Dead);
                AssertStatValue(GetSummaryStats(entities).InjuryStats, label, initial + 2);
            }
            finally
            {
                entities.DeleteEntity(patient);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RoundRestartClearsMedicalSummaryCounters()
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var patient = entities.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var index = entities.System<CMUMedicalBodyIndexSystem>();
            Assert.That(index.TryGetOrgan<LiverComponent>(patient, out var liver), Is.True);
            Assert.That(index.TryGetBodyPart(patient,
                new CMUMedicalBodyPartKey(BodyPartType.Arm, BodyPartSymmetry.Left), out var arm), Is.True);
            Assert.That(entities.System<SharedBoneSystem>().SeedFracture(arm, FractureSeverity.Hairline), Is.True);
            InjureTo(entities, patient, liver, OrganDamageStage.Failing);
            entities.DeleteEntity(patient);
            var before = GetSummaryStats(entities);
            Assert.That(GetStatValue(before.InjuryStats, "round-end-summary-window-stat-bones-broken"), Is.Positive);
            Assert.That(GetStatValue(before.InjuryStats, "round-end-summary-window-stat-organ-crises"), Is.Positive);

            entities.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());
            var after = GetSummaryStats(entities);
            Assert.Multiple(() =>
            {
                foreach (var stat in after.InjuryStats.Concat(after.OddityStats))
                    Assert.That(stat.Value, Is.Zero, stat.Label);
            });
        });
        await pair.CleanReturnAsync();
    }

    private static void InjureTo(IEntityManager entities, EntityUid patient, EntityUid organ, OrganDamageStage stage)
    {
        var health = entities.GetComponent<OrganHealthComponent>(organ);
        var amount = health.Current - health.StageThresholds[stage];
        Assert.That(amount, Is.GreaterThan(FixedPoint2.Zero));
        var damage = new DamageSpecifier { DamageDict = { ["Blunt"] = amount } };
        var ev = new OrganDamagedEvent(patient, organ, damage, OrganDamageSource.Direct);
        entities.EventBus.RaiseLocalEvent(organ, ref ev, broadcast: true);
        Assert.That(health.Stage, Is.EqualTo(stage));
    }

    [Test]
    public async Task RoundEndStatsIncludeDirectedMedicalEvents()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var body = entMan.System<SharedBodySystem>();
            var patient = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var surgeon = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var part = body.GetBodyChildren(patient).First().Id;
                var baselineStats = GetSummaryStats(entMan);
                var baselineSurgeries = GetStatValue(baselineStats.InjuryStats, "round-end-summary-window-stat-surgeries");
                var baselineDefibs = GetStatValue(baselineStats.InjuryStats, "round-end-summary-window-stat-defibs");
                var baselineShrapnelEmbedded = GetStatValue(baselineStats.OddityStats, "round-end-summary-window-stat-shrapnel-embedded");
                var baselineShrapnelExtracted = GetStatValue(baselineStats.OddityStats, "round-end-summary-window-stat-shrapnel-extracted");

                var surgery = new CMSurgeryCompleteEvent(patient, surgeon, "CMUTelemetryTestSurgery");
                entMan.EventBus.RaiseLocalEvent(patient, ref surgery);

                var defib = new RMCDefibrillatorAttemptEvent(patient);
                entMan.EventBus.RaiseLocalEvent(patient, defib);

                var embedded = new CMUShrapnelChangedEvent(patient, part, false);
                entMan.EventBus.RaiseLocalEvent(part, ref embedded);

                var extracted = new CMUShrapnelChangedEvent(patient, part, true);
                entMan.EventBus.RaiseLocalEvent(part, ref extracted);

                var stats = GetSummaryStats(entMan);

                Assert.Multiple(() =>
                {
                    AssertStatValue(stats.InjuryStats, "round-end-summary-window-stat-surgeries", baselineSurgeries + 1);
                    AssertStatValue(stats.InjuryStats, "round-end-summary-window-stat-defibs", baselineDefibs + 1);
                    AssertStatValue(stats.OddityStats, "round-end-summary-window-stat-shrapnel-embedded", baselineShrapnelEmbedded + 1);
                    AssertStatValue(stats.OddityStats, "round-end-summary-window-stat-shrapnel-extracted", baselineShrapnelExtracted + 1);
                });
            }
            finally
            {
                entMan.DeleteEntity(patient);
                entMan.DeleteEntity(surgeon);
            }
        });

        await pair.CleanReturnAsync();
    }

    private static RoundEndSummaryStats GetSummaryStats(IEntityManager entMan)
    {
        var statsEv = new RoundEndSummaryStatsEvent();
        entMan.EventBus.RaiseEvent(EventSource.Local, statsEv);
        return statsEv.ToSummaryStats();
    }

    private static int GetStatValue(RoundEndSummaryStat[] stats, string label)
    {
        var stat = stats.SingleOrDefault(s => s.Label == label);

        Assert.That(stat.Label, Is.EqualTo(label), $"Missing {label}");
        return stat.Value;
    }

    private static void AssertStatValue(RoundEndSummaryStat[] stats, string label, int value)
    {
        Assert.That(GetStatValue(stats, label), Is.EqualTo(value), label);
    }
}
