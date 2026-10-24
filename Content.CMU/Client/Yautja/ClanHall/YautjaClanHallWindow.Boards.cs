using System.Linq;
using System.Numerics;
using Content.Shared.CMU14.Yautja;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.CMU14.Yautja.ClanHall;

// the boards: every clan ranked by honor, every hunter ranked by honor
public sealed partial class YautjaClanHallWindow
{
    private const int NoClanFilter = -1;
    private const int ClanlessFilter = -2;

    private HallButton _clansMode = default!;
    private HallButton _huntersMode = default!;
    private Control _clanBoard = default!;
    private Control _hunterBoard = default!;
    private BoxContainer _clanRows = default!;
    private BoxContainer _hunterRows = default!;
    private Control _foundRow = default!;
    private LineEdit _foundName = default!;
    private LineEdit _foundColor = default!;
    private LineEdit _hunterSearch = default!;
    private OptionButton _hunterClanFilter = default!;
    private readonly List<int> _hunterFilterIds = new();
    private int _hunterFilter = NoClanFilter;
    private bool _showHunters;

    private Control BuildBoardsPage()
    {
        var page = YautjaClanHallStyle.VBox(8);
        page.HorizontalExpand = page.VerticalExpand = true;

        var modes = YautjaClanHallStyle.HBox(4);
        _clansMode = new HallButton(Loc.GetString("cmu-yautja-hall-board-clans"), _heading, YautjaClanHallStyle.HotRed, 14, 4);
        _huntersMode = new HallButton(Loc.GetString("cmu-yautja-hall-board-hunters"), _heading, YautjaClanHallStyle.HotRed, 14, 4);
        _clansMode.OnPressed += _ => SetBoardMode(false);
        _huntersMode.OnPressed += _ => SetBoardMode(true);
        modes.AddChild(_clansMode);
        modes.AddChild(_huntersMode);
        page.AddChild(modes);

        // clans
        var clans = YautjaClanHallStyle.VBox(6);
        clans.VerticalExpand = true;
        clans.AddChild(BoardHeader(
            ("#", 40, false),
            (Loc.GetString("cmu-yautja-hall-col-clan"), 0, false),
            (Loc.GetString("cmu-yautja-hall-col-leader"), 150, false),
            (Loc.GetString("cmu-yautja-hall-col-hunters"), 90, true),
            (Loc.GetString("cmu-yautja-hall-col-honor"), 240, true)));
        _clanRows = YautjaClanHallStyle.VBox(3);
        clans.AddChild(Scroll(_clanRows));

        var found = YautjaClanHallStyle.Panel(YautjaClanHallStyle.Card, YautjaClanHallStyle.MutedBorder, 10, 6);
        var foundRow = YautjaClanHallStyle.HBox(6);
        foundRow.AddChild(YautjaClanHallStyle.Label(Loc.GetString("cmu-yautja-hall-found"), _readout, YautjaClanHallStyle.HotRed));
        _foundName = new LineEdit { HorizontalExpand = true, PlaceHolder = Loc.GetString("cmu-yautja-hall-found-name") };
        _foundColor = new LineEdit { MinWidth = 84, Text = "#c62d2d" };
        var foundButton = new HallButton(Loc.GetString("cmu-yautja-hall-found-button"), _readout, YautjaClanHallStyle.HotRed);
        foundButton.OnPressed += _ =>
        {
            if (string.IsNullOrWhiteSpace(_foundName.Text))
                return;

            Request(new YautjaClanHallFoundClanMessage(_foundName.Text, _foundColor.Text.Trim()));
            _foundName.Text = string.Empty;
        };
        foundRow.AddChild(_foundName);
        foundRow.AddChild(_foundColor);
        foundRow.AddChild(foundButton);
        found.AddChild(foundRow);
        _foundRow = found;
        clans.AddChild(found);
        _clanBoard = clans;
        page.AddChild(clans);

        // hunters
        var hunters = YautjaClanHallStyle.VBox(6);
        hunters.VerticalExpand = true;
        var filters = YautjaClanHallStyle.HBox(6);
        _hunterSearch = new LineEdit { HorizontalExpand = true, PlaceHolder = Loc.GetString("cmu-yautja-hall-search") };
        _hunterSearch.OnTextChanged += _ => RebuildHunterBoard();
        // enter asks the server, so staff can open someone who isn't on the board yet
        _hunterSearch.OnTextEntered += args =>
        {
            if (!string.IsNullOrWhiteSpace(args.Text))
                Request(new YautjaClanHallFindMessage(args.Text));
        };
        filters.AddChild(_hunterSearch);
        _hunterClanFilter = new OptionButton { MinWidth = 180 };
        _hunterClanFilter.OnItemSelected += args =>
        {
            _hunterClanFilter.SelectId(args.Id);
            _hunterFilter = args.Id >= 0 && args.Id < _hunterFilterIds.Count ? _hunterFilterIds[args.Id] : NoClanFilter;
            RebuildHunterBoard();
        };
        filters.AddChild(_hunterClanFilter);
        hunters.AddChild(filters);
        hunters.AddChild(BoardHeader(
            ("#", 40, false),
            (Loc.GetString("cmu-yautja-hall-col-hunter"), 0, false),
            (Loc.GetString("cmu-yautja-hall-col-clan"), 150, false),
            (Loc.GetString("cmu-yautja-hall-col-honor"), 88, true)));
        _hunterRows = YautjaClanHallStyle.VBox(2);
        hunters.AddChild(Scroll(_hunterRows));
        _hunterBoard = hunters;
        page.AddChild(hunters);

        SetBoardMode(false);
        return page;
    }

    private void SetBoardMode(bool hunters)
    {
        if (hunters != _showHunters)
            _holo.Flicker();

        _showHunters = hunters;
        _clansMode.Selected = !hunters;
        _huntersMode.Selected = hunters;
        _clanBoard.Visible = !hunters;
        _hunterBoard.Visible = hunters;
    }

    private Control BoardHeader(params (string Text, float Width, bool Right)[] columns)
    {
        var row = YautjaClanHallStyle.HBox(8);
        row.Margin = new Thickness(6, 0);
        foreach (var (text, width, alignRight) in columns)
        {
            var label = YautjaClanHallStyle.Label(text.ToUpperInvariant(), _small, YautjaClanHallStyle.Dim);
            if (width > 0)
                label.MinWidth = width;
            else
                label.HorizontalExpand = true;
            if (alignRight)
                label.Align = Label.AlignMode.Right;
            row.AddChild(label);
        }

        return row;
    }

    private void UpdateBoardsPage(YautjaClanHallEuiState state)
    {
        _foundRow.Visible = state.Viewer.Council;

        _clanRows.RemoveAllChildren();
        var top = Math.Max(1, state.Clans.Count == 0 ? 1 : state.Clans.Max(c => c.Honor));
        for (var i = 0; i < state.Clans.Count; i++)
            _clanRows.AddChild(ClanBoardRow(state.Clans[i], i + 1, top, state.Viewer.ClanId == state.Clans[i].Id));

        if (state.Clans.Count == 0)
            _clanRows.AddChild(Empty(Loc.GetString("cmu-yautja-hall-no-clans")));

        var selected = _hunterFilter;
        _hunterClanFilter.Clear();
        _hunterFilterIds.Clear();
        _hunterClanFilter.AddItem(Loc.GetString("cmu-yautja-hall-filter-all"), _hunterFilterIds.Count);
        _hunterFilterIds.Add(NoClanFilter);
        _hunterClanFilter.AddItem(Loc.GetString("cmu-yautja-hall-clanless"), _hunterFilterIds.Count);
        _hunterFilterIds.Add(ClanlessFilter);
        foreach (var clan in state.Clans)
        {
            _hunterClanFilter.AddItem(clan.Name, _hunterFilterIds.Count);
            _hunterFilterIds.Add(clan.Id);
        }

        var index = _hunterFilterIds.IndexOf(selected);
        _hunterFilter = index < 0 ? NoClanFilter : selected;
        _hunterClanFilter.SelectId(index < 0 ? 0 : index);
        RebuildHunterBoard();
    }

    private Control ClanBoardRow(YautjaClanHallClanRow clan, int standing, int top, bool own)
    {
        var button = new HallButton(string.Empty, _body, YautjaClanHallStyle.HotRed, 0, 0)
        {
            HorizontalExpand = true,
            Disabled = !clan.CanView,
            Selected = own,
            ToolTip = clan.CanView ? null : Loc.GetString("cmu-yautja-hall-clan-locked"),
        };
        button.Label.Visible = false;

        var color = YautjaClanHallStyle.ClanColor(clan.Color);
        var row = YautjaClanHallStyle.HBox(8);
        row.Margin = new Thickness(6, 4);
        var number = YautjaClanHallStyle.Label(standing.ToString(), _heading, standing <= 3 ? YautjaClanHallStyle.Honor : YautjaClanHallStyle.Dim);
        number.MinWidth = 40;
        number.VerticalAlignment = VAlignment.Center;
        row.AddChild(number);
        var badge = new SigilBadge(30);
        badge.Set(clan.Sigil, color);
        row.AddChild(badge);
        var name = YautjaClanHallStyle.Label(clan.Name, _bodyBold, YautjaClanHallStyle.Readable(color), clip: true);
        name.HorizontalExpand = true;
        name.VerticalAlignment = VAlignment.Center;
        row.AddChild(name);
        var leader = YautjaClanHallStyle.Label(clan.LeaderName ?? "—", _body, YautjaClanHallStyle.Muted, clip: true);
        leader.MinWidth = 150;
        leader.VerticalAlignment = VAlignment.Center;
        row.AddChild(leader);
        var members = YautjaClanHallStyle.Label(clan.Members.ToString(), _readout, YautjaClanHallStyle.Text);
        members.MinWidth = 90;
        members.Align = Label.AlignMode.Right;
        members.VerticalAlignment = VAlignment.Center;
        row.AddChild(members);
        row.AddChild(new HonorBar(clan.Honor, top, _readout) { MinWidth = 240, VerticalAlignment = VAlignment.Center });
        button.AddChild(row);

        var id = clan.Id;
        button.OnPressed += _ => OpenClan(id);
        return button;
    }

    private void RebuildHunterBoard()
    {
        _hunterRows.RemoveAllChildren();
        if (_state is not { } state)
            return;

        var filter = _hunterSearch.Text.Trim();
        var shown = 0;
        for (var i = 0; i < state.Hunters.Count; i++)
        {
            var hunter = state.Hunters[i];
            if (_hunterFilter == ClanlessFilter && hunter.ClanId != null ||
                _hunterFilter >= 0 && hunter.ClanId != _hunterFilter ||
                filter.Length > 0 && !hunter.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            _hunterRows.AddChild(HunterLine(hunter, (i + 1).ToString(), showClan: true, highlight: hunter.PlayerId == state.Viewer.PlayerId));
            shown++;
        }

        if (shown == 0)
            _hunterRows.AddChild(Empty(Loc.GetString("cmu-yautja-hall-no-hunters")));
    }

    // honor as a bar against the top clan, number on the end
    private sealed class HonorBar : Control
    {
        private readonly float _fill;
        private readonly Label _label;

        public HonorBar(int honor, int top, Font font)
        {
            _fill = Math.Clamp(honor / (float) top, 0, 1);
            MinHeight = 16;
            MouseFilter = MouseFilterMode.Ignore;
            _label = YautjaClanHallStyle.Label(YautjaClanHallStyle.Number(honor), font, YautjaClanHallStyle.Honor);
            _label.HorizontalAlignment = HAlignment.Right;
            _label.VerticalAlignment = VAlignment.Center;
            AddChild(_label);
        }

        protected override void Draw(DrawingHandleScreen handle)
        {
            base.Draw(handle);
            var s = UIScale;
            var barWidth = PixelSize.X - 74 * s;
            var y = PixelSize.Y / 2;
            var bar = UIBox2.FromDimensions(0, y - 3 * s, barWidth, 6 * s);
            handle.DrawRect(bar, YautjaClanHallStyle.HonorDim.WithAlpha(0.25f));
            handle.DrawRect(UIBox2.FromDimensions(bar.Left, bar.Top, barWidth * _fill, bar.Height), YautjaClanHallStyle.Honor.WithAlpha(0.85f));
            // tick marks every tenth, so close standings still read
            for (var i = 1; i < 10; i++)
            {
                var x = barWidth * i / 10f;
                handle.DrawLine(new Vector2(x, bar.Top), new Vector2(x, bar.Bottom), YautjaClanHallStyle.Screen.WithAlpha(0.6f));
            }
        }
    }
}
