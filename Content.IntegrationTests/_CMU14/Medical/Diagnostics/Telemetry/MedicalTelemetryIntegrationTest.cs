using System.Linq;
using Content.Shared._CMU14.Medical.Injuries.Shrapnel;
using Content.Shared._RMC14.Medical.Defibrillator;
using Content.Shared._RMC14.Medical.Surgery;
using Content.Shared.Body.Systems;
using Content.Shared.GameTicking;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._CMU14.Medical.Diagnostics.Telemetry;

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
