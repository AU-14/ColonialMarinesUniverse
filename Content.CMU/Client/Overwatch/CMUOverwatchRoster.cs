using System.Linq;
using Content.Client.CMU14.Interface;
using Content.Client.Lobby.UI;
using Content.Client.Stylesheets;
using Content.Shared._RMC14.Overwatch;
using Content.Shared._RMC14.Tracker.SquadLeader;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared.Mobs;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client.CMU14.Overwatch;

/// <summary>
/// One marine or tripod camera on the roster, with the names the upstream console already worked out.
/// </summary>
public readonly record struct CMURosterEntry(OverwatchMarine Marine, string DisplayName, string RoleName, bool IsTripod);

/// <summary>
/// The overwatch squad roster, grouped the way the squad talks on the radio: squad lead, each
/// fireteam (with its nickname), the unassigned, then tripod cameras. Each row carries the
/// marine's callsign, a health bar and bleeding/SSD tags.
/// </summary>
/// <remarks>
/// Rows are kept and moved between groups rather than rebuilt, because the console refreshes
/// twice a second and a rebuilt button loses a click that lands between press and release.
/// </remarks>
public sealed class CMUOverwatchRoster : BoxContainer
{
    // Columns share the width by these ratios and clip their text, so the roster always fits the
    // window instead of running off its right edge. Only the action buttons are fixed.
    private const float CallsignRatio = 1.0f;
    private const float NameRatio = 1.6f;
    private const float RoleRatio = 1.3f;
    private const float StatusRatio = 1.8f;
    private const float LocationRatio = 1.4f;
    private const float DistanceRatio = 0.5f;
    private const float MinCellWidth = 30;
    private const float ButtonWidth = 42;
    private const int CellSeparation = 6;

    // Matches the row panels' content padding (CrtInsetPanel) so headers line up with the cells.
    private static readonly Thickness RowInset = new(9, 0);

    public event Action<NetEntity>? OnFeed;
    public event Action<NetEntity, bool>? OnHide;
    public event Action<NetEntity>? OnPromote;

    private readonly BoxContainer _groupsBox;
    private readonly Dictionary<string, Group> _groups = new();
    private readonly Dictionary<NetEntity, Row> _rows = new();

    public CMUOverwatchRoster()
    {
        Orientation = LayoutOrientation.Vertical;
        VerticalExpand = true;
        HorizontalExpand = true;
        SeparationOverride = 2;

        AddChild(CreateColumnHeader());

        var scroll = new ScrollContainer
        {
            VerticalExpand = true,
            HorizontalExpand = true,
            HScrollEnabled = false,
        };

        _groupsBox = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            HorizontalExpand = true,
            SeparationOverride = 6,
        };

        scroll.AddChild(_groupsBox);
        AddChild(scroll);
    }

    public void Update(
        IReadOnlyList<CMURosterEntry> entries,
        NetEntity? leader,
        FireteamData? fireteams,
        OverwatchConsoleComponent console,
        Func<NetEntity, bool> isHidden,
        string search,
        IReadOnlyCollection<NetEntity> feeds)
    {
        var seen = new HashSet<NetEntity>();
        var byGroup = new Dictionary<string, List<CMURosterEntry>>();
        foreach (var entry in entries)
        {
            seen.Add(entry.Marine.Id);
            byGroup.GetOrNew(GroupKey(entry, leader)).Add(entry);
        }

        foreach (var (id, row) in _rows.ToArray())
        {
            if (seen.Contains(id))
                continue;

            row.Orphan();
            _rows.Remove(id);
        }

        var groupOrder = 0;
        foreach (var key in GroupOrder())
        {
            var group = GetGroup(key);
            group.SetPositionInParent(groupOrder++);

            if (!byGroup.TryGetValue(key, out var groupEntries))
            {
                group.Visible = false;
                continue;
            }

            // Fireteam leaders head their team; everyone else keeps the console's role ordering.
            var ordered = groupEntries
                .Select((entry, index) => (entry, index))
                .OrderBy(e => e.entry.Marine.FireteamLeader ? 0 : 1)
                .ThenBy(e => e.index)
                .Select(e => e.entry)
                .ToList();

            var visible = 0;
            for (var i = 0; i < ordered.Count; i++)
            {
                var entry = ordered[i];
                var row = GetRow(entry.Marine.Id);
                if (row.Parent != group.Rows)
                {
                    row.Orphan();
                    group.Rows.AddChild(row);
                }

                row.SetPositionInParent(i);
                row.Update(entry, entry.Marine.Id == leader, isHidden(entry.Marine.Id), feeds.Contains(entry.Marine.Id));
                row.Visible = Matches(entry, console, isHidden(entry.Marine.Id), search);
                if (row.Visible)
                    visible++;
            }

            group.SetTitle(GroupTitle(key, fireteams), visible);
            group.Visible = visible > 0;
        }
    }

    private static IEnumerable<string> GroupOrder()
    {
        yield return "lead";
        yield return "ft0";
        yield return "ft1";
        yield return "ft2";
        yield return "unassigned";
        yield return "cameras";
    }

    private static string GroupKey(CMURosterEntry entry, NetEntity? leader)
    {
        if (entry.IsTripod)
            return "cameras";

        if (entry.Marine.Id == leader)
            return "lead";

        return entry.Marine.Fireteam is { } fireteam and >= 0 and <= 2
            ? $"ft{fireteam}"
            : "unassigned";
    }

    private static string GroupTitle(string key, FireteamData? fireteams)
    {
        switch (key)
        {
            case "lead":
                return Loc.GetString("cmu-overwatch-roster-group-lead");
            case "unassigned":
                return Loc.GetString("cmu-overwatch-roster-group-unassigned");
            case "cameras":
                return Loc.GetString("cmu-overwatch-roster-group-cameras");
        }

        var index = key[^1] - '0';
        var nickname = fireteams?.Fireteams.ElementAtOrDefault(index)?.Nickname;
        return string.IsNullOrWhiteSpace(nickname)
            ? Loc.GetString("cmu-overwatch-roster-group-fireteam", ("number", index + 1))
            : Loc.GetString("cmu-overwatch-roster-group-fireteam-named", ("number", index + 1), ("name", nickname));
    }

    private static bool Matches(CMURosterEntry entry, OverwatchConsoleComponent console, bool hidden, string search)
    {
        var marine = entry.Marine;
        if (console.Location is { } location && marine.Location != location)
            return false;

        if (!console.ShowDead && marine.State != MobState.Alive && !entry.IsTripod)
            return false;

        if (!console.ShowHidden && hidden)
            return false;

        if (string.IsNullOrWhiteSpace(search))
            return true;

        return Contains(entry.DisplayName, search) ||
               Contains(entry.RoleName, search) ||
               Contains(marine.AreaName, search) ||
               Contains(marine.Callsign, search);
    }

    private static bool Contains(string text, string search)
    {
        return text.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private Group GetGroup(string key)
    {
        if (_groups.TryGetValue(key, out var group))
            return group;

        group = new Group();
        _groups[key] = group;
        _groupsBox.AddChild(group);
        CrtLobbyTheme.Apply(group, useCrtTypography: true);
        return group;
    }

    private Row GetRow(NetEntity id)
    {
        if (_rows.TryGetValue(id, out var row))
            return row;

        row = new Row(id);
        row.Feed.OnPressed += _ => OnFeed?.Invoke(id);
        row.Hide.OnPressed += _ => OnHide?.Invoke(id, !row.Hidden);
        row.Promote.OnPressed += _ => OnPromote?.Invoke(id);
        CrtLobbyTheme.Apply(row, useCrtTypography: true);

        _rows[id] = row;
        return row;
    }

    private static Control CreateColumnHeader()
    {
        var header = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = CellSeparation,
            Margin = RowInset,
        };

        header.AddChild(HeaderCell("cmu-overwatch-roster-col-callsign", CallsignRatio));
        header.AddChild(HeaderCell("cmu-overwatch-roster-col-name", NameRatio));
        header.AddChild(HeaderCell("cmu-overwatch-roster-col-role", RoleRatio));
        header.AddChild(HeaderCell("cmu-overwatch-roster-col-status", StatusRatio));
        header.AddChild(HeaderCell("cmu-overwatch-roster-col-location", LocationRatio));
        header.AddChild(HeaderCell("cmu-overwatch-roster-col-distance", DistanceRatio));
        header.AddChild(new Control { MinWidth = ButtonWidth * 3 + CellSeparation * 2 });
        return header;
    }

    private static Label HeaderCell(string loc, float ratio)
    {
        var label = Cell(ratio);
        label.Text = Loc.GetString(loc);
        label.StyleClasses.Add(StyleNano.StyleClassCrtDimText);
        return label;
    }

    /// <summary>
    /// A column cell: takes its share of the row and clips rather than widening the row.
    /// </summary>
    private static Label Cell(float ratio)
    {
        return new Label
        {
            HorizontalExpand = true,
            SizeFlagsStretchRatio = ratio,
            MinWidth = MinCellWidth,
            ClipText = true,
            VerticalAlignment = VAlignment.Center,
        };
    }

    private sealed class Group : BoxContainer
    {
        private readonly Label _title;
        public readonly BoxContainer Rows;

        public Group()
        {
            Orientation = LayoutOrientation.Vertical;
            HorizontalExpand = true;
            SeparationOverride = 2;

            _title = new Label
            {
                StyleClasses = { StyleNano.StyleClassCrtSectionTitle },
                VerticalAlignment = VAlignment.Center,
            };

            var rule = new PanelContainer
            {
                StyleClasses = { StyleNano.StyleClassCrtSectionRule },
                HorizontalExpand = true,
                VerticalAlignment = VAlignment.Center,
                MinHeight = 1,
            };

            AddChild(new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal,
                SeparationOverride = 8,
                Children = { _title, rule },
            });

            Rows = new BoxContainer
            {
                Orientation = LayoutOrientation.Vertical,
                HorizontalExpand = true,
                SeparationOverride = 2,
            };
            AddChild(Rows);
        }

        public void SetTitle(string title, int count)
        {
            _title.Text = $"{title} ({count})";
        }
    }

    private sealed class Row : PanelContainer
    {
        public readonly Label Callsign;
        public readonly Label Name;
        public readonly Label Role;
        public readonly CMUOverwatchHealthBar Health;
        public readonly Label State;
        public readonly Label Bleeding;
        public readonly Label Ssd;
        public readonly Label Location;
        public readonly Label Distance;
        public readonly Button Feed;
        public readonly Button Hide;
        public readonly Button Promote;
        public bool Hidden;

        public Row(NetEntity id)
        {
            StyleClasses.Add(StyleNano.StyleClassCrtInsetPanel);
            HorizontalExpand = true;

            Callsign = Cell(CallsignRatio);
            Name = Cell(NameRatio);
            Role = Cell(RoleRatio);
            Health = new CMUOverwatchHealthBar();
            State = new Label { ClipText = true, VerticalAlignment = VAlignment.Center };
            Bleeding = new Label { ClipText = true, VerticalAlignment = VAlignment.Center };
            Ssd = new Label { ClipText = true, VerticalAlignment = VAlignment.Center, Text = Loc.GetString("rmc-overwatch-console-ssd") };
            Location = Cell(LocationRatio);
            Distance = Cell(DistanceRatio);
            Feed = new Button { MinWidth = ButtonWidth, Text = Loc.GetString("cmu-overwatch-roster-feed"), ToolTip = Loc.GetString("cmu-overwatch-roster-feed-tooltip") };
            Hide = new Button { MinWidth = ButtonWidth };
            Promote = new Button { MinWidth = ButtonWidth, Text = Loc.GetString("cmu-overwatch-roster-promote"), ToolTip = Loc.GetString("rmc-overwatch-console-promote-squad-leader") };

            var status = new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal,
                HorizontalExpand = true,
                SizeFlagsStretchRatio = StatusRatio,
                MinWidth = MinCellWidth,
                RectClipContent = true,
                SeparationOverride = 5,
                Children = { Health, State, Bleeding, Ssd },
            };

            AddChild(new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal,
                SeparationOverride = CellSeparation,
                Children = { Callsign, Name, Role, status, Location, Distance, Feed, Hide, Promote },
            });
        }

        public void Update(CMURosterEntry entry, bool isLeader, bool hidden, bool feedOpen)
        {
            var marine = entry.Marine;

            Callsign.Text = marine.Callsign;
            Callsign.FontColorOverride = CrtTerminalPalette.TextDim;

            var hasCamera = marine.Camera != default;
            var name = entry.DisplayName;
            if (marine.FireteamLeader)
                name = Loc.GetString("cmu-overwatch-roster-ftl", ("name", name));

            Name.Text = hasCamera ? name : Loc.GetString("cmu-overwatch-roster-no-camera", ("name", name));
            Name.ToolTip = Name.Text;

            Role.Text = entry.RoleName;
            Location.Text = marine.AreaName;

            Distance.Text = marine.LeaderDistance is { } distance && distance.LengthSquared() > 0
                ? $"{distance.Length():F0}"
                : Loc.GetString("rmc-overwatch-console-na");

            Health.Visible = !entry.IsTripod;
            Health.SetHealth(marine.Health, marine.State == MobState.Dead);
            UpdateStatus(entry);

            Feed.Disabled = !hasCamera;
            CMUOverwatchCrt.SetLit(Feed, feedOpen);

            Hidden = hidden;
            Hide.Text = Hidden ? Loc.GetString("cmu-overwatch-roster-show") : Loc.GetString("cmu-overwatch-roster-hide");
            Hide.ToolTip = Hidden
                ? Loc.GetString("rmc-overwatch-console-show-marine")
                : Loc.GetString("rmc-overwatch-console-hide-marine");

            Hide.Visible = !isLeader && !entry.IsTripod;
            Promote.Visible = !isLeader && !entry.IsTripod;
        }

        private void UpdateStatus(CMURosterEntry entry)
        {
            var marine = entry.Marine;
            Bleeding.Visible = false;
            Ssd.Visible = false;

            if (entry.IsTripod)
            {
                SetLabel(State, Loc.GetString("rmc-overwatch-tripod-camera-active"), CrtTerminalPalette.Text);
                return;
            }

            switch (marine.State)
            {
                case MobState.Dead:
                    SetLabel(State, Loc.GetString("rmc-overwatch-console-state-dead"), CrtTerminalPalette.Alert);
                    return;
                case MobState.Critical:
                    SetLabel(State, Loc.GetString("cmu-overwatch-roster-crit"), CrtTerminalPalette.Caution);
                    break;
                default:
                    if (marine.Health is { } health)
                        SetLabel(State, $"{MathF.Round(health * 100):F0}%", CMUOverwatchHealthBar.ColorFor(health));
                    else
                        SetLabel(State, Loc.GetString("rmc-overwatch-console-state-conscious"), CrtTerminalPalette.Accent);
                    break;
            }

            switch (marine.Bleeding)
            {
                case ExternalBleedTier.Arterial:
                    SetLabel(Bleeding, Loc.GetString("cmu-overwatch-roster-arterial"), CrtTerminalPalette.Alert);
                    Bleeding.Visible = true;
                    break;
                case ExternalBleedTier.Severe:
                    SetLabel(Bleeding, Loc.GetString("cmu-overwatch-roster-bleeding-heavy"), CrtTerminalPalette.Alert);
                    Bleeding.Visible = true;
                    break;
                case ExternalBleedTier.Minor or ExternalBleedTier.Moderate:
                    SetLabel(Bleeding, Loc.GetString("cmu-overwatch-roster-bleeding"), CrtTerminalPalette.Caution);
                    Bleeding.Visible = true;
                    break;
            }

            if (marine.SSD)
            {
                Ssd.FontColorOverride = CrtTerminalPalette.TextDim;
                Ssd.Visible = true;
            }
        }

        private static void SetLabel(Label label, string text, Color color)
        {
            label.Text = text;
            label.FontColorOverride = color;
        }
    }
}
