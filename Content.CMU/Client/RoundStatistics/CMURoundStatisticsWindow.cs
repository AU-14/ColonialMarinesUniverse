using System;
using System.Linq;
using System.Numerics;
using Content.Client.Lobby.UI;
using Content.Client.Stylesheets;
using Content.Shared._CMU14.RoundStatistics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Maths;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client._CMU14.RoundStatistics;

public sealed class CMURoundStatisticsWindow : DefaultWindow
{
    private const float BorderAlpha = 0.85f;
    private const int BarWidth = 650;

    private static readonly Color Background = Color.FromHex("#071311");
    private static readonly Color Card = Color.FromHex("#0d1f1c");
    private static readonly Color CardQuiet = Color.FromHex("#0a1715");
    private static readonly Color Border = Color.FromHex("#4972A1").WithAlpha(BorderAlpha);
    private static readonly Color Text = Color.FromHex("#d7f4dc");
    private static readonly Color Muted = Color.FromHex("#7ea993");
    private static readonly Color GovforBlue = Color.FromHex("#68a7d8");
    private static readonly Color XenoRed = Color.FromHex("#d66a7b");
    private static readonly Color ClfGold = Color.FromHex("#d1b85d");
    private static readonly Color ColonistGreen = Color.FromHex("#77c88e");
    private static readonly Color ThreatPurple = Color.FromHex("#c98fda");
    private static readonly Color DrawGray = Color.FromHex("#b7b7b7");
    private static readonly Color UnknownGray = Color.FromHex("#666f6b");

    private readonly BoxContainer _modes;
    private readonly BoxContainer _recent;
    private readonly Label _summary;
    private readonly Button _refresh;

    public event Action? OnRefresh;

    public CMURoundStatisticsWindow()
    {
        MinSize = new Vector2(900, 680);
        SetSize = new Vector2(980, 760);
        Title = "CMU Round Outcomes";

        var root = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 12,
            Margin = new Thickness(12),
            HorizontalExpand = true,
            VerticalExpand = true,
        };

        var header = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 12,
            HorizontalExpand = true,
        };

        var headerText = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 2,
            HorizontalExpand = true,
        };

        headerText.AddChild(new Label
        {
            Text = "Operational Outcomes",
            FontColorOverride = Text,
            StyleClasses = { StyleBase.StyleClassLabelHeading },
            ClipText = true,
            HorizontalExpand = true,
        });

        _summary = new Label
        {
            Text = "Waiting for data",
            FontColorOverride = Muted,
            ClipText = true,
            HorizontalExpand = true,
        };
        headerText.AddChild(_summary);
        header.AddChild(headerText);

        _refresh = new Button
        {
            Text = "Refresh",
            MinSize = new Vector2(110, 34),
            VerticalAlignment = VAlignment.Center,
        };
        _refresh.OnPressed += _ => OnRefresh?.Invoke();
        header.AddChild(_refresh);

        root.AddChild(header);

        var tabs = new TabContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
        };

        var overviewTab = new BoxContainer
        {
            Name = "Overview",
            Orientation = LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        var overviewScroll = new ScrollContainer
        {
            HScrollEnabled = false,
            VerticalExpand = true,
        };
        _modes = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 12,
            HorizontalExpand = true,
        };
        overviewScroll.AddChild(_modes);
        overviewTab.AddChild(overviewScroll);

        var recentTab = new BoxContainer
        {
            Name = "Recent Rounds",
            Orientation = LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        var recentScroll = new ScrollContainer
        {
            HScrollEnabled = false,
            VerticalExpand = true,
        };
        _recent = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 8,
            HorizontalExpand = true,
        };
        recentScroll.AddChild(_recent);
        recentTab.AddChild(recentScroll);

        tabs.AddChild(overviewTab);
        tabs.AddChild(recentTab);
        root.AddChild(tabs);

        Contents.AddChild(root);
        CrtLobbyTheme.ApplyWindow(this, useCrtTypography: true);
    }

    public void UpdateDashboard(CMURoundStatisticsDashboard dashboard)
    {
        var totalRounds = dashboard.Modes.Sum(mode => mode.Total);
        var decidedRounds = dashboard.Modes.Sum(mode => mode.DecidedTotal);
        _summary.Text = $"{totalRounds} tracked endings, {decidedRounds} decided wins";

        _modes.DisposeAllChildren();
        foreach (var mode in dashboard.Modes)
            _modes.AddChild(MakeModePanel(mode));

        _recent.DisposeAllChildren();
        if (dashboard.RecentRounds.Count == 0)
        {
            _recent.AddChild(MakeEmptyPanel("No tracked rounds yet."));
            return;
        }

        foreach (var record in dashboard.RecentRounds)
            _recent.AddChild(MakeRecentRoundPanel(record));
    }

    private Control MakeModePanel(CMURoundModeStatistics mode)
    {
        var panel = MakePanel(Card, Border);
        var container = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            Margin = new Thickness(12),
            SeparationOverride = 10,
            HorizontalExpand = true,
        };

        container.AddChild(new Label
        {
            Text = mode.Title,
            FontColorOverride = Text,
            StyleClasses = { StyleBase.StyleClassLabelHeading },
            ClipText = true,
            HorizontalExpand = true,
        });
        container.AddChild(new Label
        {
            Text = $"{mode.Total} tracked endings / {mode.DecidedTotal} decided wins / " +
                   $"{mode.Draws} draws / {mode.Unknown} unknown",
            FontColorOverride = Muted,
            ClipText = true,
            HorizontalExpand = true,
        });

        container.AddChild(MakeRateGrid(mode));
        container.AddChild(MakeInsightGrid(mode));
        container.AddChild(MakeRecentFormPanel(mode));
        container.AddChild(MakeOutcomeBar(mode));

        if (mode.Preset == CMURoundStatisticsPreset.DistressSignal)
            container.AddChild(MakeDistressSplit(mode));

        container.AddChild(MakeOutcomeBreakdown(mode));

        if (mode.ManualReasons.Count > 0)
            container.AddChild(MakeManualReasonBreakdown(mode));

        if (mode.Threats.Count > 0)
            container.AddChild(MakeThreatBreakdown(mode));
        if (mode.Planets.Count > 0)
            container.AddChild(MakePlanetBreakdown(mode));
        if (mode.PlatoonMatchups.Count > 0)
            container.AddChild(MakePlatoonMatchupBreakdown(mode));
        if (mode.PlayerCountBands.Count > 0)
            container.AddChild(MakePlayerCountBreakdown(mode));

        panel.AddChild(container);
        return panel;
    }

    private Control MakeRateGrid(CMURoundModeStatistics mode)
    {
        var grid = new GridContainer
        {
            Columns = 4,
            HSeparationOverride = 8,
            VSeparationOverride = 8,
            HorizontalExpand = true,
        };

        grid.AddChild(MakeMetric(
            mode.SideA,
            $"{FormatRate(mode.SideAWins, mode.DecidedTotal)}",
            $"{mode.SideAWins} wins",
            GetSideAColor(mode.Preset)));
        grid.AddChild(MakeMetric(
            mode.SideB,
            $"{FormatRate(mode.SideBWins, mode.DecidedTotal)}",
            $"{mode.SideBWins} wins",
            GetSideBColor(mode.Preset)));
        grid.AddChild(MakeMetric("Draws", mode.Draws.ToString(), "excluded", DrawGray));
        grid.AddChild(MakeMetric("Unknown", mode.Unknown.ToString(), "excluded", UnknownGray));

        return grid;
    }

    private Control MakeInsightGrid(CMURoundModeStatistics mode)
    {
        var grid = new GridContainer
        {
            Columns = 4,
            HSeparationOverride = 8,
            VSeparationOverride = 8,
            HorizontalExpand = true,
        };

        grid.AddChild(MakeMetric(
            "Recent 10",
            FormatRecentForm(mode),
            $"{mode.RecentForm.Rounds} tracked",
            Border));
        grid.AddChild(MakeMetric(
            "Current Streak",
            FormatStreak(mode.CurrentStreak),
            "decided endings",
            StreakColor(mode.CurrentStreak)));
        grid.AddChild(MakeMetric(
            "Longest Streak",
            FormatStreak(mode.LongestStreak),
            "decided endings",
            StreakColor(mode.LongestStreak)));
        grid.AddChild(MakeMetric(
            "Avg Duration",
            FormatDurationOrNone(mode.Durations.AverageSeconds),
            $"{mode.SideA} {FormatDurationOrNone(mode.Durations.SideAAverageSeconds)} / " +
            $"{mode.SideB} {FormatDurationOrNone(mode.Durations.SideBAverageSeconds)}",
            Border));

        return grid;
    }

    private Control MakeRecentFormPanel(CMURoundModeStatistics mode)
    {
        var panel = MakePanel(CardQuiet, Border);
        var box = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            Margin = new Thickness(8, 6),
            SeparationOverride = 6,
            HorizontalExpand = true,
        };

        var header = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            HorizontalExpand = true,
        };
        header.AddChild(new Label
        {
            Text = "Recent Form",
            FontColorOverride = Text,
            ClipText = true,
            HorizontalExpand = true,
        });
        header.AddChild(new Label
        {
            Text = $"{mode.SideA} {mode.RecentForm.SideAWins} / {mode.SideB} {mode.RecentForm.SideBWins}",
            FontColorOverride = Muted,
            ClipText = true,
            MinWidth = 180,
        });
        box.AddChild(header);

        var pips = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 4,
            HorizontalExpand = true,
        };

        if (mode.RecentForm.Winners.Count == 0)
        {
            pips.AddChild(new Label
            {
                Text = "No recent rounds",
                FontColorOverride = Muted,
                ClipText = true,
                HorizontalExpand = true,
            });
        }
        else
        {
            foreach (var winner in mode.RecentForm.Winners)
                pips.AddChild(MakeFormPip(WinnerColor(winner)));
        }

        box.AddChild(pips);
        panel.AddChild(box);
        return panel;
    }

    private static Control MakeFormPip(Color color)
    {
        return new PanelContainer
        {
            MinSize = new Vector2(18, 18),
            HorizontalExpand = false,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = color.WithAlpha(0.85f),
                BorderColor = color,
                BorderThickness = new Thickness(1),
            },
        };
    }

    private Control MakeMetric(string label, string value, string detail, Color color)
    {
        var panel = MakePanel(CardQuiet, color.WithAlpha(BorderAlpha));
        panel.MinSize = new Vector2(190, 76);

        var box = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            Margin = new Thickness(10, 8),
            SeparationOverride = 2,
            HorizontalExpand = true,
        };
        box.AddChild(new Label
        {
            Text = label,
            FontColorOverride = Muted,
            ClipText = true,
            HorizontalExpand = true,
        });
        box.AddChild(new Label
        {
            Text = value,
            FontColorOverride = color,
            StyleClasses = { StyleNano.StyleClassLabelBig },
            ClipText = true,
            HorizontalExpand = true,
        });
        box.AddChild(new Label
        {
            Text = detail,
            FontColorOverride = Muted,
            ClipText = true,
            HorizontalExpand = true,
        });

        panel.AddChild(box);
        return panel;
    }

    private Control MakeOutcomeBar(CMURoundModeStatistics mode)
    {
        var panel = MakePanel(CardQuiet, UnknownGray.WithAlpha(BorderAlpha));
        panel.HorizontalExpand = false;
        panel.MinSize = new Vector2(BarWidth, 18);

        var bar = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            HorizontalExpand = false,
            MinSize = new Vector2(BarWidth, 18),
        };

        if (mode.Total <= 0)
        {
            bar.AddChild(MakeBarSegment(BarWidth, UnknownGray));
        }
        else
        {
            AddBarSegment(bar, mode.SideAWins, mode.Total, GetSideAColor(mode.Preset));
            AddBarSegment(bar, mode.Draws, mode.Total, DrawGray);
            AddBarSegment(bar, mode.Unknown, mode.Total, UnknownGray);
            AddBarSegment(bar, mode.SideBWins, mode.Total, GetSideBColor(mode.Preset));
        }

        panel.AddChild(bar);
        return panel;
    }

    private static void AddBarSegment(BoxContainer bar, int count, int total, Color color)
    {
        if (count <= 0)
            return;

        var width = Math.Max(5, (int) MathF.Round(BarWidth * count / (float) total));
        bar.AddChild(MakeBarSegment(width, color));
    }

    private static Control MakeBarSegment(int width, Color color)
    {
        return new PanelContainer
        {
            MinSize = new Vector2(width, 18),
            HorizontalExpand = false,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = color,
                BorderColor = color,
                BorderThickness = new Thickness(0),
            },
        };
    }

    private Control MakeOutcomeBreakdown(CMURoundModeStatistics mode)
    {
        var box = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 6,
            HorizontalExpand = true,
        };

        box.AddChild(MakeSectionLabel("Outcome Breakdown"));

        if (mode.Outcomes.Count == 0)
        {
            box.AddChild(MakeEmptyPanel("No outcomes recorded for this mode."));
            return box;
        }

        foreach (var outcome in mode.Outcomes)
            box.AddChild(MakeBreakdownRow(
                OutcomeName(outcome.Outcome),
                $"{WinnerName(outcome.Winner)} / {FormatRate(outcome.Count, mode.Total)} of endings",
                outcome.Count,
                WinnerColor(outcome.Winner)));

        return box;
    }

    private Control MakeManualReasonBreakdown(CMURoundModeStatistics mode)
    {
        var box = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 6,
            HorizontalExpand = true,
        };

        box.AddChild(MakeSectionLabel("Manual Ending Reasons"));

        var manualTotal = mode.ManualReasons.Sum(reason => reason.Count);
        foreach (var reason in mode.ManualReasons)
        {
            box.AddChild(MakeBreakdownRow(
                FormatOutcomeSource(reason.Reason),
                $"{FormatRate(reason.Count, manualTotal)} of manual endings",
                reason.Count,
                UnknownGray));
        }

        return box;
    }

    private Control MakeDistressSplit(CMURoundModeStatistics mode)
    {
        var box = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 6,
            HorizontalExpand = true,
        };

        box.AddChild(MakeSectionLabel("Distress Signal Major / Minor Split"));
        box.AddChild(MakeOutcomeSplitRow(
            mode,
            CMURoundStatisticsOutcome.XenoMajorHijackWin,
            "Xeno major",
            "Hijack win",
            XenoRed));
        box.AddChild(MakeOutcomeSplitRow(
            mode,
            CMURoundStatisticsOutcome.XenoMinorHijackLoss,
            "Xeno minor",
            "Hijack loss",
            XenoRed));
        box.AddChild(MakeOutcomeSplitRow(
            mode,
            CMURoundStatisticsOutcome.MarineMinorHiveCollapse,
            "Marine minor",
            "Hive collapse",
            GovforBlue));
        box.AddChild(MakeOutcomeSplitRow(
            mode,
            CMURoundStatisticsOutcome.MarineMajorXenoWipe,
            "Marine major",
            "Pre-hijack xeno wipe",
            GovforBlue));
        box.AddChild(MakeOutcomeSplitRow(
            mode,
            CMURoundStatisticsOutcome.DrawAlmayerAutodestruct,
            "Draw",
            "Almayer autodestruct",
            DrawGray));

        return box;
    }

    private Control MakeOutcomeSplitRow(
        CMURoundModeStatistics mode,
        CMURoundStatisticsOutcome outcome,
        string label,
        string detail,
        Color color)
    {
        var count = mode.Outcomes
            .Where(entry => entry.Outcome == outcome)
            .Sum(entry => entry.Count);

        return MakeBreakdownRow(label, $"{detail} / {FormatRate(count, mode.Total)}", count, color);
    }

    private Control MakeThreatBreakdown(CMURoundModeStatistics mode)
    {
        var box = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 6,
            HorizontalExpand = true,
        };

        box.AddChild(MakeSectionLabel("Threat Breakdown"));

        foreach (var threat in mode.Threats)
        {
            var decided = threat.SideAWins + threat.SideBWins;
            var text = $"{mode.SideA} {FormatRate(threat.SideAWins, decided)} ({threat.SideAWins}) / " +
                       $"{mode.SideB} {FormatRate(threat.SideBWins, decided)} ({threat.SideBWins})";
            if (threat.Draws > 0)
                text += $" / draws {threat.Draws}";
            if (threat.Unknown > 0)
                text += $" / unknown {threat.Unknown}";

            box.AddChild(MakeBreakdownRow(threat.ThreatId, text, threat.Total, Border));
        }

        return box;
    }

    private Control MakePlanetBreakdown(CMURoundModeStatistics mode)
    {
        var box = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 6,
            HorizontalExpand = true,
        };

        box.AddChild(MakeSectionLabel("Planet Breakdown"));

        foreach (var planet in mode.Planets)
        {
            var decided = planet.SideAWins + planet.SideBWins;
            var text = $"{mode.SideA} {FormatRate(planet.SideAWins, decided)} ({planet.SideAWins}) / " +
                       $"{mode.SideB} {FormatRate(planet.SideBWins, decided)} ({planet.SideBWins}) / " +
                       $"avg {FormatDurationOrNone(planet.AverageDurationSeconds)}";
            if (planet.Draws > 0)
                text += $" / draws {planet.Draws}";
            if (planet.Unknown > 0)
                text += $" / unknown {planet.Unknown}";

            box.AddChild(MakeBreakdownRow(planet.PlanetId, text, planet.Total, Border));
        }

        return box;
    }

    private Control MakePlatoonMatchupBreakdown(CMURoundModeStatistics mode)
    {
        var box = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 6,
            HorizontalExpand = true,
        };

        box.AddChild(MakeSectionLabel("Platoon Matchups"));

        foreach (var matchup in mode.PlatoonMatchups)
        {
            var decided = matchup.SideAWins + matchup.SideBWins;
            var text = $"{mode.SideA} {FormatRate(matchup.SideAWins, decided)} ({matchup.SideAWins}) / " +
                       $"{mode.SideB} {FormatRate(matchup.SideBWins, decided)} ({matchup.SideBWins})";
            if (matchup.Draws > 0)
                text += $" / draws {matchup.Draws}";
            if (matchup.Unknown > 0)
                text += $" / unknown {matchup.Unknown}";

            box.AddChild(MakeBreakdownRow(
                $"{matchup.GovforPlatoonId} vs {matchup.OpforPlatoonId}",
                text,
                matchup.Total,
                Border));
        }

        return box;
    }

    private Control MakePlayerCountBreakdown(CMURoundModeStatistics mode)
    {
        var box = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 6,
            HorizontalExpand = true,
        };

        box.AddChild(MakeSectionLabel("Player Count Bands"));

        foreach (var band in mode.PlayerCountBands)
        {
            var decided = band.SideAWins + band.SideBWins;
            var text = $"{mode.SideA} {FormatRate(band.SideAWins, decided)} ({band.SideAWins}) / " +
                       $"{mode.SideB} {FormatRate(band.SideBWins, decided)} ({band.SideBWins})";
            if (band.Draws > 0)
                text += $" / draws {band.Draws}";
            if (band.Unknown > 0)
                text += $" / unknown {band.Unknown}";

            box.AddChild(MakeBreakdownRow(band.Band, text, band.Total, Border));
        }

        return box;
    }

    private Control MakeBreakdownRow(string left, string right, int count, Color color)
    {
        var panel = MakePanel(CardQuiet, color.WithAlpha(BorderAlpha));
        var row = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            Margin = new Thickness(8, 6),
            SeparationOverride = 10,
            HorizontalExpand = true,
        };

        row.AddChild(MakeBadge(count.ToString(), color));
        row.AddChild(new Label
        {
            Text = left,
            FontColorOverride = Text,
            ClipText = true,
            HorizontalExpand = true,
        });
        row.AddChild(new Label
        {
            Text = right,
            FontColorOverride = Muted,
            ClipText = true,
            MinWidth = 240,
        });

        panel.AddChild(row);
        return panel;
    }

    private Control MakeRecentRoundPanel(CMURoundOutcomeRecord record)
    {
        var color = WinnerColor(record.Winner);
        var panel = MakePanel(CardQuiet, color.WithAlpha(BorderAlpha));
        var box = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            Margin = new Thickness(10, 8),
            SeparationOverride = 4,
            HorizontalExpand = true,
        };

        box.AddChild(new Label
        {
            Text = $"Round {record.RoundId} - {PresetName(record.Preset)} - {WinnerName(record.Winner)}",
            FontColorOverride = color,
            ClipText = true,
            HorizontalExpand = true,
        });
        box.AddChild(new Label
        {
            Text = OutcomeName(record.Outcome),
            FontColorOverride = Text,
            ClipText = true,
            HorizontalExpand = true,
        });
        box.AddChild(new Label
        {
            Text = $"{(record.Outcome == CMURoundStatisticsOutcome.Unknown ? "Manual reason" : "Recorded source")}: {FormatOutcomeSource(record.Source)}",
            FontColorOverride = Muted,
            ClipText = true,
            HorizontalExpand = true,
        });

        var threat = string.IsNullOrWhiteSpace(record.SelectedThreatId)
            ? "no threat"
            : $"threat {record.SelectedThreatId}";
        var planet = string.IsNullOrWhiteSpace(record.PlanetId)
            ? "no planet"
            : $"planet {record.PlanetId}";

        box.AddChild(new Label
        {
            Text = $"{record.PlayerCount} players / {FormatDuration(record.DurationSeconds)} / {threat} / {planet} / {record.RecordedAt:yyyy-MM-dd HH:mm} UTC",
            FontColorOverride = Muted,
            ClipText = true,
            HorizontalExpand = true,
        });

        panel.AddChild(box);
        return panel;
    }

    private Control MakeEmptyPanel(string text)
    {
        var panel = MakePanel(CardQuiet, UnknownGray.WithAlpha(BorderAlpha));
        panel.AddChild(new Label
        {
            Text = text,
            FontColorOverride = Muted,
            Margin = new Thickness(10, 8),
            ClipText = true,
            HorizontalExpand = true,
        });
        return panel;
    }

    private static Label MakeSectionLabel(string text)
    {
        return new Label
        {
            Text = text,
            FontColorOverride = Text,
            StyleClasses = { StyleBase.StyleClassLabelSubText },
            ClipText = true,
            HorizontalExpand = true,
        };
    }

    private static Control MakeBadge(string label, Color color)
    {
        var panel = MakePanel(Background, color.WithAlpha(BorderAlpha));
        panel.HorizontalExpand = false;
        panel.MinSize = new Vector2(Math.Max(28, label.Length * 8 + 14), 20);
        panel.AddChild(new Label
        {
            Text = label,
            FontColorOverride = color,
            Margin = new Thickness(7, 2),
            ClipText = true,
        });
        return panel;
    }

    private static PanelContainer MakePanel(Color background, Color border)
    {
        return new PanelContainer
        {
            HorizontalExpand = true,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = background,
                BorderColor = border,
                BorderThickness = new Thickness(1),
            },
        };
    }

    private static string FormatRate(int wins, int decided)
    {
        return decided <= 0
            ? "0.0%"
            : $"{wins * 100f / decided:0.0}%";
    }

    private static string FormatRecentForm(CMURoundModeStatistics mode)
    {
        if (mode.RecentForm.Rounds == 0)
            return "No data";

        return $"{mode.RecentForm.SideAWins}-{mode.RecentForm.SideBWins}";
    }

    private static string FormatStreak(CMURoundStreak streak)
    {
        return streak.Count <= 0
            ? "None"
            : $"{WinnerName(streak.Winner)} x{streak.Count}";
    }

    private static Color StreakColor(CMURoundStreak streak)
    {
        return streak.Count <= 0
            ? UnknownGray
            : WinnerColor(streak.Winner);
    }

    private static string FormatDuration(int seconds)
    {
        var duration = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return $"{(int) duration.TotalHours:00}:{duration.Minutes:00}:{duration.Seconds:00}";
    }

    private static string FormatDurationOrNone(int seconds)
    {
        return seconds <= 0
            ? "No data"
            : FormatDuration(seconds);
    }

    private static string FormatOutcomeSource(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return "Unknown source";

        source = source.Trim();
        if (source.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
            return "Unknown source";

        if (source.Equals("NoPendingOutcome", StringComparison.OrdinalIgnoreCase) ||
            source.Equals("RoundEndMessageEvent", StringComparison.OrdinalIgnoreCase))
        {
            return "No stats outcome was recorded before round end";
        }

        if (source.Equals("WithdrawConsoleStalemate", StringComparison.OrdinalIgnoreCase))
            return "Withdraw console stalemate";

        const string withdrawPrefix = "WithdrawConsole:";
        if (source.StartsWith(withdrawPrefix, StringComparison.OrdinalIgnoreCase))
            return $"Withdraw console: {FormatFaction(source[withdrawPrefix.Length..])}";

        const string objectivePrefix = "AuObjective:";
        if (source.StartsWith(objectivePrefix, StringComparison.OrdinalIgnoreCase))
            return $"AU objective: {FormatFaction(source[objectivePrefix.Length..])}";

        return source;
    }

    private static string FormatFaction(string faction)
    {
        if (string.IsNullOrWhiteSpace(faction))
            return "unknown faction";

        return faction.Trim().ToLowerInvariant() switch
        {
            "govfor" => "Govfor",
            "opfor" => "Opfor",
            "clf" => "CLF",
            "colony" or "colonist" => "Colonists",
            "threat" => "Threat",
            "xeno" => "Xeno",
            "unknown" => "unknown faction",
            var other => other,
        };
    }

    private static string PresetName(CMURoundStatisticsPreset preset)
    {
        return preset switch
        {
            CMURoundStatisticsPreset.DistressSignal => "Distress Signal",
            CMURoundStatisticsPreset.Insurgency => "Insurgency",
            CMURoundStatisticsPreset.ColonyFall => "Colony Fall",
            _ => preset.ToString(),
        };
    }

    private static string WinnerName(CMURoundStatisticsWinner winner)
    {
        return winner switch
        {
            CMURoundStatisticsWinner.Xeno => "Xeno",
            CMURoundStatisticsWinner.Govfor => "Govfor",
            CMURoundStatisticsWinner.Clf => "CLF",
            CMURoundStatisticsWinner.Colonists => "Colonists",
            CMURoundStatisticsWinner.Threat => "Threat",
            CMURoundStatisticsWinner.Draw => "Draw",
            CMURoundStatisticsWinner.Unknown => "Unknown",
            _ => winner.ToString(),
        };
    }

    private static string OutcomeName(CMURoundStatisticsOutcome outcome)
    {
        return outcome switch
        {
            CMURoundStatisticsOutcome.XenoMajorHijackWin => "Xeno major - hijack win",
            CMURoundStatisticsOutcome.XenoMinorHijackLoss => "Xeno minor - hijack loss / xenowipe",
            CMURoundStatisticsOutcome.MarineMinorHiveCollapse => "Marine minor - hive collapse",
            CMURoundStatisticsOutcome.MarineMajorXenoWipe => "Marine major - pre-hijack xeno wipe",
            CMURoundStatisticsOutcome.DrawAlmayerAutodestruct => "Draw - Almayer autodestruct",
            CMURoundStatisticsOutcome.InsurgencyClfVictory => "CLF victory",
            CMURoundStatisticsOutcome.InsurgencyGovforVictory => "Govfor victory",
            CMURoundStatisticsOutcome.ColonyFallThreatVictory => "Threat victory",
            CMURoundStatisticsOutcome.ColonyFallSurvivorVictory => "Colonist victory",
            CMURoundStatisticsOutcome.Stalemate => "Stalemate",
            CMURoundStatisticsOutcome.ObjectiveVictory => "Objective victory",
            CMURoundStatisticsOutcome.Unknown => "Unknown / manual ending",
            _ => outcome.ToString(),
        };
    }

    private static Color WinnerColor(CMURoundStatisticsWinner winner)
    {
        return winner switch
        {
            CMURoundStatisticsWinner.Xeno => XenoRed,
            CMURoundStatisticsWinner.Govfor => GovforBlue,
            CMURoundStatisticsWinner.Clf => ClfGold,
            CMURoundStatisticsWinner.Colonists => ColonistGreen,
            CMURoundStatisticsWinner.Threat => ThreatPurple,
            CMURoundStatisticsWinner.Draw => DrawGray,
            CMURoundStatisticsWinner.Unknown => UnknownGray,
            _ => Text,
        };
    }

    private static Color GetSideAColor(CMURoundStatisticsPreset preset)
    {
        return preset switch
        {
            CMURoundStatisticsPreset.DistressSignal => XenoRed,
            CMURoundStatisticsPreset.Insurgency => GovforBlue,
            CMURoundStatisticsPreset.ColonyFall => ColonistGreen,
            _ => Text,
        };
    }

    private static Color GetSideBColor(CMURoundStatisticsPreset preset)
    {
        return preset switch
        {
            CMURoundStatisticsPreset.DistressSignal => GovforBlue,
            CMURoundStatisticsPreset.Insurgency => ClfGold,
            CMURoundStatisticsPreset.ColonyFall => ThreatPurple,
            _ => Text,
        };
    }
}
