using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Client.Lobby.UI;
using Content.Client.Stylesheets;
using Content.Shared.CMU14.RoundStatistics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Localization;
using Robust.Shared.Maths;
using Timer = Robust.Shared.Timing.Timer;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client.CMU14.RoundStatistics;

public sealed class CMUPlaytimeLeaderboardWindow : DefaultWindow
{
    private const float BorderAlpha = 0.85f;

    private static readonly Color Card = Color.FromHex("#0d1f1c");
    private static readonly Color Text = Color.FromHex("#d7f4dc");
    private static readonly Color Muted = Color.FromHex("#7ea993");
    private static readonly Color You = Color.FromHex("#f2d16b");
    private static readonly Color GovforBlue = Color.FromHex("#68a7d8");
    private static readonly Color XenoRed = Color.FromHex("#d66a7b");

    private readonly BoxContainer _sections;
    private readonly Label _summary;
    private readonly Button _refresh;

    public event Action? OnRefresh;

    public CMUPlaytimeLeaderboardWindow()
    {
        MinSize = new Vector2(720, 620);
        SetSize = new Vector2(860, 720);
        Title = Loc.GetString("cmu-playtime-leaderboard-title");

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
            SeparationOverride = 8,
        };

        _summary = new Label
        {
            Text = Loc.GetString("cmu-playtime-leaderboard-loading"),
            FontColorOverride = Muted,
            ClipText = true,
            HorizontalExpand = true,
        };
        header.AddChild(_summary);

        _refresh = new Button
        {
            Text = Loc.GetString("cmu-playtime-leaderboard-refresh"),
            MinSize = new Vector2(110, 34),
            VerticalAlignment = VAlignment.Center,
        };
        _refresh.OnPressed += _ =>
        {
            OnRefresh?.Invoke();
            _refresh.Disabled = true;
            Timer.Spawn(CMUPlaytimeLeaderboardShared.RefreshCooldown, () => _refresh.Disabled = false);
        };
        header.AddChild(_refresh);
        root.AddChild(header);

        var scroll = new ScrollContainer
        {
            HScrollEnabled = false,
            VerticalExpand = true,
        };
        _sections = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 12,
            HorizontalExpand = true,
        };
        scroll.AddChild(_sections);
        root.AddChild(scroll);

        Contents.AddChild(root);
        CrtLobbyTheme.ApplyWindow(this, useCrtTypography: true);
    }

    public void UpdateLeaderboard(CMUPlaytimeLeaderboard leaderboard)
    {
        var roleCount = leaderboard.Xeno.Count + leaderboard.Govfor.Count;
        _summary.Text = Loc.GetString("cmu-playtime-leaderboard-summary",
            ("roles", roleCount),
            ("players", leaderboard.Players));

        _sections.DisposeAllChildren();
        AddSection(
            Loc.GetString("cmu-playtime-leaderboard-xeno-header"),
            Loc.GetString("cmu-playtime-leaderboard-xeno-champions"),
            XenoRed,
            leaderboard.XenoChampions,
            leaderboard.Xeno);
        AddSection(
            Loc.GetString("cmu-playtime-leaderboard-govfor-header"),
            Loc.GetString("cmu-playtime-leaderboard-govfor-champions"),
            GovforBlue,
            leaderboard.GovforChampions,
            leaderboard.Govfor);
    }

    private void AddSection(
        string title,
        string championsTitle,
        Color accent,
        List<CMUPlaytimeLeaderboardEntry> champions,
        List<CMUPlaytimeRoleLeaderboard> roles)
    {
        _sections.AddChild(new Label
        {
            Text = title,
            FontColorOverride = accent,
            StyleClasses = { StyleClass.LabelHeading },
            ClipText = true,
            HorizontalExpand = true,
        });

        if (roles.Count == 0 && champions.Count == 0)
        {
            _sections.AddChild(new Label
            {
                Text = Loc.GetString("cmu-playtime-leaderboard-empty"),
                FontColorOverride = Muted,
            });
            return;
        }

        if (champions.Count > 0)
            _sections.AddChild(MakeEntryPanel(championsTitle, champions, accent));

        foreach (var role in roles)
            _sections.AddChild(MakeRolePanel(role, accent));
    }

    private Control MakeEntryPanel(string title, List<CMUPlaytimeLeaderboardEntry> entries, Color accent)
    {
        var panel = MakePanel(Card, accent.WithAlpha(BorderAlpha));
        var box = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            Margin = new Thickness(10, 8),
            SeparationOverride = 4,
            HorizontalExpand = true,
        };

        box.AddChild(new Label
        {
            Text = title,
            FontColorOverride = Text,
            ClipText = true,
            HorizontalExpand = true,
        });

        AddEntries(box, entries, accent);

        panel.AddChild(box);
        return panel;
    }

    private Control MakeRolePanel(CMUPlaytimeRoleLeaderboard role, Color accent)
    {
        var panel = MakePanel(Card, accent.WithAlpha(BorderAlpha));
        var box = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            Margin = new Thickness(10, 8),
            SeparationOverride = 4,
            HorizontalExpand = true,
        };

        var header = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            HorizontalExpand = true,
        };
        header.AddChild(new Label
        {
            Text = role.Role,
            FontColorOverride = Text,
            ClipText = true,
            HorizontalExpand = true,
        });
        header.AddChild(new Label
        {
            Text = Loc.GetString("cmu-playtime-leaderboard-role-subtitle",
                ("hours", $"{role.TotalHours:0.0}"),
                ("players", role.PlayerCount)),
            FontColorOverride = Muted,
        });
        box.AddChild(header);

        AddEntries(box, role.Entries, accent);

        panel.AddChild(box);
        return panel;
    }

    private static void AddEntries(BoxContainer box, List<CMUPlaytimeLeaderboardEntry> entries, Color accent)
    {
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var color = entry.IsYou
                ? You
                : i == 0
                    ? accent
                    : Muted;
            var you = entry.IsYou ? $" {Loc.GetString("cmu-playtime-leaderboard-you")}" : string.Empty;
            var tag = entry.Tag.Length > 0 ? $" {entry.Tag}" : string.Empty;
            var rank = entry.Rank > 0 ? $"{entry.Rank}.".PadRight(4) : "    ";

            var row = new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal,
                HorizontalExpand = true,
            };
            row.AddChild(new Label
            {
                Text = $"{rank}{entry.Player}{you}{tag}",
                FontColorOverride = color,
                ClipText = true,
                HorizontalExpand = true,
            });
            row.AddChild(new Label
            {
                Text = $"{entry.Hours:0.0}h",
                FontColorOverride = color,
            });
            box.AddChild(row);
        }
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
}
