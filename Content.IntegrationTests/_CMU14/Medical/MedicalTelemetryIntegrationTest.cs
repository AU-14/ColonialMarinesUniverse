using System.Linq;
using Content.Shared._CMU14.Medical.Human.Damage.Shrapnel;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared._CMU14.Medical.Human.Surgery;
using Content.Shared._RMC14.Medical.Defibrillator;
using Content.Shared.GameTicking;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._CMU14.Medical;

[TestFixture]
public sealed class MedicalTelemetryIntegrationTest
{
    [Test]
    public async Task RoundEndStatsIncludeDirectedMedicalEvents()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var patient = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var surgeon = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var baselineStats = GetSummaryStats(entMan);
                var baselineSurgeries = GetStatValue(baselineStats.InjuryStats, "round-end-summary-window-stat-surgeries");
                var baselineDefibs = GetStatValue(baselineStats.InjuryStats, "round-end-summary-window-stat-defibs");
                var baselineShrapnelEmbedded = GetStatValue(baselineStats.OddityStats, "round-end-summary-window-stat-shrapnel-embedded");
                var baselineShrapnelExtracted = GetStatValue(baselineStats.OddityStats, "round-end-summary-window-stat-shrapnel-extracted");

                var attempt = new SurgeryAttempt(
                    BodyRegion.Chest,
                    SurgeryStepKind.OpenIncision,
                    ProcedureId: SurgeryProcedureId.SurgicalAccess);
                var result = new SurgeryResult(
                    Applied: true,
                    DirtyFlags: MedicalDirtyFlags.Summary,
                    FailureReason: string.Empty);
                var surgery = new HumanSurgeryAppliedEvent(attempt, result, surgeon);
                entMan.EventBus.RaiseLocalEvent(patient, ref surgery);

                var defib = new RMCDefibrillatorAttemptEvent(patient);
                entMan.EventBus.RaiseEvent(EventSource.Local, defib);

                var embedded = new CMUShrapnelChangedEvent(patient, BodyRegion.Chest, false);
                entMan.EventBus.RaiseLocalEvent(patient, ref embedded);

                var extracted = new CMUShrapnelChangedEvent(patient, BodyRegion.Chest, true);
                entMan.EventBus.RaiseLocalEvent(patient, ref extracted);

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
