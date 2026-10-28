using System.Linq;
using System.Numerics;
using Content.Shared.CMU14.Yautja;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.Yautja.ClanHall;

// clan tab: honor gauge and the clan's mark up top, its text blocks on the left, people on the right.
// each text block has its own edit button and edits in place
public sealed partial class YautjaClanHallWindow
{
    private BoxContainer _clanPage = default!;

    // editors outlive refreshes, so a state update from someone else doesn't eat what you're typing
    private readonly Dictionary<YautjaClanHallText, TextEdit> _textEdits = new();
    private YautjaClanHallText? _editingText;
    private bool _editingClan;
    private int? _shownClan;

    private LineEdit _editName = default!;
    private LineEdit _editColor = default!;
    private OptionButton _editMask = default!;
    private OptionButton _editMetal = default!;
    private LineEdit _editHonor = default!;

    private Control BuildClanPage()
    {
        _clanPage = VBox(12);

        foreach (var field in new[] { YautjaClanHallText.Notice, YautjaClanHallText.Description, YautjaClanHallText.EliteNotes })
            _textEdits[field] = new TextEdit { MinHeight = 90, HorizontalExpand = true };

        _editName = Hud.LineEdit();
        _editName.HorizontalExpand = true;
        _editColor = Hud.LineEdit();
        _editColor.MinWidth = 100;
        _editMask = new OptionButton { StyleBoxOverride = Hud.Field() };
        for (var style = 1; style <= YautjaClanSigils.Styles; style++)
            _editMask.AddItem(Loc.GetString("cmu-yautja-hall-mask-style", ("style", style)), style);
        _editMask.OnItemSelected += args => _editMask.SelectId(args.Id);
        _editMetal = new OptionButton { StyleBoxOverride = Hud.Field() };
        for (var i = 0; i < YautjaClanSigils.Materials.Length; i++)
            _editMetal.AddItem(Loc.GetString($"cmu-yautja-hall-material-{YautjaClanSigils.Materials[i]}"), i);
        _editMetal.OnItemSelected += args => _editMetal.SelectId(args.Id);
        _editHonor = Hud.LineEdit();
        _editHonor.MinWidth = 100;

        return _clanPage;
    }

    private void UpdateClanPage(YautjaClanHallEuiState state)
    {
        _clanPage.RemoveAllChildren();
        foreach (var edit in _textEdits.Values)
            edit.Orphan();
        foreach (var control in new Control[] { _editName, _editColor, _editMask, _editMetal, _editHonor })
            control.Orphan();

        if (state.Clan is not { } clan)
        {
            var frame = Frame(null);
            frame.Body.AddChild(Empty(state.Viewer.ClanId == null
                ? state.Viewer.Rank < YautjaRank.Blooded ? "cmu-yautja-hall-no-clan-young" : "cmu-yautja-hall-no-clan"
                : "cmu-yautja-hall-clan-hidden"));
            _clanPage.AddChild(frame);
            return;
        }

        if (_shownClan != clan.Id)
        {
            _shownClan = clan.Id;
            _editingText = null;
            _editingClan = false;
        }

        var color = Color.TryFromHex(clan.Color, out var parsed) ? parsed : Hud.Bright;
        var top = state.Clans.Count == 0 ? 1 : Math.Max(1, state.Clans.Max(c => c.Honor));

        // header: gauge, mark, name, tags, actions
        var header = HBox(18);
        var gauge = new HudGauge(190, _number, _small);
        gauge.Set(clan.Honor / (float) top, clan.Honor.ToString(),
            Loc.GetString("cmu-yautja-hall-honor").ToUpperInvariant(),
            Loc.GetString("cmu-yautja-hall-rank-of", ("standing", clan.Standing), ("clans", state.Clans.Count)).ToUpperInvariant());
        header.AddChild(gauge);

        var id = VBox(8);
        id.HorizontalExpand = true;
        id.VerticalAlignment = VAlignment.Center;
        var nameRow = HBox(12);
        nameRow.AddChild(new SigilRing(SigilTexture(clan.Sigil), color));
        var name = Hud.Write(clan.Name.ToUpperInvariant(), _big, Hud.Bright, clip: true);
        name.HorizontalExpand = true;
        name.VerticalAlignment = VAlignment.Center;
        nameRow.AddChild(name);
        id.AddChild(nameRow);

        var tags = HBox(6);
        tags.AddChild(new HudTag(Loc.GetString("cmu-yautja-hall-members-count", ("members", clan.Members.Count)), _small) { Fill = Hud.Label, Outline = true });
        var leader = clan.Members.FirstOrDefault(m => m.Rank == YautjaRank.Leader);
        if (leader != null)
            tags.AddChild(new HudTag(Loc.GetString("cmu-yautja-hall-leader-tag", ("name", leader.Name)), _small) { Fill = Hud.Label, Outline = true });
        if (clan.Id == state.Viewer.ClanId)
            tags.AddChild(new HudTag(Loc.GetString("cmu-yautja-hall-your-clan"), _small) { Fill = Hud.Hot });
        id.AddChild(tags);

        var actions = HBox(6);
        var viewer = state.Viewer;
        if (viewer.ClanId == null && viewer.CanPetition)
        {
            var clanId = clan.Id;
            actions.AddChild(viewer.PetitionClanId == clan.Id
                ? Button("cmu-yautja-hall-withdraw", () => Request(new YautjaClanHallPetitionMessage(null)))
                : Button("cmu-yautja-hall-petition", () => Request(new YautjaClanHallPetitionMessage(clanId))));
        }

        if (clan.CanEditPresentation && !_editingClan)
            actions.AddChild(Button("cmu-yautja-hall-edit-clan", () => StartClanEdit(clan)));
        if (actions.ChildCount > 0)
            id.AddChild(actions);

        if (viewer.PetitionClanId == clan.Id)
            id.AddChild(Hud.Write(Loc.GetString("cmu-yautja-hall-petition-pending"), _body, Hud.Muted));

        header.AddChild(id);
        _clanPage.AddChild(header);

        if (_editingClan)
            _clanPage.AddChild(ClanEditor(clan));

        // body: words left, people right
        var left = VBox(10);
        left.AddChild(TextBlock(clan, YautjaClanHallText.Notice, "cmu-yautja-hall-notice", clan.Notice, "cmu-yautja-hall-notice-empty", clan.CanEditNotice));
        left.AddChild(TextBlock(clan, YautjaClanHallText.Description, "cmu-yautja-hall-description", clan.Description, null, clan.CanEditPresentation));
        if (clan.EliteNotes != null)
            left.AddChild(TextBlock(clan, YautjaClanHallText.EliteNotes, "cmu-yautja-hall-elite-notes", clan.EliteNotes, "cmu-yautja-hall-notes-empty", clan.CanEditEliteNotes));

        var right = VBox(10);
        if (clan.Petitions is { Count: > 0 } petitions)
        {
            var requests = Frame("cmu-yautja-hall-petitions-title");
            foreach (var petitioner in petitions)
            {
                var row = HBox(4);
                var line = HunterRow(petitioner, compact: true);
                line.HorizontalExpand = true;
                row.AddChild(line);
                var target = petitioner.PlayerId;
                row.AddChild(Button("cmu-yautja-hall-accept", () => Request(new YautjaClanHallAnswerPetitionMessage(target, true))));
                row.AddChild(Button("cmu-yautja-hall-refuse", () => Request(new YautjaClanHallAnswerPetitionMessage(target, false))));
                requests.Body.AddChild(row);
            }

            right.AddChild(requests);
        }

        var members = Frame("cmu-yautja-hall-members");
        foreach (var group in clan.Members.GroupBy(member => member.Rank).OrderByDescending(group => group.Key))
        {
            var rank = Caption(RankName(group.Key));
            rank.Margin = new Thickness(0, 4, 0, 0);
            members.Body.AddChild(rank);
            foreach (var member in group)
                members.Body.AddChild(HunterRow(member));
        }

        if (clan.Members.Count == 0)
            members.Body.AddChild(Empty("cmu-yautja-hall-no-members"));

        right.AddChild(members);
        _clanPage.AddChild(new HudColumns(left, right, 360));
    }

    private Control TextBlock(YautjaClanHallClanPage clan, YautjaClanHallText field, string titleLoc, string text, string? emptyLoc, bool canEdit)
    {
        var frame = Frame(titleLoc);
        if (canEdit && _editingText != field)
        {
            frame.Header.AddChild(Gap());
            frame.Header.AddChild(Button("cmu-yautja-hall-edit", () =>
            {
                _editingText = field;
                _textEdits[field].TextRope = new Rope.Leaf(text);
                if (_state != null)
                    UpdateClanPage(_state);
            }));
        }

        if (_editingText == field)
        {
            var edit = _textEdits[field];
            var slot = new PanelContainer { PanelOverride = Hud.Field() };
            slot.AddChild(edit);
            frame.Body.AddChild(slot);
            var buttons = HBox(4);
            buttons.AddChild(Gap());
            buttons.AddChild(Button("cmu-yautja-hall-cancel", StopTextEdit));
            var clanId = clan.Id;
            buttons.AddChild(Button("cmu-yautja-hall-save", () =>
            {
                Request(new YautjaClanHallEditTextMessage(clanId, field, Rope.Collapse(edit.TextRope)));
                StopTextEdit();
            }));
            frame.Body.AddChild(buttons);
            return frame;
        }

        if (string.IsNullOrWhiteSpace(text) && emptyLoc != null)
            frame.Body.AddChild(Empty(emptyLoc));
        else
            frame.Body.AddChild(Hud.Paragraph(text, Hud.Text));

        return frame;
    }

    private void StopTextEdit()
    {
        _editingText = null;
        if (_state != null)
            UpdateClanPage(_state);
    }

    private void StartClanEdit(YautjaClanHallClanPage clan)
    {
        _editingClan = true;
        _editName.Text = clan.Name;
        _editName.Editable = clan.CanRename;
        _editColor.Text = clan.Color;
        if (YautjaClanSigils.TryParse(clan.Sigil, out var style, out var material))
        {
            _editMask.SelectId(style);
            _editMetal.SelectId(Array.IndexOf(YautjaClanSigils.Materials, material));
        }

        _editHonor.Text = clan.Honor.ToString();
        if (_state != null)
            UpdateClanPage(_state);
    }

    private void StopClanEdit()
    {
        _editingClan = false;
        if (_state != null)
            UpdateClanPage(_state);
    }

    private Control ClanEditor(YautjaClanHallClanPage clan)
    {
        var frame = Frame("cmu-yautja-hall-edit-clan");
        var grid = new GridContainer { Columns = 2, HSeparationOverride = 10, VSeparationOverride = 6 };
        grid.AddChild(Caption(Loc.GetString("cmu-yautja-hall-name")));
        grid.AddChild(_editName);
        grid.AddChild(Caption(Loc.GetString("cmu-yautja-hall-color")));
        grid.AddChild(_editColor);
        grid.AddChild(Caption(Loc.GetString("cmu-yautja-hall-mask")));
        var mask = HBox(4);
        mask.AddChild(_editMask);
        mask.AddChild(_editMetal);
        grid.AddChild(mask);
        frame.Body.AddChild(grid);

        var buttons = HBox(4);
        buttons.AddChild(Gap());
        buttons.AddChild(Button("cmu-yautja-hall-cancel", StopClanEdit));
        var id = clan.Id;
        buttons.AddChild(Button("cmu-yautja-hall-save", () =>
        {
            var material = YautjaClanSigils.Materials[Math.Clamp(_editMetal.SelectedId, 0, YautjaClanSigils.Materials.Length - 1)];
            Request(new YautjaClanHallEditLookMessage(id, _editName.Text, _editColor.Text.Trim(), YautjaClanSigils.Make(_editMask.SelectedId, material)));
            StopClanEdit();
        }));
        frame.Body.AddChild(buttons);

        if (clan.CanSetHonor || clan.CanDisband)
        {
            var council = HBox(6);
            council.Margin = new Thickness(0, 6, 0, 0);
            if (clan.CanSetHonor)
            {
                council.AddChild(Caption(Loc.GetString("cmu-yautja-hall-honor")));
                council.AddChild(_editHonor);
                council.AddChild(Button("cmu-yautja-hall-set", () =>
                {
                    if (int.TryParse(_editHonor.Text.Trim(), out var honor))
                        Request(new YautjaClanHallSetHonorMessage(id, honor));
                }));
            }

            council.AddChild(Gap());
            if (clan.CanDisband)
            {
                council.AddChild(Button("cmu-yautja-hall-disband", () =>
                {
                    Request(new YautjaClanHallDisbandMessage(id));
                    StopClanEdit();
                }, confirm: true));
            }

            frame.Body.AddChild(council);
        }

        return frame;
    }

    // the clan's mask in a dotted ring, its colour as the inner line. the mask icons only use the
    // middle of their 32px canvas (9-23 across all 100), so that middle is what gets drawn
    private sealed class SigilRing : Control
    {
        private static readonly UIBox2 MaskRegion = new(9, 9, 23, 23);
        private readonly Texture? _mask;
        private readonly Color _color;

        public SigilRing(Texture? mask, Color color, float size = 56)
        {
            _mask = mask;
            _color = color;
            MinSize = new Vector2(size, size);
            VerticalAlignment = VAlignment.Center;
        }

        protected override void Draw(DrawingHandleScreen handle)
        {
            var s = UIScale;
            var c = PixelSize / 2;
            var r = MathF.Min(PixelSize.X, PixelSize.Y) / 2 - 2 * s;
            for (var i = 0; i < 48; i++)
            {
                var a = i * MathF.Tau / 48;
                var p = c + new Vector2(MathF.Cos(a), MathF.Sin(a)) * r;
                handle.DrawRect(UIBox2.FromDimensions(p - new Vector2(0.75f * s), new Vector2(1.5f * s)), Hud.Line);
            }

            Hud.Ring(handle, c, r * 0.82f, _color);
            if (_mask == null)
                return;

            var half = r * 0.62f;
            handle.DrawTextureRectRegion(_mask, new UIBox2(c.X - half, c.Y - half, c.X + half, c.Y + half), MaskRegion);
        }
    }
}
