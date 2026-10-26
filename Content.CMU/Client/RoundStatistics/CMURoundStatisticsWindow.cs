using System;
using System.Linq;
using System.Numerics;
using Content.Client.Lobby.UI;
using Content.Client.Stylesheets;
using Content.Shared.CMU14.RoundStatistics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Maths;
using Robust.Shared.Localization;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client.CMU14.RoundStatistics;

public sealed partial class CMURoundStatisticsWindow : DefaultWindow
{
    private const float BorderAlpha = 0.85f;

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
            StyleClasses = { StyleClass.LabelHeading },
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

        container.AddChild(MakeOutcomeBreakdown(mode));

        if (mode.ManualReasons.Count > 0)
            container.AddChild(MakeManualReasonBreakdown(mode));


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
            "Average Duration",
            FormatDurationOrNone(mode.Durations.AverageSeconds),
            $"{SideMinutes(mode.SideA, mode.Durations.SideAAverageSeconds)} / " +
            $"{SideMinutes(mode.SideB, mode.Durations.SideBAverageSeconds)}",
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
        => MakeCountBar(mode.Preset, mode.SideAWins, mode.SideBWins, mode.Draws, mode.Unknown);

    private static Control MakeCountBar(CMURoundStatisticsPreset preset, int sideA, int sideB, int draws, int unknown)
    {
        var bar = new BoxContainer { HorizontalExpand = true, SeparationOverride = 0, MinHeight = 18 };
        AddBarSegment(bar, sideA, GetSideAColor(preset));
        AddBarSegment(bar, draws, DrawGray);
        AddBarSegment(bar, unknown, UnknownGray);
        AddBarSegment(bar, sideB, GetSideBColor(preset));
        if (sideA + sideB + draws + unknown == 0)
            AddBarSegment(bar, 1, UnknownGray);
        return bar;
    }

    private static void AddBarSegment(BoxContainer bar, int count, Color color)
    {
        if (count <= 0)
            return;
        bar.AddChild(new PanelContainer
        {
            HorizontalExpand = true,
            SizeFlagsStretchRatio = count,
            MinHeight = 18,
            PanelOverride = new StyleBoxFlat { BackgroundColor = color },
        });
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
            StyleClasses = { StyleClass.LabelSubText },
            ClipText = true,
            HorizontalExpand = true,
        };
    }

    private static Control MakeBadge(string label, Color color)
    {
        var panel = MakePanel(Background, color.WithAlpha(BorderAlpha));
        panel.HorizontalExpand = false;
        panel.MinHeight = 20;
        panel.AddChild(new Label
        {
            Text = label,
            FontColorOverride = color,
            Margin = new Thickness(7, 2),
            ClipText = false,
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
            ? "—"
            : $"{wins * 100f / decided:0}%";
    }

    private static string SideShorthand(string side)
    {
        return side.Trim().ToLowerInvariant() switch
        {
            "xeno" or "threat" => "X",
            "govfor" => "G",
            "opfor" => "O",
            "indfor" => "I",
            "colonist" or "colonists" => "C",
            "clf" => "CLF",
            var other => other,
        };
    }

    private static string SideMinutes(string side, int seconds)
        => seconds <= 0
            ? "No data"
            : $"{SideShorthand(side)}-{seconds / 60f:0}min";

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
            CMURoundStatisticsPreset.ForceOnForce => Loc.GetString("cmu-round-stats-force-on-force"),
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
            CMURoundStatisticsWinner.Opfor => Loc.GetString("cmu-round-stats-opfor"),
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
            CMURoundStatisticsOutcome.DrawAlmayerAutodestruct => "Draw - Warship selfdestruct",
            CMURoundStatisticsOutcome.InsurgencyClfVictory => "CLF victory",
            CMURoundStatisticsOutcome.InsurgencyGovforVictory => "Govfor victory",
            CMURoundStatisticsOutcome.ForceOnForceGovforVictory => Loc.GetString("cmu-round-stats-fof-govfor-victory"),
            CMURoundStatisticsOutcome.ForceOnForceOpforVictory => Loc.GetString("cmu-round-stats-fof-opfor-victory"),
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
            CMURoundStatisticsWinner.Opfor => ClfGold,
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
            CMURoundStatisticsPreset.ForceOnForce => GovforBlue,
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
            CMURoundStatisticsPreset.ForceOnForce => ClfGold,
            CMURoundStatisticsPreset.ColonyFall => ThreatPurple,
            _ => Text,
        };
    }
}
