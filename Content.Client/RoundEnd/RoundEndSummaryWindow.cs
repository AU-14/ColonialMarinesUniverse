using System.Linq;
using System.Numerics;
using Content.Client.Lobby.UI;
using Content.Client.Message;
using Content.Client.Stylesheets;
using Content.Shared.GameTicking;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Maths;
using Robust.Shared.Utility;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client.RoundEnd;

public sealed class RoundEndSummaryWindow : DefaultWindow
{
    private const float PanelBorderAlpha = 0.65f;
    private const float AccentBorderAlpha = 0.85f;
    private static Color Background => StyleNano.CrtBackground;
    private static Color Card => StyleNano.CrtPanelBackground;
    private static Color CardQuiet => StyleNano.CrtInsetBackground;
    private static Color Border => StyleNano.CrtGreenDim.WithAlpha(PanelBorderAlpha);
    private static Color Text => StyleNano.CrtGreenSoft;
    private static Color TextMuted => StyleNano.CrtGreenDim;
    private static Color MarineBlue => StyleNano.CrtGreen;
    private static Color MedicalCyan => StyleNano.CrtGreenSoft;
    private static Color WarningGold => StyleNano.CrtGreenSoft;
    private static Color TraumaRed => StyleNano.CrtGreenSoft;
    private static Color OddityPurple => StyleNano.CrtGreen;
    private static Color SuccessGreen => StyleNano.CrtGreenSoft;

    private readonly IEntityManager _entityManager;

    public int RoundId;

    public RoundEndSummaryWindow(
        string gm,
        string roundEnd,
        TimeSpan roundTimeSpan,
        int roundId,
        RoundEndMessageEvent.RoundEndPlayerInfo[] info,
        RoundEndSummaryStats summaryStats,
        IEntityManager entityManager)
    {
        _entityManager = entityManager;

        MinSize = new Vector2(820, 700);
        SetSize = new Vector2(900, 760);
        Title = Loc.GetString("round-end-summary-window-title");

        RoundId = roundId;
        var roundEndTabs = new TabContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true
        };

        roundEndTabs.AddChild(MakeRoundEndSummaryTab(gm, roundEnd, roundTimeSpan, roundId, info, summaryStats));
        roundEndTabs.AddChild(MakePlayerManifestTab(info));

        Contents.AddChild(roundEndTabs);
        CrtLobbyTheme.ApplyWindow(this, useCrtTypography: true);

        OpenCenteredRight();
        MoveToFront();
    }

    private BoxContainer MakeRoundEndSummaryTab(
        string gamemode,
        string roundEnd,
        TimeSpan roundDuration,
        int roundId,
        RoundEndMessageEvent.RoundEndPlayerInfo[] playersInfo,
        RoundEndSummaryStats summaryStats)
    {
        var roundEndSummaryTab = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            Name = Loc.GetString("round-end-summary-window-round-end-summary-tab-title")
        };

        var roundEndSummaryContainerScrollbox = new ScrollContainer
        {
            VerticalExpand = true,
            Margin = new Thickness(12),
            HScrollEnabled = false,
        };
        var roundEndSummaryContainer = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 14
        };

        roundEndSummaryContainer.AddChild(MakeReportHeader(gamemode, roundId, roundDuration, playersInfo));

        if (!string.IsNullOrWhiteSpace(roundEnd))
            roundEndSummaryContainer.AddChild(MakeRoundEndTextPanel(roundEnd));

        roundEndSummaryContainer.AddChild(MakeMetricSection(roundId, roundDuration, playersInfo));

        if (summaryStats.InjuryStats.Length == 0 && summaryStats.OddityStats.Length == 0)
        {
            roundEndSummaryContainer.AddChild(MakeEmptyStatsPanel());
        }
        else
        {
            if (summaryStats.InjuryStats.Length > 0)
            {
                roundEndSummaryContainer.AddChild(MakeStatSection(
                    "round-end-summary-window-injury-ledger-title",
                    "round-end-summary-window-injury-ledger-subtitle",
                    summaryStats.InjuryStats));
            }

            if (summaryStats.OddityStats.Length > 0)
            {
                roundEndSummaryContainer.AddChild(MakeStatSection(
                    "round-end-summary-window-oddities-title",
                    "round-end-summary-window-oddities-subtitle",
                    summaryStats.OddityStats));
            }
        }

        roundEndSummaryContainerScrollbox.AddChild(roundEndSummaryContainer);
        roundEndSummaryTab.AddChild(roundEndSummaryContainerScrollbox);

        return roundEndSummaryTab;
    }

    private Control MakeReportHeader(
        string gamemode,
        int roundId,
        TimeSpan roundDuration,
        RoundEndMessageEvent.RoundEndPlayerInfo[] playersInfo)
    {
        var panel = MakePanel(CardQuiet, MarineBlue.WithAlpha(AccentBorderAlpha));
        var container = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            Margin = new Thickness(12),
            SeparationOverride = 4,
            HorizontalExpand = true
        };

        container.AddChild(new Label
        {
            Text = Loc.GetString("round-end-summary-window-after-action-title"),
            FontColorOverride = Text,
            StyleClasses = { StyleBase.StyleClassLabelHeading }
        });
        container.AddChild(new Label
        {
            Text = GetAfterActionDetail(gamemode, roundId, roundDuration, playersInfo.Length),
            FontColorOverride = TextMuted,
            ClipText = true,
            HorizontalExpand = true
        });

        panel.AddChild(container);
        return panel;
    }

    private Control MakeMetricSection(
        int roundId,
        TimeSpan roundDuration,
        RoundEndMessageEvent.RoundEndPlayerInfo[] playersInfo)
    {
        var section = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 8,
            HorizontalExpand = true
        };

        section.AddChild(MakeSectionHeader(
            "round-end-summary-window-telemetry-title",
            "round-end-summary-window-telemetry-subtitle"));
        section.AddChild(MakeMetricGrid(roundId, roundDuration, playersInfo));

        return section;
    }

    private Control MakeMetricGrid(
        int roundId,
        TimeSpan roundDuration,
        RoundEndMessageEvent.RoundEndPlayerInfo[] playersInfo)
    {
        var antags = playersInfo.Count(player => player.Antag);
        var observers = playersInfo.Count(player => player.Observer);
        var connected = playersInfo.Count(player => player.Connected);

        var grid = new GridContainer
        {
            Columns = 2,
            HSeparationOverride = 10,
            VSeparationOverride = 10,
            HorizontalExpand = true
        };

        grid.AddChild(MakeMetricCard(
            Loc.GetString("round-end-summary-window-metric-round"),
            Loc.GetString("round-end-summary-window-metric-round-value", ("roundId", roundId)),
            MarineBlue));
        grid.AddChild(MakeMetricCard(
            Loc.GetString("round-end-summary-window-metric-duration"),
            FormatDuration(roundDuration),
            WarningGold));
        grid.AddChild(MakeMetricCard(
            Loc.GetString("round-end-summary-window-metric-players"),
            playersInfo.Length.ToString(),
            MedicalCyan));
        grid.AddChild(MakeMetricCard(
            Loc.GetString("round-end-summary-window-metric-antags"),
            antags.ToString(),
            TraumaRed));
        grid.AddChild(MakeMetricCard(
            Loc.GetString("round-end-summary-window-metric-observers"),
            observers.ToString(),
            OddityPurple));
        grid.AddChild(MakeMetricCard(
            Loc.GetString("round-end-summary-window-metric-connected"),
            connected.ToString(),
            SuccessGreen));

        return grid;
    }

    private Control MakeMetricCard(string title, string value, Color accent)
    {
        var panel = MakePanel(CardQuiet, accent.WithAlpha(AccentBorderAlpha));
        panel.MinSize = new Vector2(260, 64);

        var container = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            Margin = new Thickness(12, 8),
            SeparationOverride = 2,
            HorizontalExpand = true
        };

        container.AddChild(new Label
        {
            Text = title,
            FontColorOverride = TextMuted,
            ClipText = true,
            HorizontalExpand = true
        });
        container.AddChild(new Label
        {
            Text = value,
            FontColorOverride = accent,
            StyleClasses = { StyleNano.StyleClassLabelBig },
            ClipText = true,
            HorizontalExpand = true
        });

        panel.AddChild(container);
        return panel;
    }

    private Control MakeStatSection(
        string title,
        string subtitle,
        RoundEndSummaryStat[] stats)
    {
        var section = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 8,
            HorizontalExpand = true
        };

        section.AddChild(MakeSectionHeader(title, subtitle));

        var list = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 8,
            HorizontalExpand = true
        };

        foreach (var stat in stats)
            list.AddChild(MakeStatCard(stat));

        section.AddChild(list);
        return section;
    }

    private Control MakeStatCard(RoundEndSummaryStat stat)
    {
        var accent = GetStatColor(stat.Color);
        var panel = MakePanel(CardQuiet, accent.WithAlpha(AccentBorderAlpha));
        panel.MinSize = new Vector2(0, 76);

        var row = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            Margin = new Thickness(12, 10),
            SeparationOverride = 12,
            HorizontalExpand = true
        };

        var value = MakePanel(Background, accent.WithAlpha(AccentBorderAlpha));
        value.HorizontalExpand = false;
        value.MinSize = new Vector2(64, 52);
        value.AddChild(new Label
        {
            Text = stat.Value.ToString(),
            FontColorOverride = accent,
            StyleClasses = { StyleNano.StyleClassLabelBig },
            ClipText = true,
            Align = Label.AlignMode.Center,
            VAlign = Label.VAlignMode.Center,
            HorizontalAlignment = HAlignment.Stretch,
            VerticalAlignment = VAlignment.Stretch
        });
        row.AddChild(value);

        var text = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 2,
            VerticalAlignment = VAlignment.Center,
            HorizontalExpand = true
        };
        text.AddChild(new Label
        {
            Text = Loc.GetString(stat.Label),
            FontColorOverride = Text,
            ClipText = true,
            HorizontalExpand = true
        });
        text.AddChild(new Label
        {
            Text = Loc.GetString(stat.Detail),
            FontColorOverride = TextMuted,
            ClipText = true,
            HorizontalExpand = true
        });

        row.AddChild(text);
        panel.AddChild(row);
        return panel;
    }

    private Control MakeEmptyStatsPanel()
    {
        var panel = MakePanel(CardQuiet, Border);
        var container = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            Margin = new Thickness(12, 10),
            SeparationOverride = 2,
            HorizontalExpand = true
        };

        container.AddChild(new Label
        {
            Text = Loc.GetString("round-end-summary-window-telemetry-empty-title"),
            FontColorOverride = Text,
            StyleClasses = { StyleBase.StyleClassLabelHeading }
        });
        container.AddChild(new Label
        {
            Text = Loc.GetString("round-end-summary-window-telemetry-empty"),
            FontColorOverride = TextMuted,
            ClipText = true,
            HorizontalExpand = true
        });

        panel.AddChild(container);
        return panel;
    }

    private Control MakeRoundEndTextPanel(string roundEnd)
    {
        var panel = MakePanel(CardQuiet, MarineBlue.WithAlpha(AccentBorderAlpha));
        var container = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            Margin = new Thickness(12),
            SeparationOverride = 8,
            HorizontalExpand = true
        };

        container.AddChild(new Label
        {
            Text = Loc.GetString("round-end-summary-window-transmission-title"),
            FontColorOverride = Text,
            StyleClasses = { StyleBase.StyleClassLabelHeading }
        });

        var roundEndLabel = new RichTextLabel
        {
            StyleClasses = { StyleNano.StyleClassCrtRichText },
            HorizontalExpand = true
        };
        roundEndLabel.SetMarkup(roundEnd.Trim());
        container.AddChild(roundEndLabel);

        panel.AddChild(container);
        return panel;
    }

    private BoxContainer MakePlayerManifestTab(RoundEndMessageEvent.RoundEndPlayerInfo[] playersInfo)
    {
        var playerManifestTab = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            Name = Loc.GetString("round-end-summary-window-player-manifest-tab-title")
        };

        var playerInfoContainerScrollbox = new ScrollContainer
        {
            VerticalExpand = true,
            Margin = new Thickness(12),
            HScrollEnabled = false
        };
        var playerInfoContainer = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 10
        };

        playerInfoContainer.AddChild(MakeManifestHeader(playersInfo));

        var sortedPlayersInfo = playersInfo
            .OrderBy(player => player.Observer)
            .ThenBy(player => !player.Antag)
            .ThenBy(player => player.PlayerICName ?? player.PlayerOOCName);

        foreach (var playerInfo in sortedPlayersInfo)
            playerInfoContainer.AddChild(MakePlayerCard(playerInfo));

        playerInfoContainerScrollbox.AddChild(playerInfoContainer);
        playerManifestTab.AddChild(playerInfoContainerScrollbox);

        return playerManifestTab;
    }

    private Control MakeManifestHeader(RoundEndMessageEvent.RoundEndPlayerInfo[] playersInfo)
    {
        var panel = MakePanel(CardQuiet, MarineBlue.WithAlpha(AccentBorderAlpha));
        panel.AddChild(new Label
        {
            Text = Loc.GetString("round-end-summary-window-manifest-title", ("players", playersInfo.Length)),
            FontColorOverride = Text,
            Margin = new Thickness(12, 10),
            StyleClasses = { StyleBase.StyleClassLabelHeading }
        });

        return panel;
    }

    private Control MakePlayerCard(RoundEndMessageEvent.RoundEndPlayerInfo playerInfo)
    {
        var accent = playerInfo.Antag
            ? TraumaRed
            : playerInfo.Observer
                ? MarineBlue
                : MedicalCyan;

        var panel = MakePanel(CardQuiet, accent.WithAlpha(AccentBorderAlpha));
        var row = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            Margin = new Thickness(10),
            SeparationOverride = 12,
            HorizontalExpand = true
        };

        row.AddChild(MakePlayerSprite(playerInfo));

        var info = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 4,
            VerticalAlignment = VAlignment.Center,
            HorizontalExpand = true
        };
        info.AddChild(new Label
        {
            Text = playerInfo.PlayerICName ?? playerInfo.PlayerOOCName,
            FontColorOverride = accent,
            ClipText = true,
            HorizontalExpand = true
        });
        info.AddChild(new Label
        {
            Text = Loc.GetString(
                "round-end-summary-window-player-ooc-line",
                ("playerOOCName", playerInfo.PlayerOOCName)),
            FontColorOverride = TextMuted,
            ClipText = true,
            HorizontalExpand = true
        });
        info.AddChild(new Label
        {
            Text = Loc.GetString(
                "round-end-summary-window-player-role-line",
                ("playerRole", GetPlayerRole(playerInfo))),
            FontColorOverride = TextMuted,
            ClipText = true,
            HorizontalExpand = true
        });

        var badges = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 4,
            HorizontalExpand = true
        };
        badges.AddChild(MakeBadge(
            playerInfo.Connected
                ? Loc.GetString("round-end-summary-window-player-connected")
                : Loc.GetString("round-end-summary-window-player-disconnected"),
            playerInfo.Connected ? SuccessGreen : TextMuted));

        if (playerInfo.Observer)
        {
            badges.AddChild(MakeBadge(
                Loc.GetString("round-end-summary-window-player-observer"),
                MarineBlue));
        }

        if (playerInfo.Antag)
        {
            badges.AddChild(MakeBadge(
                Loc.GetString("round-end-summary-window-player-antagonist"),
                TraumaRed));
        }

        info.AddChild(badges);
        row.AddChild(info);
        panel.AddChild(row);

        return panel;
    }

    private Control MakePlayerSprite(RoundEndMessageEvent.RoundEndPlayerInfo playerInfo)
    {
        if (playerInfo.PlayerNetEntity != null)
        {
            return new SpriteView(playerInfo.PlayerNetEntity.Value, _entityManager)
            {
                OverrideDirection = Direction.South,
                VerticalAlignment = VAlignment.Center,
                SetSize = new Vector2(42, 42),
                MinSize = new Vector2(42, 42),
                VerticalExpand = true,
            };
        }

        var placeholder = MakePanel(Background, Border);
        placeholder.HorizontalExpand = false;
        placeholder.MinSize = new Vector2(42, 42);
        return placeholder;
    }

    private Control MakeBadge(string label, Color color)
    {
        var badge = MakePanel(Background, color.WithAlpha(AccentBorderAlpha));
        badge.HorizontalExpand = false;
        badge.AddChild(new Label
        {
            Text = label,
            FontColorOverride = color,
            Margin = new Thickness(6, 2)
        });

        return badge;
    }

    private static Control MakeSectionHeader(string title, string? subtitle = null)
    {
        var container = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 2,
            HorizontalExpand = true
        };

        container.AddChild(new Label
        {
            Text = Loc.GetString(title),
            FontColorOverride = Text,
            StyleClasses = { StyleBase.StyleClassLabelHeading },
            ClipText = true,
            HorizontalExpand = true
        });

        if (subtitle != null)
        {
            container.AddChild(new Label
            {
                Text = Loc.GetString(subtitle),
                FontColorOverride = TextMuted,
                ClipText = true,
                HorizontalExpand = true
            });
        }

        return container;
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
                BorderThickness = new Thickness(1)
            }
        };
    }

    private static Color GetStatColor(RoundEndSummaryStatColor color)
    {
        return color switch
        {
            RoundEndSummaryStatColor.Blue => MarineBlue,
            RoundEndSummaryStatColor.Red => TraumaRed,
            RoundEndSummaryStatColor.Gold => WarningGold,
            RoundEndSummaryStatColor.Purple => OddityPurple,
            RoundEndSummaryStatColor.Cyan => MedicalCyan,
            RoundEndSummaryStatColor.Green => SuccessGreen,
            _ => Text,
        };
    }

    private static string GetPlayerRole(RoundEndMessageEvent.RoundEndPlayerInfo playerInfo)
    {
        return playerInfo.Observer
            ? Loc.GetString("round-end-summary-window-player-observer-role")
            : Loc.GetString(playerInfo.Role);
    }

    private static string FormatDuration(TimeSpan roundDuration)
    {
        return Loc.GetString(
            "round-end-summary-window-duration-value",
            ("hours", (roundDuration.Days * 24) + roundDuration.Hours),
            ("minutes", roundDuration.Minutes),
            ("seconds", roundDuration.Seconds));
    }

    private static string GetAfterActionDetail(
        string gamemode,
        int roundId,
        TimeSpan roundDuration,
        int playerCount)
    {
        var duration = FormatDuration(roundDuration);
        return string.IsNullOrWhiteSpace(gamemode)
            ? Loc.GetString(
                "round-end-summary-window-after-action-detail-no-gamemode",
                ("roundId", roundId),
                ("duration", duration),
                ("players", playerCount))
            : Loc.GetString(
                "round-end-summary-window-after-action-detail",
                ("roundId", roundId),
                ("gamemode", gamemode),
                ("duration", duration),
                ("players", playerCount));
    }
}
