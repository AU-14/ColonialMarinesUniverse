using System.Linq;
using Content.Shared.CMU14.Yautja;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.CMU14.Yautja.ClanHall;

// leaderboard tab: every clan by honor, every hunter by honor
public sealed partial class YautjaClanHallWindow
{
    private const int AllClans = -1;
    private const int NoClan = -2;

    private HudButton _clansButton = default!;
    private HudButton _huntersButton = default!;
    private HudButton _foundButton = default!;
    private HudFrame _foundForm = default!;
    private LineEdit _foundName = default!;
    private LineEdit _foundColor = default!;
    private HudFrame _clanBoard = default!;
    private HudFrame _hunterBoard = default!;
    private BoxContainer _hunterRows = default!;
    private LineEdit _hunterSearch = default!;
    private OptionButton _hunterFilter = default!;
    private readonly List<int> _filterIds = new();
    private int _filter = AllClans;
    private bool _showHunters;

    private Control BuildBoardsPage()
    {
        var page = VBox(10);

        var modes = HBox(6);
        _clansButton = Button("cmu-yautja-hall-board-clans", () => ShowBoard(false));
        _huntersButton = Button("cmu-yautja-hall-board-hunters", () => ShowBoard(true));
        modes.AddChild(_clansButton);
        modes.AddChild(_huntersButton);
        modes.AddChild(Gap());
        _foundButton = Button("cmu-yautja-hall-found", () =>
        {
            _foundForm.Visible = !_foundForm.Visible;
            _foundButton.Selected = _foundForm.Visible;
        });
        modes.AddChild(_foundButton);
        page.AddChild(modes);

        _foundForm = Frame("cmu-yautja-hall-found");
        _foundForm.Visible = false;
        var form = HBox(6);
        form.AddChild(Caption(Loc.GetString("cmu-yautja-hall-name")));
        _foundName = Hud.LineEdit();
        _foundName.HorizontalExpand = true;
        form.AddChild(_foundName);
        form.AddChild(Caption(Loc.GetString("cmu-yautja-hall-color")));
        _foundColor = Hud.LineEdit();
        _foundColor.MinWidth = 90;
        _foundColor.Text = "#c62d2d";
        form.AddChild(_foundColor);
        form.AddChild(Button("cmu-yautja-hall-create", () =>
        {
            if (string.IsNullOrWhiteSpace(_foundName.Text))
                return;

            Request(new YautjaClanHallFoundClanMessage(_foundName.Text, _foundColor.Text.Trim()));
            _foundName.Text = string.Empty;
            _foundForm.Visible = false;
            _foundButton.Selected = false;
        }));
        _foundForm.Body.AddChild(form);
        page.AddChild(_foundForm);

        _clanBoard = Frame("cmu-yautja-hall-board-clans");
        page.AddChild(_clanBoard);

        _hunterBoard = Frame("cmu-yautja-hall-board-hunters");
        var filters = HBox(6);
        _hunterSearch = Hud.LineEdit(Loc.GetString("cmu-yautja-hall-search"));
        _hunterSearch.HorizontalExpand = true;
        _hunterSearch.OnTextChanged += _ => RebuildHunterBoard();
        // enter asks the server, so staff can open someone who isn't listed yet
        _hunterSearch.OnTextEntered += args =>
        {
            if (!string.IsNullOrWhiteSpace(args.Text))
                Request(new YautjaClanHallFindMessage(args.Text));
        };
        filters.AddChild(_hunterSearch);
        _hunterFilter = new OptionButton { MinWidth = 180, StyleBoxOverride = Hud.Field() };
        _hunterFilter.OnItemSelected += args =>
        {
            _hunterFilter.SelectId(args.Id);
            _filter = _filterIds[args.Id];
            RebuildHunterBoard();
        };
        filters.AddChild(_hunterFilter);
        _hunterBoard.Body.AddChild(filters);
        _hunterRows = VBox(1);
        _hunterBoard.Body.AddChild(_hunterRows);
        page.AddChild(_hunterBoard);

        ShowBoard(false);
        return page;
    }

    private void ShowBoard(bool hunters)
    {
        _showHunters = hunters;
        _clansButton.Selected = !hunters;
        _huntersButton.Selected = hunters;
        _clanBoard.Visible = !hunters;
        _hunterBoard.Visible = hunters;
    }

    private void UpdateBoardsPage(YautjaClanHallEuiState state)
    {
        _foundButton.Visible = state.Viewer.Council;
        if (!state.Viewer.Council)
            _foundForm.Visible = false;

        _clanBoard.Body.RemoveAllChildren();
        var top = state.Clans.Count == 0 ? 1 : Math.Max(1, state.Clans.Max(c => c.Honor));
        var header = HBox(8);
        header.Margin = new Thickness(8, 0, 4, 2);
        header.AddChild(new Label { Text = "#", MinWidth = 28, FontOverride = _small, FontColorOverride = Hud.Muted });
        header.AddChild(new Label { Text = Loc.GetString("cmu-yautja-hall-col-clan").ToUpperInvariant(), HorizontalExpand = true, FontOverride = _small, FontColorOverride = Hud.Muted, Margin = new Thickness(30, 0, 0, 0) });
        header.AddChild(new Label { Text = Loc.GetString("cmu-yautja-hall-col-leader").ToUpperInvariant(), MinWidth = 150, FontOverride = _small, FontColorOverride = Hud.Muted });
        header.AddChild(new Label { Text = Loc.GetString("cmu-yautja-hall-col-members").ToUpperInvariant(), MinWidth = 70, FontOverride = _small, FontColorOverride = Hud.Muted, Align = Label.AlignMode.Right });
        header.AddChild(new Label { Text = Loc.GetString("cmu-yautja-hall-col-honor").ToUpperInvariant(), MinWidth = 190, FontOverride = _small, FontColorOverride = Hud.Muted, Align = Label.AlignMode.Right });
        _clanBoard.Body.AddChild(header);

        for (var i = 0; i < state.Clans.Count; i++)
        {
            var clan = state.Clans[i];
            var row = new HudRow { Selected = clan.Id == state.Viewer.ClanId, Disabled = !clan.CanView };
            if (!clan.CanView)
                row.ToolTip = Loc.GetString("cmu-yautja-hall-clan-locked");
            var line = HBox(8);
            line.Margin = new Thickness(8, 4, 4, 4);
            var number = Hud.Write((i + 1).ToString(), _label, i < 3 ? Hud.Hot : Hud.Muted);
            number.MinWidth = 28;
            number.VerticalAlignment = VAlignment.Center;
            line.AddChild(number);
            line.AddChild(new SigilRing(SigilTexture(clan.Sigil), Color.TryFromHex(clan.Color, out var c) ? c : Hud.Bright, 28));
            var name = Hud.Write(clan.Name, _bodyBold, Hud.Text, clip: true);
            name.HorizontalExpand = true;
            name.VerticalAlignment = VAlignment.Center;
            line.AddChild(name);
            var leader = Hud.Write(clan.LeaderName ?? "-", _body, Hud.Muted, clip: true);
            leader.MinWidth = 150;
            leader.VerticalAlignment = VAlignment.Center;
            line.AddChild(leader);
            var members = Hud.Write(clan.Members.ToString(), _label, Hud.Label);
            members.MinWidth = 70;
            members.Align = Label.AlignMode.Right;
            members.VerticalAlignment = VAlignment.Center;
            line.AddChild(members);
            line.AddChild(new HudBar(clan.Honor / (float) top));
            var honor = Hud.Write(clan.Honor.ToString(), _label, Hud.Text);
            honor.MinWidth = 60;
            honor.Align = Label.AlignMode.Right;
            honor.VerticalAlignment = VAlignment.Center;
            line.AddChild(honor);
            row.AddChild(line);
            var id = clan.Id;
            row.OnPressed += _ => OpenClan(id);
            _clanBoard.Body.AddChild(row);
        }

        if (state.Clans.Count == 0)
            _clanBoard.Body.AddChild(Empty("cmu-yautja-hall-no-clans"));

        var selected = _filter;
        _hunterFilter.Clear();
        _filterIds.Clear();
        _hunterFilter.AddItem(Loc.GetString("cmu-yautja-hall-filter-all"), _filterIds.Count);
        _filterIds.Add(AllClans);
        _hunterFilter.AddItem(Loc.GetString("cmu-yautja-hall-clanless"), _filterIds.Count);
        _filterIds.Add(NoClan);
        foreach (var clan in state.Clans)
        {
            _hunterFilter.AddItem(clan.Name, _filterIds.Count);
            _filterIds.Add(clan.Id);
        }

        var index = _filterIds.IndexOf(selected);
        _filter = index < 0 ? AllClans : selected;
        _hunterFilter.SelectId(index < 0 ? 0 : index);
        RebuildHunterBoard();
    }

    private void RebuildHunterBoard()
    {
        _hunterRows.RemoveAllChildren();
        if (_state is not { } state)
            return;

        var search = _hunterSearch.Text.Trim();
        var shown = 0;
        for (var i = 0; i < state.Hunters.Count; i++)
        {
            var hunter = state.Hunters[i];
            if (_filter == NoClan && hunter.ClanId != null ||
                _filter >= 0 && hunter.ClanId != _filter ||
                search.Length > 0 && !hunter.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            _hunterRows.AddChild(HunterRow(hunter, (i + 1).ToString(), showClan: true));
            shown++;
        }

        if (shown == 0)
            _hunterRows.AddChild(Empty("cmu-yautja-hall-no-hunters"));
    }
}
