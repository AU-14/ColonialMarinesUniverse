using System;
using System.Linq;
using Content.Client.Lobby.UI;
using Content.Shared._RMC14.Rules;
using Content.Shared.CMU14.RoundStatistics;
using Content.Shared.CMU14.util;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Client.CMU14.RoundStatistics;

public sealed partial class CMURoundStatisticsWindow
{
    private string PlanetName(string id)
    {
        if (!_prototypes.TryIndex<EntityPrototype>(id, out var prototype))
            return id;
        if (prototype.Components.TryGetValue("RMCPlanetMapPrototype", out var entry) &&
            entry.Component is RMCPlanetMapPrototypeComponent { VoteName: { Length: > 0 } name })
            return name;
        return prototype.Name;
    }

    private string PlatoonName(string id) => _prototypes.TryIndex<PlatoonPrototype>(id, out var prototype)
        ? Loc.GetString(prototype.Name) : id;

    private string ThreatName(string id) => _loc.TryGetString(id, out var name) ? name : id;

    private void RenderPlanets(CMURoundModeStatistics mode, bool interactive = true)
    {
        var header = DataRow();
        header.AddChild(Cell(Loc.GetString("cmu-round-stats-location"), 240));
        header.AddChild(Hint(mode.SideA + " / " + mode.SideB));
        header.AddChild(Cell(Loc.GetString("cmu-round-stats-endings"), 65, true));
        header.AddChild(Cell(Loc.GetString("cmu-round-stats-avg-time"), 100, true));
        header.AddChild(Cell("", 16));
        AddDataRow(header);
        foreach (var planet in mode.Planets)
        {
            var row = DataRow();
            row.MinHeight = 52;
            row.AddChild(WrappedCell(PlanetName(planet.PlanetId), 240));
            row.AddChild(RateCell(mode, planet.SideAWins, planet.SideBWins, planet.Draws, planet.Unknown));
            row.AddChild(Cell(planet.Total.ToString(), 65, true));
            row.AddChild(Cell(FormatDurationOrNone(planet.AverageDurationSeconds), 100, true));
            row.AddChild(Cell(interactive ? "›" : "", 16));
            AddDataRow(row, interactive ? () => OpenDetail("location-file", PlanetName(planet.PlanetId),
                new CMURoundStatisticsQuery(_query.Preset, planet.PlanetId)) : null);
        }
        if (mode.Total - mode.Planets.Sum(planet => planet.Total) is > 0 and var missing)
            _body.AddChild(Hint(Loc.GetString("cmu-round-stats-missing-planets", ("count", missing))));
        if (mode.Planets.Count == 0)
            _body.AddChild(MakeEmptyPanel(Loc.GetString("cmu-round-stats-no-planets")));
        AddLegend(mode);
    }

    private void RenderPopulation(CMURoundModeStatistics mode, bool interactive = true)
    {
        var header = DataRow();
        header.AddChild(Cell(Loc.GetString("cmu-round-stats-population"), 110));
        header.AddChild(Hint(Loc.GetString("cmu-round-stats-endings")));
        header.AddChild(Cell("", 55));
        header.AddChild(Hint(mode.SideA + " / " + mode.SideB));
        header.AddChild(Cell(mode.SideA, 65, true));
        header.AddChild(Cell("", 16));
        AddDataRow(header);
        var max = mode.PlayerCountBands.Count == 0 ? 1 : mode.PlayerCountBands.Max(band => band.Total);
        foreach (var band in mode.PlayerCountBands)
        {
            var row = DataRow();
            row.MinHeight = 34;
            row.AddChild(Cell(band.Band, 110));
            row.AddChild(Frequency(band.Total, max));
            row.AddChild(Cell(band.Total.ToString(), 55, true));
            row.AddChild(CompactBar(mode.Preset, band.SideAWins, band.SideBWins, band.Draws, band.Unknown));
            row.AddChild(Cell(FormatRate(band.SideAWins, band.SideAWins + band.SideBWins), 65, true));
            row.AddChild(Cell(interactive ? "›" : "", 16));
            AddDataRow(row, interactive ? () => OpenDetail("population-file",
                Loc.GetString("cmu-round-stats-players", ("band", band.Band)),
                new CMURoundStatisticsQuery(_query.Preset, populationBand: band.Band)) : null);
        }
        if (mode.PlayerCountBands.Count == 0)
            _body.AddChild(MakeEmptyPanel(Loc.GetString("cmu-round-stats-empty")));
        AddLegend(mode);
    }

    private void RenderThreats(CMURoundModeStatistics mode)
    {
        var header = DataRow();
        header.AddChild(Cell(Loc.GetString("cmu-round-stats-threats"), 240));
        header.AddChild(Hint(mode.SideA + " / " + mode.SideB));
        header.AddChild(Cell(Loc.GetString("cmu-round-stats-endings"), 65, true));
        header.AddChild(Cell(Loc.GetString("cmu-round-stats-decided"), 100, true));
        header.AddChild(Cell("", 16));
        AddDataRow(header);
        foreach (var threat in mode.Threats)
        {
            var row = DataRow();
            row.MinHeight = 52;
            row.AddChild(WrappedCell(ThreatName(threat.ThreatId), 240));
            row.AddChild(RateCell(mode, threat.SideAWins, threat.SideBWins, threat.Draws, threat.Unknown));
            row.AddChild(Cell(threat.Total.ToString(), 65, true));
            row.AddChild(Cell((threat.SideAWins + threat.SideBWins).ToString(), 100, true));
            row.AddChild(Cell("›", 16));
            AddDataRow(row, () => OpenDetail("threat-file", ThreatName(threat.ThreatId),
                new CMURoundStatisticsQuery(_query.Preset, threatId: threat.ThreatId)));
        }
        var missing = mode.Total - mode.Threats.Sum(threat => threat.Total);
        if (missing > 0)
            _body.AddChild(Hint(Loc.GetString("cmu-round-stats-missing-threats", ("count", missing))));
        if (mode.Threats.Count == 0)
            _body.AddChild(MakeEmptyPanel(Loc.GetString("cmu-round-stats-empty")));
        AddLegend(mode);
    }

    private void RenderMatchups(CMURoundModeStatistics mode)
    {
        if (_dashboard == null)
            return;
        var controls = new BoxContainer { SeparationOverride = 4 };
        foreach (var againstThreat in new[] { true, false })
        {
            var button = ActionButton(againstThreat ? "platoon-threat" : "platoon-platoon", () =>
            {
                _againstThreat = againstThreat;
                Render(System.Numerics.Vector2.Zero);
            });
            button.ToggleMode = true;
            button.Pressed = againstThreat == _againstThreat;
            StyleNavigation(button, button.Pressed, true);
            controls.AddChild(button);
        }
        _body.AddChild(controls);
        var pairs = _dashboard.Matchups.Where(pair => pair.AgainstThreat == _againstThreat).ToList();
        if (pairs.Count == 0)
        {
            _body.AddChild(MakeEmptyPanel(Loc.GetString("cmu-round-stats-no-pairings")));
            return;
        }
        // Rows are the recorded govfor platoon. In Distress Signal that faction is side B.
        var useSideB = mode.Preset == CMURoundStatisticsPreset.DistressSignal;
        _body.AddChild(WrappedCell(Loc.GetString("cmu-round-stats-matrix-caption", ("side", useSideB ? mode.SideB : mode.SideA)), color: Muted));
        var platoons = pairs.Select(pair => pair.PlatoonId).Distinct().OrderBy(PlatoonName).ToList();
        var opponents = pairs.Select(pair => pair.OpponentId).Distinct()
            .OrderBy(id => _againstThreat ? ThreatName(id) : PlatoonName(id)).ToList();
        var lookup = pairs.ToDictionary(pair => (pair.PlatoonId, pair.OpponentId));
        var grid = new GridContainer { Columns = opponents.Count + 1, HSeparationOverride = 4, VSeparationOverride = 4 };
        const float platoonWidth = 240;
        const float opponentWidth = 148;
        grid.AddChild(WrappedCell(Loc.GetString("cmu-round-stats-platoon-opponent"), platoonWidth));
        foreach (var opponent in opponents)
        {
            grid.AddChild(WrappedCell(_againstThreat ? ThreatName(opponent) : PlatoonName(opponent), opponentWidth));
        }
        foreach (var platoon in platoons)
        {
            grid.AddChild(WrappedCell(PlatoonName(platoon), platoonWidth));
            foreach (var opponent in opponents)
            {
                if (!lookup.TryGetValue((platoon, opponent), out var pair))
                {
                    var empty = Cell("—", opponentWidth);
                    empty.Align = Label.AlignMode.Center;
                    empty.ToolTip = Loc.GetString("cmu-round-stats-no-pairing");
                    grid.AddChild(empty);
                    continue;
                }
                var stats = pair.Statistics;
                var wins = useSideB ? stats.SideBWins : stats.SideAWins;
                var leader = stats.DecidedTotal == 0 ? UnknownGray :
                    wins * 2 >= stats.DecidedTotal
                        ? useSideB ? GetSideBColor(mode.Preset) : GetSideAColor(mode.Preset)
                        : useSideB ? GetSideAColor(mode.Preset) : GetSideBColor(mode.Preset);
                var cell = new ContainerButton
                {
                    MinWidth = opponentWidth, MaxWidth = opponentWidth, MinHeight = 62,
                    StyleBoxOverride = DeskStyle(DeskRaised, leader, new Thickness(0, 0, 0, 3)),
                    ToolTip = PairName(pair) + "\n" + Loc.GetString("cmu-round-stats-counts",
                        ("total", stats.Total), ("decided", stats.DecidedTotal), ("draws", stats.Draws), ("unknown", stats.Unknown)),
                };
                var values = Column();
                values.Margin = new Thickness(5);
                values.SeparationOverride = 2;
                var rate = Cell(FormatRate(wins, stats.DecidedTotal));
                rate.Align = Label.AlignMode.Center;
                values.AddChild(rate);
                var count = Cell(Loc.GetString("cmu-round-stats-decided-count", ("count", stats.DecidedTotal)));
                count.Align = Label.AlignMode.Center;
                count.FontColorOverride = Muted;
                values.AddChild(count);
                cell.AddChild(values);
                cell.OnPressed += _ =>
                {
                    if (!_loading)
                        OpenDetail("matchup-file", PairName(pair), matchup: pair);
                };
                cell.OnMouseEntered += _ => cell.ModulateSelfOverride = Color.FromHex("#cce6d4");
                cell.OnMouseExited += _ => cell.ModulateSelfOverride = Color.White;
                grid.AddChild(cell);
            }
        }
        _matchupScroll = new MatrixScroll { HScrollEnabled = true, VScrollEnabled = false, HorizontalExpand = true, ReturnMeasure = true };
        _matchupScroll.AddChild(grid);
        _body.AddChild(_matchupScroll);
        var missing = mode.Total - pairs.Sum(pair => pair.Statistics.Total);
        if (missing > 0)
            _body.AddChild(Hint(Loc.GetString("cmu-round-stats-missing-pairings", ("count", missing))));
    }

    private string PairName(CMURoundMatchupStatistics pair) => Loc.GetString("cmu-round-stats-pair",
        ("platoon", PlatoonName(pair.PlatoonId)),
        ("opponent", pair.AgainstThreat ? ThreatName(pair.OpponentId) : PlatoonName(pair.OpponentId)));

    private void RenderDetail(CMURoundModeStatistics mode)
    {
        var hero = Surface(Card, DeskAccent);
        var heading = Column();
        heading.Margin = new Thickness(14);
        heading.AddChild(Hint(Loc.GetString("cmu-round-stats-" + _detailKind)));
        heading.AddChild(WrappedCell(_detailTitle!));
        heading.AddChild(Hint(PresetName(_query.Preset)));
        hero.AddChild(heading);
        _body.AddChild(hero);
        if (_selectedRound is { } record)
        {
            _body.AddChild(MakeSectionLabel(OutcomeName(record.Outcome)));
            _body.AddChild(Hint(Loc.GetString("cmu-round-stats-round-detail",
                ("planet", record.PlanetId == null ? "—" : PlanetName(record.PlanetId)),
                ("players", record.PlayerCount), ("duration", FormatDuration(record.DurationSeconds)),
                ("date", record.RecordedAt.ToString("yyyy-MM-dd HH:mm")))));
            _body.AddChild(WrappedCell(Loc.GetString("cmu-round-stats-source", ("source", FormatOutcomeSource(record.Source))), color: Muted));
            _body.AddChild(Hint(Loc.GetString("cmu-round-stats-round-pair",
                ("govfor", record.GovforPlatoonId == null ? "—" : PlatoonName(record.GovforPlatoonId)),
                ("opfor", record.OpforPlatoonId == null ? "—" : PlatoonName(record.OpforPlatoonId)),
                ("threat", record.SelectedThreatId == null ? "—" : ThreatName(record.SelectedThreatId)))));
            return;
        }
        var metrics = new BoxContainer { HorizontalExpand = true, SeparationOverride = 20, Margin = new Thickness(8, 10) };
        foreach (var (label, value) in new[]
                 {
                     (mode.SideA, mode.SideAWins + " / " + FormatRate(mode.SideAWins, mode.DecidedTotal)),
                     (mode.SideB, mode.SideBWins + " / " + FormatRate(mode.SideBWins, mode.DecidedTotal)),
                     (Loc.GetString("cmu-round-stats-endings"), mode.Total.ToString()),
                     (Loc.GetString("cmu-round-stats-avg-time"), FormatDurationOrNone(mode.Durations.AverageSeconds)),
                 })
        {
            var item = Column();
            item.SeparationOverride = 4;
            item.AddChild(Hint(label));
            item.AddChild(MakeSectionLabel(value));
            metrics.AddChild(item);
        }
        _body.AddChild(metrics);
        _body.AddChild(CompactBar(mode.Preset, mode.SideAWins, mode.SideBWins, mode.Draws, mode.Unknown));
        AddLegend(mode);
        if (_detailKind != "location-file")
        {
            _body.AddChild(MakeSectionLabel(Loc.GetString("cmu-round-stats-planets")));
            RenderPlanets(mode, false);
        }
        if (_detailKind != "population-file")
        {
            _body.AddChild(MakeSectionLabel(Loc.GetString("cmu-round-stats-population")));
            RenderPopulation(mode, false);
        }
        _body.AddChild(MakeOutcomeBreakdown(mode));
    }

    private void RenderArchive()
    {
        if (_dashboard == null)
            return;
        var searchRow = new BoxContainer { SeparationOverride = 8 };
        var search = new LineEdit { Text = _query.Search, PlaceHolder = Loc.GetString("cmu-round-stats-search-placeholder"), HorizontalExpand = true };
        CrtLobbyTheme.Apply(search, useCrtTypography: true);
        void Submit() => Query(new CMURoundStatisticsQuery(_query.Preset, search: search.Text));
        search.OnTextEntered += _ => Submit();
        searchRow.AddChild(search);
        searchRow.AddChild(ActionButton("search", Submit));
        _body.AddChild(searchRow);
        var header = DataRow();
        header.AddChild(Cell(Loc.GetString("cmu-round-stats-round-label"), 70));
        header.AddChild(Hint(Loc.GetString("cmu-round-stats-location")));
        header.AddChild(Cell(Loc.GetString("cmu-round-stats-winner"), 100));
        header.AddChild(Cell(Loc.GetString("cmu-round-stats-population"), 100, true));
        header.AddChild(Cell(Loc.GetString("cmu-round-stats-duration"), 100, true));
        header.AddChild(Cell("", 16));
        AddDataRow(header);
        foreach (var record in _dashboard.RecentRounds)
        {
            var row = DataRow();
            row.AddChild(Cell("#" + record.RoundId, 70));
            row.AddChild(WrappedCell(record.PlanetId == null ? "—" : PlanetName(record.PlanetId)));
            var winner = Cell(WinnerName(record.Winner), 100);
            winner.FontColorOverride = WinnerColor(record.Winner);
            row.AddChild(winner);
            row.AddChild(Cell(record.PlayerCount.ToString(), 100, true));
            row.AddChild(Cell(FormatDuration(record.DurationSeconds), 100, true));
            row.AddChild(Cell("›", 16));
            AddDataRow(row, () => OpenDetail("round-file",
                Loc.GetString("cmu-round-stats-round", ("round", record.RoundId),
                    ("winner", WinnerName(record.Winner)), ("outcome", OutcomeName(record.Outcome))), round: record));
        }
        var pages = Math.Max(1, (_dashboard.ArchiveTotal + CMURoundStatisticsQuery.PageSize - 1) / CMURoundStatisticsQuery.PageSize);
        var pager = new BoxContainer { SeparationOverride = 8 };
        var previous = ActionButton("previous", () => Query(new CMURoundStatisticsQuery(_query.Preset, search: _query.Search, page: _query.Page - 1)));
        previous.Disabled = _query.Page == 0;
        pager.AddChild(previous);
        pager.AddChild(Hint(Loc.GetString("cmu-round-stats-page", ("page", _query.Page + 1), ("pages", pages), ("count", _dashboard.ArchiveTotal))));
        var next = ActionButton("next", () => Query(new CMURoundStatisticsQuery(_query.Preset, search: _query.Search, page: _query.Page + 1)));
        next.Disabled = _query.Page + 1 >= pages;
        pager.AddChild(next);
        _body.AddChild(pager);
        if (_dashboard.RecentRounds.Count == 0)
            _body.AddChild(MakeEmptyPanel(Loc.GetString("cmu-round-stats-empty")));
    }
}
