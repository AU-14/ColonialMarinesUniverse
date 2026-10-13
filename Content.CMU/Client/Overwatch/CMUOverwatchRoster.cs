using System.Linq;
using Content.Client.CMU14.Interface;
using Content.Client.Lobby.UI;
using Content.Client.Message;
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
    private const float CallsignWidth = 120;
    private const float NameWidth = 210;
    private const float RoleWidth = 165;
    private const float StatusWidth = 240;
    private const float LocationWidth = 180;
    private const float DistanceWidth = 70;
    private const float ButtonWidth = 46;

    public event Action<NetEntity>? OnWatch;
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
        row.Name.OnPressed += _ => OnWatch?.Invoke(id);
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
            SeparationOverride = 6,
            Margin = new Thickness(4, 0),
        };

        header.AddChild(HeaderCell("cmu-overwatch-roster-col-callsign", CallsignWidth));
        header.AddChild(HeaderCell("cmu-overwatch-roster-col-name", NameWidth, expand: true));
        header.AddChild(HeaderCell("cmu-overwatch-roster-col-role", RoleWidth));
        header.AddChild(HeaderCell("cmu-overwatch-roster-col-status", StatusWidth));
        header.AddChild(HeaderCell("cmu-overwatch-roster-col-location", LocationWidth));
        header.AddChild(HeaderCell("cmu-overwatch-roster-col-distance", DistanceWidth));
        header.AddChild(new Control { MinWidth = ButtonWidth * 3 + 12 });
        return header;
    }

    private static Label HeaderCell(string loc, float width, bool expand = false)
    {
        return new Label
        {
            Text = Loc.GetString(loc),
            MinWidth = width,
            HorizontalExpand = expand,
            StyleClasses = { StyleNano.StyleClassCrtDimText },
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
        public readonly Button Name;
        public readonly Label Role;
        public readonly CMUOverwatchHealthBar Health;
        public readonly RichTextLabel Status;
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

            Callsign = new Label { MinWidth = CallsignWidth, MaxWidth = CallsignWidth, ClipText = true, VerticalAlignment = VAlignment.Center };
            Name = new Button { MinWidth = NameWidth, HorizontalExpand = true, ClipText = true };
            Role = new Label { MinWidth = RoleWidth, MaxWidth = RoleWidth, ClipText = true, VerticalAlignment = VAlignment.Center };
            Health = new CMUOverwatchHealthBar();
            Status = new RichTextLabel { VerticalAlignment = VAlignment.Center };
            Location = new Label { MinWidth = LocationWidth, MaxWidth = LocationWidth, ClipText = true, VerticalAlignment = VAlignment.Center };
            Distance = new Label { MinWidth = DistanceWidth, MaxWidth = DistanceWidth, VerticalAlignment = VAlignment.Center };
            Feed = new Button { MinWidth = ButtonWidth, Text = Loc.GetString("cmu-overwatch-roster-feed"), ToolTip = Loc.GetString("cmu-overwatch-roster-feed-tooltip") };
            Hide = new Button { MinWidth = ButtonWidth };
            Promote = new Button { MinWidth = ButtonWidth, Text = Loc.GetString("cmu-overwatch-roster-promote"), ToolTip = Loc.GetString("rmc-overwatch-console-promote-squad-leader") };

            var status = new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal,
                MinWidth = StatusWidth,
                MaxWidth = StatusWidth,
                SeparationOverride = 6,
                Children = { Health, Status },
            };

            AddChild(new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal,
                SeparationOverride = 6,
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
            Name.Disabled = !hasCamera;
            Name.ToolTip = hasCamera ? Loc.GetString("cmu-overwatch-roster-watch-tooltip") : null;

            Role.Text = entry.RoleName;
            Location.Text = marine.AreaName;

            Distance.Text = marine.LeaderDistance is { } distance && distance.LengthSquared() > 0
                ? $"{distance.Length():F0}"
                : Loc.GetString("rmc-overwatch-console-na");

            Health.Visible = !entry.IsTripod;
            Health.SetHealth(marine.Health, marine.State == MobState.Dead);
            Status.SetMarkupPermissive(StatusMarkup(entry));

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

        private static string StatusMarkup(CMURosterEntry entry)
        {
            var marine = entry.Marine;
            if (entry.IsTripod)
                return Colored(Loc.GetString("rmc-overwatch-tripod-camera-active"), CrtTerminalPalette.Text);

            string state = marine.State switch
            {
                MobState.Dead => Colored(Loc.GetString("rmc-overwatch-console-state-dead"), CrtTerminalPalette.Alert),
                MobState.Critical => Colored(Loc.GetString("cmu-overwatch-roster-crit"), CrtTerminalPalette.Caution),
                _ when marine.Health is { } health => Colored($"{MathF.Round(health * 100):F0}%", CMUOverwatchHealthBar.ColorFor(health)),
                _ => Colored(Loc.GetString("rmc-overwatch-console-state-conscious"), CrtTerminalPalette.Accent),
            };

            if (marine.State != MobState.Dead)
            {
                state += marine.Bleeding switch
                {
                    ExternalBleedTier.Arterial => " " + Colored(Loc.GetString("cmu-overwatch-roster-arterial"), CrtTerminalPalette.Alert),
                    ExternalBleedTier.Severe => " " + Colored(Loc.GetString("cmu-overwatch-roster-bleeding-heavy"), CrtTerminalPalette.Alert),
                    ExternalBleedTier.Minor or ExternalBleedTier.Moderate => " " + Colored(Loc.GetString("cmu-overwatch-roster-bleeding"), CrtTerminalPalette.Caution),
                    _ => string.Empty,
                };

                if (marine.SSD)
                    state += " " + Colored(Loc.GetString("rmc-overwatch-console-ssd"), CrtTerminalPalette.TextDim);
            }

            return state;
        }

        private static string Colored(string text, Robust.Shared.Maths.Color color)
        {
            return $"[color={color.ToHex()}]{FormattedMessageEscape(text)}[/color]";
        }

        private static string FormattedMessageEscape(string text)
        {
            return Robust.Shared.Utility.FormattedMessage.EscapeText(text);
        }
    }
}
