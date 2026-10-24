using System.Linq;
using System.Numerics;
using Content.Shared.CMU14.Yautja;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.Yautja.ClanHall;

// the clan page: banner, notice, description, elite notes, roster by rank, and the editors for
// whoever is allowed them. editors live outside the rebuilt parts so a refresh never eats typing
public sealed partial class YautjaClanHallWindow
{
    private static readonly string[] Swatches =
        ["#c62d2d", "#ff7a3d", "#e8a23a", "#d9d2b0", "#7fbf6a", "#3fa7a0", "#4f7bd6", "#8e5bd6", "#c04f9a", "#8c8c8c"];

    private OptionButton _clanPicker = default!;
    private readonly List<int> _clanPickerIds = new();
    private SigilBadge _banner = default!;
    private Label _bannerName = default!;
    private Label _bannerLine = default!;
    private Label _bannerHonor = default!;
    private BoxContainer _clanText = default!;
    private BoxContainer _roster = default!;
    private BoxContainer _petitions = default!;
    private BoxContainer _clanActions = default!;
    private Control _clanEmpty = default!;
    private Control _clanBody = default!;

    private TextEditor _noticeEditor = default!;
    private TextEditor _descriptionEditor = default!;
    private TextEditor _notesEditor = default!;
    private Control _lookEditor = default!;
    private LineEdit _lookName = default!;
    private LineEdit _lookColor = default!;
    private SigilBadge _lookPreview = default!;
    private Label _lookStyle = default!;
    private readonly List<HallButton> _materialButtons = new();
    private int _lookStyleIndex = 1;
    private string _lookMaterial = YautjaClanSigils.Materials[0];
    private LineEdit _honorEdit = default!;
    private Control _honorRow = default!;
    private ConfirmButton _disband = default!;
    private int? _editingClan;

    private Control BuildClanPage()
    {
        var page = YautjaClanHallStyle.VBox(10);
        page.HorizontalExpand = page.VerticalExpand = true;

        _clanEmpty = YautjaClanHallStyle.VBox(8);
        _clanEmpty.Visible = false;
        page.AddChild(_clanEmpty);

        var bodyBox = YautjaClanHallStyle.VBox(10);
        bodyBox.VerticalExpand = true;
        _clanBody = bodyBox;
        page.AddChild(bodyBox);

        // banner
        var banner = YautjaClanHallStyle.Panel(YautjaClanHallStyle.Card, YautjaClanHallStyle.Border, 12, 8);
        var bannerRow = YautjaClanHallStyle.HBox(14);
        _banner = new SigilBadge(64);
        bannerRow.AddChild(_banner);
        var names = YautjaClanHallStyle.VBox(2);
        names.HorizontalExpand = true;
        names.VerticalAlignment = VAlignment.Center;
        _bannerName = YautjaClanHallStyle.Label(string.Empty, _big, YautjaClanHallStyle.Text, clip: true);
        _bannerLine = YautjaClanHallStyle.Label(string.Empty, _readout, YautjaClanHallStyle.Muted);
        names.AddChild(_bannerName);
        names.AddChild(_bannerLine);
        bannerRow.AddChild(names);
        var right = YautjaClanHallStyle.VBox(4);
        right.VerticalAlignment = VAlignment.Center;
        _clanPicker = new OptionButton { MinWidth = 200 };
        _clanPicker.OnItemSelected += args =>
        {
            _clanPicker.SelectId(args.Id);
            if (args.Id >= 0 && args.Id < _clanPickerIds.Count)
                OpenClan(_clanPickerIds[args.Id]);
        };
        right.AddChild(_clanPicker);
        _bannerHonor = YautjaClanHallStyle.Label(string.Empty, _big, YautjaClanHallStyle.Honor);
        _bannerHonor.HorizontalAlignment = HAlignment.Right;
        right.AddChild(_bannerHonor);
        bannerRow.AddChild(right);
        banner.AddChild(bannerRow);
        bodyBox.AddChild(banner);

        var columns = YautjaClanHallStyle.HBox(12);
        columns.VerticalExpand = true;
        bodyBox.AddChild(columns);

        // left: words
        var left = YautjaClanHallStyle.VBox(10);
        left.SizeFlagsStretchRatio = 1.25f;
        left.HorizontalExpand = true;
        _clanActions = YautjaClanHallStyle.HBox(6);
        left.AddChild(_clanActions);
        // editors open right under the buttons that opened them, above the text they change
        _noticeEditor = new TextEditor(this, YautjaClanHallText.Notice);
        _descriptionEditor = new TextEditor(this, YautjaClanHallText.Description);
        _notesEditor = new TextEditor(this, YautjaClanHallText.EliteNotes);
        _lookEditor = BuildLookEditor();
        left.AddChild(_lookEditor);
        left.AddChild(_noticeEditor);
        left.AddChild(_descriptionEditor);
        left.AddChild(_notesEditor);

        _clanText = YautjaClanHallStyle.VBox(10);
        left.AddChild(_clanText);
        columns.AddChild(Scroll(left));

        // right: the hunters
        var rosterColumn = YautjaClanHallStyle.VBox(6);
        rosterColumn.HorizontalExpand = true;
        _petitions = YautjaClanHallStyle.VBox(4);
        rosterColumn.AddChild(_petitions);
        rosterColumn.AddChild(SectionTitle(Loc.GetString("cmu-yautja-hall-roster")));
        _roster = YautjaClanHallStyle.VBox(2);
        rosterColumn.AddChild(Scroll(_roster));
        columns.AddChild(rosterColumn);

        return page;
    }

    private Control BuildLookEditor()
    {
        var panel = YautjaClanHallStyle.Panel(YautjaClanHallStyle.Card, YautjaClanHallStyle.HotRed, 10, 8);
        panel.Visible = false;
        var box = YautjaClanHallStyle.VBox(8);
        box.AddChild(SectionTitle(Loc.GetString("cmu-yautja-hall-edit-look")));

        var nameRow = YautjaClanHallStyle.HBox(8);
        nameRow.AddChild(YautjaClanHallStyle.Label(Loc.GetString("cmu-yautja-hall-name"), _readout, YautjaClanHallStyle.Muted));
        _lookName = new LineEdit { HorizontalExpand = true };
        _lookName.OnTextChanged += _ => _lookName.Text = _lookName.Text.Length > YautjaClanHallPolicy.MaxNameLength
            ? _lookName.Text[..YautjaClanHallPolicy.MaxNameLength]
            : _lookName.Text;
        nameRow.AddChild(_lookName);
        box.AddChild(nameRow);

        var sigilRow = YautjaClanHallStyle.HBox(10);
        _lookPreview = new SigilBadge(56);
        sigilRow.AddChild(_lookPreview);
        var sigilControls = YautjaClanHallStyle.VBox(6);
        sigilControls.HorizontalExpand = true;
        var styleRow = YautjaClanHallStyle.HBox(6);
        styleRow.AddChild(YautjaClanHallStyle.Label(Loc.GetString("cmu-yautja-hall-sigil"), _readout, YautjaClanHallStyle.Muted));
        var prev = new HallButton("◀", _readout, YautjaClanHallStyle.HotRed, 8, 2);
        prev.OnPressed += _ => StepStyle(-1);
        _lookStyle = YautjaClanHallStyle.Label(string.Empty, _readout, YautjaClanHallStyle.Text);
        _lookStyle.MinWidth = 54;
        _lookStyle.Align = Label.AlignMode.Center;
        var next = new HallButton("▶", _readout, YautjaClanHallStyle.HotRed, 8, 2);
        next.OnPressed += _ => StepStyle(1);
        styleRow.AddChild(prev);
        styleRow.AddChild(_lookStyle);
        styleRow.AddChild(next);
        sigilControls.AddChild(styleRow);
        var materials = YautjaClanHallStyle.HBox(4);
        foreach (var material in YautjaClanSigils.Materials)
        {
            var button = new HallButton(Loc.GetString($"cmu-yautja-hall-material-{material}"), _small, YautjaClanHallStyle.HotRed, 6, 2);
            var picked = material;
            button.OnPressed += _ =>
            {
                _lookMaterial = picked;
                RefreshLookPreview();
            };
            _materialButtons.Add(button);
            materials.AddChild(button);
        }

        sigilControls.AddChild(materials);
        sigilRow.AddChild(sigilControls);
        box.AddChild(sigilRow);

        var colorRow = YautjaClanHallStyle.HBox(4);
        colorRow.AddChild(YautjaClanHallStyle.Label(Loc.GetString("cmu-yautja-hall-color"), _readout, YautjaClanHallStyle.Muted));
        _lookColor = new LineEdit { MinWidth = 84 };
        _lookColor.OnTextChanged += _ => RefreshLookPreview();
        colorRow.AddChild(_lookColor);
        foreach (var hex in Swatches)
        {
            var swatch = new Swatch(YautjaClanHallStyle.ClanColor(hex));
            var picked = hex;
            swatch.OnPressed += _ =>
            {
                _lookColor.Text = picked;
                RefreshLookPreview();
            };
            colorRow.AddChild(swatch);
        }

        box.AddChild(colorRow);

        var buttons = YautjaClanHallStyle.HBox(6);
        buttons.AddChild(YautjaClanHallStyle.Gap());
        var cancel = new HallButton(Loc.GetString("cmu-yautja-hall-cancel"), _readout, YautjaClanHallStyle.Dim);
        cancel.OnPressed += _ => panel.Visible = false;
        var save = new HallButton(Loc.GetString("cmu-yautja-hall-save"), _readout, YautjaClanHallStyle.HotRed);
        save.OnPressed += _ =>
        {
            if (_editingClan is not { } clanId)
                return;

            Request(new YautjaClanHallEditLookMessage(
                clanId,
                _lookName.Text,
                _lookColor.Text.Trim(),
                YautjaClanSigils.Make(_lookStyleIndex, _lookMaterial)));
            panel.Visible = false;
        };
        buttons.AddChild(cancel);
        buttons.AddChild(save);
        box.AddChild(buttons);

        // council only: honor + disband
        var honorRow = YautjaClanHallStyle.HBox(6);
        honorRow.AddChild(YautjaClanHallStyle.Label(Loc.GetString("cmu-yautja-hall-set-honor"), _readout, YautjaClanHallStyle.Honor));
        _honorEdit = new LineEdit { MinWidth = 90 };
        honorRow.AddChild(_honorEdit);
        var setHonor = new HallButton(Loc.GetString("cmu-yautja-hall-apply"), _readout, YautjaClanHallStyle.Honor);
        setHonor.OnPressed += _ =>
        {
            if (_editingClan is { } clanId && int.TryParse(_honorEdit.Text.Trim(), out var value))
                Request(new YautjaClanHallSetHonorMessage(clanId, value));
        };
        honorRow.AddChild(setHonor);
        honorRow.AddChild(YautjaClanHallStyle.Gap());
        _disband = new ConfirmButton(Loc.GetString("cmu-yautja-hall-disband"), Loc.GetString("cmu-yautja-hall-disband-confirm"), _readout);
        _disband.Confirmed += () =>
        {
            if (_editingClan is { } clanId)
            {
                Request(new YautjaClanHallDisbandMessage(clanId));
                panel.Visible = false;
            }
        };
        honorRow.AddChild(_disband);
        _honorRow = honorRow;
        box.AddChild(honorRow);

        panel.AddChild(box);
        return panel;
    }

    private void StepStyle(int by)
    {
        _lookStyleIndex = (_lookStyleIndex - 1 + by + YautjaClanSigils.Styles) % YautjaClanSigils.Styles + 1;
        RefreshLookPreview();
    }

    private void RefreshLookPreview()
    {
        _lookStyle.Text = $"{_lookStyleIndex:00}/{YautjaClanSigils.Styles}";
        var color = YautjaClanHallStyle.ClanColor(_lookColor.Text.Trim());
        _lookPreview.Set(YautjaClanSigils.Make(_lookStyleIndex, _lookMaterial), color);
        for (var i = 0; i < _materialButtons.Count; i++)
            _materialButtons[i].Selected = YautjaClanSigils.Materials[i] == _lookMaterial;
    }

    private void OpenLookEditor(YautjaClanHallClanPage clan)
    {
        _editingClan = clan.Id;
        _lookName.Text = clan.Name;
        _lookName.Editable = clan.CanRename;
        _lookColor.Text = clan.Color;
        if (YautjaClanSigils.TryParse(clan.Sigil, out var style, out var material))
        {
            _lookStyleIndex = style;
            _lookMaterial = material;
        }

        _honorEdit.Text = clan.Honor.ToString();
        _honorRow.Visible = clan.CanSetHonor || clan.CanDisband;
        _disband.Visible = clan.CanDisband;
        RefreshLookPreview();
        _lookEditor.Visible = true;
    }

    private void UpdateClanPage(YautjaClanHallEuiState state)
    {
        var viewable = state.Clans.Where(c => c.CanView).ToList();
        _clanPicker.Clear();
        _clanPickerIds.Clear();
        foreach (var clan in viewable)
        {
            _clanPicker.AddItem(clan.Name, _clanPickerIds.Count);
            _clanPickerIds.Add(clan.Id);
        }

        _clanPicker.Visible = viewable.Count > 1;

        var page = state.Clan;
        _clanBody.Visible = page != null;
        _clanEmpty.Visible = page == null;
        if (page == null)
        {
            BuildClanEmpty(state);
            return;
        }

        var index = _clanPickerIds.IndexOf(page.Id);
        if (index >= 0)
            _clanPicker.SelectId(index);

        // switched clans under an open editor: drop it, it belonged to the other clan
        if (_editingClan != page.Id)
        {
            _editingClan = page.Id;
            _lookEditor.Visible = false;
            _noticeEditor.Close();
            _descriptionEditor.Close();
            _notesEditor.Close();
        }

        var color = YautjaClanHallStyle.ClanColor(page.Color);
        _banner.Set(page.Sigil, color);
        _bannerName.Text = page.Name.ToUpperInvariant();
        _bannerName.FontColorOverride = YautjaClanHallStyle.Readable(color);
        _bannerLine.Text = Loc.GetString("cmu-yautja-hall-clan-line",
            ("standing", page.Standing),
            ("clans", state.Clans.Count),
            ("members", page.Members.Count));
        _bannerHonor.Text = YautjaClanHallStyle.Number(page.Honor);

        // actions across the top of the text column
        _clanActions.RemoveAllChildren();
        if (page.CanEditPresentation)
            AddAction(_clanActions, "cmu-yautja-hall-edit-look", () => OpenLookEditor(page));
        if (page.CanEditNotice)
            AddAction(_clanActions, "cmu-yautja-hall-edit-notice", () => _noticeEditor.Open(page.Id, page.Notice));
        if (page.CanEditPresentation)
            AddAction(_clanActions, "cmu-yautja-hall-edit-description", () => _descriptionEditor.Open(page.Id, page.Description));
        if (page.CanEditEliteNotes)
            AddAction(_clanActions, "cmu-yautja-hall-edit-notes", () => _notesEditor.Open(page.Id, page.EliteNotes ?? string.Empty));

        var viewer = state.Viewer;
        if (viewer.ClanId == null && viewer.CanPetition)
        {
            _clanActions.AddChild(YautjaClanHallStyle.Gap());
            if (viewer.PetitionClanId == page.Id)
            {
                AddAction(_clanActions, "cmu-yautja-hall-withdraw", () => Request(new YautjaClanHallPetitionMessage(null)));
            }
            else
            {
                var id = page.Id;
                AddAction(_clanActions, viewer.PetitionClanId == null ? "cmu-yautja-hall-petition" : "cmu-yautja-hall-petition-switch",
                    () => Request(new YautjaClanHallPetitionMessage(id)), YautjaClanHallStyle.Honor);
            }
        }

        _clanActions.Visible = _clanActions.ChildCount > 0;

        _clanText.RemoveAllChildren();
        if (viewer.PetitionClanId == page.Id)
            _clanText.AddChild(Callout(Loc.GetString("cmu-yautja-hall-petition-pending"), YautjaClanHallStyle.Honor));

        _clanText.AddChild(TextBlock(Loc.GetString("cmu-yautja-hall-notice"),
            string.IsNullOrWhiteSpace(page.Notice) ? Loc.GetString("cmu-yautja-hall-notice-empty") : page.Notice,
            YautjaClanHallStyle.Honor));
        _clanText.AddChild(TextBlock(Loc.GetString("cmu-yautja-hall-description"), page.Description, YautjaClanHallStyle.HotRed));
        if (page.EliteNotes != null)
        {
            _clanText.AddChild(TextBlock(Loc.GetString("cmu-yautja-hall-elite-notes"),
                string.IsNullOrWhiteSpace(page.EliteNotes) ? Loc.GetString("cmu-yautja-hall-notes-empty") : page.EliteNotes,
                YautjaClanHallStyle.Staff));
        }

        // petitions sit over the roster, they're the thing waiting on someone
        _petitions.RemoveAllChildren();
        if (page.Petitions is { Count: > 0 } petitions)
        {
            _petitions.AddChild(SectionTitle(Loc.GetString("cmu-yautja-hall-petitions", ("count", petitions.Count)), YautjaClanHallStyle.Honor));
            foreach (var petitioner in petitions)
            {
                var row = YautjaClanHallStyle.HBox(4);
                var line = HunterLine(petitioner);
                line.HorizontalExpand = true;
                row.AddChild(line);
                var id = petitioner.PlayerId;
                var accept = new HallButton(Loc.GetString("cmu-yautja-hall-accept"), _small, YautjaClanHallStyle.Honor, 6, 2);
                accept.OnPressed += _ => Request(new YautjaClanHallAnswerPetitionMessage(id, true));
                var refuse = new HallButton(Loc.GetString("cmu-yautja-hall-refuse"), _small, YautjaClanHallStyle.HotRed, 6, 2);
                refuse.OnPressed += _ => Request(new YautjaClanHallAnswerPetitionMessage(id, false));
                row.AddChild(accept);
                row.AddChild(refuse);
                _petitions.AddChild(row);
            }
        }

        _roster.RemoveAllChildren();
        foreach (var group in page.Members.GroupBy(member => member.Rank).OrderByDescending(group => group.Key))
        {
            var header = YautjaClanHallStyle.Label(
                $"{YautjaClanHallStyle.RankName(group.Key).ToUpperInvariant()}  ·  {group.Count()}",
                _small,
                YautjaClanHallStyle.Dim);
            header.Margin = new Thickness(2, 6, 0, 1);
            _roster.AddChild(header);
            foreach (var member in group)
                _roster.AddChild(HunterLine(member, highlight: member.PlayerId == viewer.PlayerId));
        }

        if (page.Members.Count == 0)
            _roster.AddChild(Empty(Loc.GetString("cmu-yautja-hall-roster-empty")));
    }

    private void BuildClanEmpty(YautjaClanHallEuiState state)
    {
        _clanEmpty.RemoveAllChildren();
        var message = state.Viewer.ClanId == null
            ? state.Viewer.Rank < YautjaRank.Blooded
                ? "cmu-yautja-hall-no-clan-young"
                : "cmu-yautja-hall-no-clan"
            : "cmu-yautja-hall-clan-hidden";
        _clanEmpty.AddChild(Callout(Loc.GetString(message), YautjaClanHallStyle.Muted));
        var boards = new HallButton(Loc.GetString("cmu-yautja-hall-see-boards"), _readout, YautjaClanHallStyle.HotRed)
        {
            HorizontalAlignment = HAlignment.Left,
        };
        boards.OnPressed += _ => ShowPage(YautjaClanHallPage.Leaderboard);
        _clanEmpty.AddChild(boards);
    }

    private void AddAction(BoxContainer row, string loc, Action action, Color? accent = null)
    {
        var button = new HallButton(Loc.GetString(loc), _small, accent ?? YautjaClanHallStyle.HotRed, 8, 3);
        button.OnPressed += _ => action();
        row.AddChild(button);
    }

    private Control TextBlock(string title, string text, Color accent)
    {
        var row = YautjaClanHallStyle.HBox(8);
        row.AddChild(new PanelContainer { MinWidth = 3, PanelOverride = YautjaClanHallStyle.Box(accent) });
        var box = YautjaClanHallStyle.VBox(3);
        box.HorizontalExpand = true;
        box.AddChild(YautjaClanHallStyle.Label(title.ToUpperInvariant(), _small, accent));
        box.AddChild(YautjaClanHallStyle.Paragraph(text, YautjaClanHallStyle.Text));
        row.AddChild(box);
        return row;
    }

    private Control Callout(string text, Color accent)
    {
        var panel = YautjaClanHallStyle.Panel(YautjaClanHallStyle.Card, accent, 10, 6);
        panel.AddChild(YautjaClanHallStyle.Paragraph(text, accent));
        return panel;
    }

    // the clan's sigil: its mask on a disc of the clan colour, with a thin ring. the mask icons only
    // use the middle of their 32px canvas, so draw that middle blown up instead of the whole thing
    private sealed class SigilBadge : Control
    {
        private static readonly UIBox2 MaskRegion = new(8, 8, 24, 24);
        private Texture? _mask;
        private Color _color = YautjaClanHallStyle.Border;

        public SigilBadge(float size)
        {
            MinSize = new Vector2(size, size);
            MouseFilter = MouseFilterMode.Ignore;
        }

        public void Set(string sigil, Color color)
        {
            _mask = YautjaClanHallStyle.Sigil(sigil);
            _color = color;
        }

        protected override void Draw(DrawingHandleScreen handle)
        {
            base.Draw(handle);
            var c = PixelSize / 2;
            var r = MathF.Min(PixelSize.X, PixelSize.Y) / 2;
            handle.DrawCircle(c, r, _color.WithAlpha(0.18f));
            handle.DrawCircle(c, r * 0.86f, YautjaClanHallStyle.Screen);
            handle.DrawCircle(c, r, _color, filled: false);
            handle.DrawCircle(c, r * 0.86f, _color.WithAlpha(0.5f), filled: false);
            if (_mask == null)
                return;

            var half = r * 0.72f;
            handle.DrawTextureRectRegion(_mask, new UIBox2(c.X - half, c.Y - half, c.X + half, c.Y + half), MaskRegion);
        }
    }

    private sealed class Swatch : ContainerButton
    {
        private readonly Color _color;

        public Swatch(Color color)
        {
            _color = color;
            MinSize = new Vector2(18, 18);
            VerticalAlignment = VAlignment.Center;
        }

        protected override void Draw(DrawingHandleScreen handle)
        {
            base.Draw(handle);
            var box = UIBox2.FromDimensions(Vector2.Zero, PixelSize);
            handle.DrawRect(box, _color);
            handle.DrawRect(box, DrawMode == DrawModeEnum.Hover ? YautjaClanHallStyle.Text : YautjaClanHallStyle.BezelShadow, filled: false);
        }
    }

    // inline editor for one clan text field
    private sealed class TextEditor : PanelContainer
    {
        private readonly YautjaClanHallWindow _window;
        private readonly YautjaClanHallText _field;
        private readonly TextEdit _edit;
        private readonly Label _count;
        private int _clanId;

        public TextEditor(YautjaClanHallWindow window, YautjaClanHallText field)
        {
            _window = window;
            _field = field;
            Visible = false;
            PanelOverride = YautjaClanHallStyle.Box(YautjaClanHallStyle.Card, YautjaClanHallStyle.HotRed, 1, 10, 8);

            var box = YautjaClanHallStyle.VBox(6);
            box.AddChild(window.SectionTitle(Loc.GetString(field switch
            {
                YautjaClanHallText.Notice => "cmu-yautja-hall-notice",
                YautjaClanHallText.EliteNotes => "cmu-yautja-hall-elite-notes",
                _ => "cmu-yautja-hall-description",
            })));
            _edit = new TextEdit { MinHeight = 110, HorizontalExpand = true };
            _edit.OnTextChanged += _ => UpdateCount();
            box.AddChild(_edit);

            var buttons = YautjaClanHallStyle.HBox(6);
            _count = YautjaClanHallStyle.Label(string.Empty, window._small, YautjaClanHallStyle.Dim);
            _count.VerticalAlignment = VAlignment.Center;
            buttons.AddChild(_count);
            buttons.AddChild(YautjaClanHallStyle.Gap());
            var cancel = new HallButton(Loc.GetString("cmu-yautja-hall-cancel"), window._readout, YautjaClanHallStyle.Dim);
            cancel.OnPressed += _ => Close();
            var save = new HallButton(Loc.GetString("cmu-yautja-hall-save"), window._readout, YautjaClanHallStyle.HotRed);
            save.OnPressed += _ =>
            {
                _window.Request(new YautjaClanHallEditTextMessage(_clanId, _field, Rope.Collapse(_edit.TextRope)));
                Close();
            };
            buttons.AddChild(cancel);
            buttons.AddChild(save);
            box.AddChild(buttons);
            AddChild(box);
        }

        public void Open(int clanId, string text)
        {
            _clanId = clanId;
            _edit.TextRope = new Rope.Leaf(text);
            UpdateCount();
            Visible = true;
            _edit.GrabKeyboardFocus();
        }

        public void Close()
        {
            Visible = false;
        }

        private void UpdateCount()
        {
            var length = Rope.Collapse(_edit.TextRope).Length;
            _count.Text = $"{length}/{YautjaClanHallPolicy.MaxTextLength}";
            _count.FontColorOverride = length > YautjaClanHallPolicy.MaxTextLength ? YautjaClanHallStyle.HotRed : YautjaClanHallStyle.Dim;
        }
    }
}
