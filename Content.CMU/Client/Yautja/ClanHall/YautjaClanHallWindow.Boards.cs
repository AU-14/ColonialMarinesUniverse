using System.Linq;
using Content.Shared.CMU14.Yautja;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.CMU14.Yautja.ClanHall;

// leaderboard tab: every clan by honor. no individual hunter ranking on purpose
public sealed partial class YautjaClanHallWindow
{
    private HudButton _foundButton = default!;
    private HudFrame _foundForm = default!;
    private LineEdit _foundName = default!;
    private LineEdit _foundColor = default!;
    private HudFrame _clanBoard = default!;

    private Control BuildBoardsPage()
    {
        var page = VBox(10);

        var modes = HBox(6);
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

        return page;
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
    }
}
