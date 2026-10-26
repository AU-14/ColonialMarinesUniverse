using System;
using System.Collections.Generic;
using System.Linq;
using Content.Shared.CMU14.RoundStatistics;

namespace Content.Server.Database;

public abstract partial class ServerDbBase
{
    public static CMURoundStatisticsDashboard BuildCMURoundStatisticsDashboard(
        List<CMURoundOutcomeRecord> records,
        CMURoundStatisticsQuery query)
    {
        var preset = Enum.IsDefined(query.Preset) ? query.Preset : CMURoundStatisticsPreset.DistressSignal;
        var planet = string.IsNullOrWhiteSpace(query.PlanetId) ? null : query.PlanetId.Trim();
        var threat = string.IsNullOrWhiteSpace(query.ThreatId) ? null : query.ThreatId.Trim();
        var search = query.Search.Trim();
        if (search.Length > CMURoundStatisticsQuery.MaxSearchLength)
            search = search[..CMURoundStatisticsQuery.MaxSearchLength];

        var modes = new List<CMURoundModeStatistics>
        {
            BuildModeStatistics(records, CMURoundStatisticsPreset.DistressSignal, "Distress Signal", "Xeno", "Govfor"),
            BuildModeStatistics(records, CMURoundStatisticsPreset.Insurgency, "Insurgency", "Govfor", "CLF"),
            BuildModeStatistics(records, CMURoundStatisticsPreset.ColonyFall, "Colony Fall", "Colonists", "Threat"),
            BuildModeStatistics(records, CMURoundStatisticsPreset.ForceOnForce, "Force on Force", "Govfor", "Opfor"),
        };
        var fullMode = modes.Single(mode => mode.Preset == preset);
        var band = fullMode.PlayerCountBands.FirstOrDefault(candidate => candidate.Band == query.PopulationBand);
        var scoped = records.Where(record => record.Preset == preset &&
                (planet == null || record.PlanetId?.Trim() == planet) &&
                (threat == null || record.SelectedThreatId?.Trim() == threat) &&
                (band.Band == null || record.PlayerCount >= band.MinPlayers &&
                    (band.MaxPlayers < 0 || record.PlayerCount <= band.MaxPlayers)))
            .OrderByDescending(record => record.RecordedAt).ThenByDescending(record => record.RoundId).ToList();
        var selected = planet == null && threat == null && band.Band == null
            ? fullMode : BuildModeStatistics(scoped, preset, fullMode.Title, fullMode.SideA, fullMode.SideB);
        var archive = scoped.Where(record => MatchesCMURoundSearch(record, search)).ToList();
        var page = Math.Clamp(query.Page, 0, Math.Max(0, (archive.Count - 1) / CMURoundStatisticsQuery.PageSize));
        var dashboard = new CMURoundStatisticsDashboard(modes,
            archive.Skip(page * CMURoundStatisticsQuery.PageSize).Take(CMURoundStatisticsQuery.PageSize).ToList())
        {
            Query = new CMURoundStatisticsQuery(preset, planet, search, page, threat, band.Band),
            SelectedMode = selected,
            AvailablePlanets = fullMode.Planets,
            ArchiveTotal = archive.Count,
            TotalRecords = records.Count,
            LatestRoundId = records.Count == 0 ? 0 : records.Max(record => record.RoundId),
            FirstRecordedAt = records.Count == 0 ? null : records.Min(record => record.RecordedAt),
        };

        foreach (var againstThreat in new[] { false, true })
        {
            // Missing pairing metadata stays in the overview, never in an invented matchup.
            var pairs = scoped.Where(record => !string.IsNullOrWhiteSpace(record.GovforPlatoonId) &&
                    !string.IsNullOrWhiteSpace(againstThreat ? record.SelectedThreatId : record.OpforPlatoonId))
                .GroupBy(record => (Platoon: record.GovforPlatoonId!.Trim(),
                    Opponent: (againstThreat ? record.SelectedThreatId : record.OpforPlatoonId)!.Trim()))
                .OrderByDescending(group => group.Count()).ThenBy(group => group.Key.Platoon, StringComparer.Ordinal)
                .ThenBy(group => group.Key.Opponent, StringComparer.Ordinal);
            foreach (var pair in pairs)
            {
                dashboard.Matchups.Add(new CMURoundMatchupStatistics(againstThreat, pair.Key.Platoon, pair.Key.Opponent,
                    BuildModeStatistics(pair.ToList(), preset, fullMode.Title, fullMode.SideA, fullMode.SideB)));
            }
        }

        return dashboard;
    }

    private static bool MatchesCMURoundSearch(CMURoundOutcomeRecord record, string search)
    {
        bool Contains(string? value) => value?.Contains(search, StringComparison.OrdinalIgnoreCase) == true;
        return search.Length == 0 || Contains(record.RoundId.ToString()) || Contains(record.PlanetId) ||
               Contains(record.GovforPlatoonId) || Contains(record.OpforPlatoonId) || Contains(record.SelectedThreatId) ||
               Contains(record.Source);
    }
}
