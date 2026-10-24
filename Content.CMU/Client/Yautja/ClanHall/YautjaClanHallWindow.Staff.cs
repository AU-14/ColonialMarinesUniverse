using Content.Shared.CMU14.Yautja;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.CMU14.Yautja.ClanHall;

// staff only pages. the server leaves Whitelist/Round null for everyone else, so these tabs
// never even get data to show
public sealed partial class YautjaClanHallWindow
{
    private static readonly YautjaWhitelistFlags[] FlagOrder =
    [
        YautjaWhitelistFlags.Yautja,
        YautjaWhitelistFlags.Legacy,
        YautjaWhitelistFlags.Council,
        YautjaWhitelistFlags.CouncilLegacy,
        YautjaWhitelistFlags.Leader,
    ];

    private BoxContainer _whitelistRows = default!;
    private LineEdit _grantName = default!;
    private LineEdit _whitelistFilter = default!;

    private Label _roundInfo = default!;
    private Label _roundSlots = default!;
    private LineEdit _slotsEdit = default!;
    private HallButton _randomToggle = default!;
    private LineEdit _randomMin = default!;
    private LineEdit _randomMax = default!;
    private Label _randomInfo = default!;
    private bool _randomEnabled;
    private bool _roundFilled;

    private Control BuildWhitelistPage()
    {
        var page = YautjaClanHallStyle.VBox(8);
        page.HorizontalExpand = page.VerticalExpand = true;
        page.AddChild(SectionTitle(Loc.GetString("cmu-yautja-hall-tab-whitelists"), YautjaClanHallStyle.Staff));

        var grant = YautjaClanHallStyle.Panel(YautjaClanHallStyle.Card, YautjaClanHallStyle.Staff, 10, 6);
        var grantRow = YautjaClanHallStyle.HBox(6);
        _grantName = new LineEdit { HorizontalExpand = true, PlaceHolder = Loc.GetString("cmu-yautja-hall-grant-placeholder") };
        var grantButton = new HallButton(Loc.GetString("cmu-yautja-hall-grant"), _readout, YautjaClanHallStyle.Staff);
        grantButton.OnPressed += _ =>
        {
            if (string.IsNullOrWhiteSpace(_grantName.Text))
                return;

            Request(new YautjaClanHallSetWhitelistMessage(_grantName.Text.Trim(), YautjaWhitelistFlags.Yautja));
            _grantName.Text = string.Empty;
        };
        var openButton = new HallButton(Loc.GetString("cmu-yautja-hall-open-record"), _readout, YautjaClanHallStyle.Staff);
        openButton.OnPressed += _ =>
        {
            if (!string.IsNullOrWhiteSpace(_grantName.Text))
                Request(new YautjaClanHallFindMessage(_grantName.Text.Trim()));
        };
        grantRow.AddChild(_grantName);
        grantRow.AddChild(grantButton);
        grantRow.AddChild(openButton);
        grant.AddChild(grantRow);
        page.AddChild(grant);

        _whitelistFilter = new LineEdit { HorizontalExpand = true, PlaceHolder = Loc.GetString("cmu-yautja-hall-search") };
        _whitelistFilter.OnTextChanged += _ =>
        {
            if (_state != null)
                UpdateWhitelistPage(_state);
        };
        page.AddChild(_whitelistFilter);

        _whitelistRows = YautjaClanHallStyle.VBox(3);
        page.AddChild(Scroll(_whitelistRows));
        return page;
    }

    private void UpdateWhitelistPage(YautjaClanHallEuiState state)
    {
        _whitelistRows.RemoveAllChildren();
        if (state.Whitelist is not { } rows)
            return;

        var filter = _whitelistFilter.Text.Trim();
        foreach (var entry in rows)
        {
            if (filter.Length > 0 && !entry.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                continue;

            var row = YautjaClanHallStyle.HBox(8);
            var name = new HallButton(entry.Name, _bodyBold, YautjaClanHallStyle.Staff, 8, 2) { MinWidth = 160 };
            name.Label.HorizontalAlignment = HAlignment.Left;
            var id = entry.PlayerId;
            name.OnPressed += _ => OpenHunter(id);
            row.AddChild(name);
            row.AddChild(new Pip(entry.Online) { VerticalAlignment = VAlignment.Center });
            row.AddChild(WhitelistToggles(entry.PlayerId.ToString(), entry.Flags));
            _whitelistRows.AddChild(row);
        }

        if (_whitelistRows.ChildCount == 0)
            _whitelistRows.AddChild(Empty(Loc.GetString("cmu-yautja-hall-no-whitelist")));
    }

    // one chip per flag, each press flips just that flag. revoke wipes the lot (and the clan profile with it)
    private Control WhitelistToggles(string player, YautjaWhitelistFlags flags)
    {
        var row = YautjaClanHallStyle.HBox(4);
        row.HorizontalExpand = true;
        foreach (var flag in FlagOrder)
        {
            var chip = new HallButton(Loc.GetString($"cmu-yautja-hall-flag-{flag.ToString().ToLowerInvariant()}"), _small, YautjaClanHallStyle.Staff, 6, 2)
            {
                Selected = flags.HasFlag(flag),
            };
            var toggled = flags ^ flag;
            chip.OnPressed += _ => Request(new YautjaClanHallSetWhitelistMessage(player, toggled));
            row.AddChild(chip);
        }

        row.AddChild(YautjaClanHallStyle.Gap());
        if (flags != YautjaWhitelistFlags.None)
        {
            var revoke = new ConfirmButton(Loc.GetString("cmu-yautja-hall-revoke"), Loc.GetString("cmu-yautja-hall-revoke-confirm"), _small);
            revoke.Confirmed += () => Request(new YautjaClanHallSetWhitelistMessage(player, YautjaWhitelistFlags.None));
            row.AddChild(revoke);
        }

        return row;
    }

    private Control BuildRoundPage()
    {
        var page = YautjaClanHallStyle.VBox(10);
        page.HorizontalExpand = page.VerticalExpand = true;
        page.AddChild(SectionTitle(Loc.GetString("cmu-yautja-hall-tab-round"), YautjaClanHallStyle.Staff));

        var info = YautjaClanHallStyle.Panel(YautjaClanHallStyle.Card, YautjaClanHallStyle.MutedBorder, 12, 8);
        var infoBox = YautjaClanHallStyle.VBox(4);
        _roundInfo = YautjaClanHallStyle.Label(string.Empty, _readout, YautjaClanHallStyle.Text);
        _roundSlots = YautjaClanHallStyle.Label(string.Empty, _readout, YautjaClanHallStyle.Muted);
        infoBox.AddChild(_roundInfo);
        infoBox.AddChild(_roundSlots);
        info.AddChild(infoBox);
        page.AddChild(info);

        var hunt = YautjaClanHallStyle.HBox(6);
        var initialize = new ConfirmButton(Loc.GetString("cmu-yautja-hall-round-init"), Loc.GetString("cmu-yautja-hall-are-you-sure"), _readout)
        {
            Accent = YautjaClanHallStyle.Staff,
        };
        initialize.Confirmed += () => Request(new YautjaClanHallRoundInitializeMessage());
        hunt.AddChild(initialize);
        page.AddChild(hunt);

        page.AddChild(SectionTitle(Loc.GetString("cmu-yautja-hall-round-slots"), YautjaClanHallStyle.Staff));
        var slots = YautjaClanHallStyle.HBox(6);
        var minus = new HallButton("−", _heading, YautjaClanHallStyle.Staff, 10, 2);
        _slotsEdit = new LineEdit { MinWidth = 60 };
        var plus = new HallButton("+", _heading, YautjaClanHallStyle.Staff, 10, 2);
        minus.OnPressed += _ => BumpSlots(-1);
        plus.OnPressed += _ => BumpSlots(1);
        var applySlots = new HallButton(Loc.GetString("cmu-yautja-hall-apply"), _readout, YautjaClanHallStyle.Staff);
        applySlots.OnPressed += _ =>
        {
            if (int.TryParse(_slotsEdit.Text.Trim(), out var value))
                Request(new YautjaClanHallRoundSlotsMessage(value));
        };
        slots.AddChild(minus);
        slots.AddChild(_slotsEdit);
        slots.AddChild(plus);
        slots.AddChild(applySlots);
        page.AddChild(slots);

        page.AddChild(SectionTitle(Loc.GetString("cmu-yautja-hall-round-random"), YautjaClanHallStyle.Staff));
        var random = YautjaClanHallStyle.HBox(6);
        _randomToggle = new HallButton(string.Empty, _readout, YautjaClanHallStyle.Staff);
        _randomToggle.OnPressed += _ =>
        {
            _randomEnabled = !_randomEnabled;
            RefreshRandomToggle();
        };
        random.AddChild(_randomToggle);
        random.AddChild(YautjaClanHallStyle.Label(Loc.GetString("cmu-yautja-hall-round-every"), _readout, YautjaClanHallStyle.Muted));
        _randomMin = new LineEdit { MinWidth = 50 };
        _randomMax = new LineEdit { MinWidth = 50 };
        random.AddChild(_randomMin);
        random.AddChild(YautjaClanHallStyle.Label("–", _readout, YautjaClanHallStyle.Muted));
        random.AddChild(_randomMax);
        random.AddChild(YautjaClanHallStyle.Label(Loc.GetString("cmu-yautja-hall-round-rounds"), _readout, YautjaClanHallStyle.Muted));
        var applyRandom = new HallButton(Loc.GetString("cmu-yautja-hall-apply"), _readout, YautjaClanHallStyle.Staff);
        applyRandom.OnPressed += _ =>
        {
            if (int.TryParse(_randomMin.Text.Trim(), out var min) && int.TryParse(_randomMax.Text.Trim(), out var max))
                Request(new YautjaClanHallRoundRandomMessage(_randomEnabled, min, max));
        };
        random.AddChild(applyRandom);
        page.AddChild(random);
        _randomInfo = YautjaClanHallStyle.Label(string.Empty, _small, YautjaClanHallStyle.Dim);
        page.AddChild(_randomInfo);
        return page;
    }

    private void BumpSlots(int by)
    {
        if (!int.TryParse(_slotsEdit.Text.Trim(), out var value))
            value = 0;
        _slotsEdit.Text = Math.Max(0, value + by).ToString();
    }

    private void RefreshRandomToggle()
    {
        _randomToggle.Selected = _randomEnabled;
        _randomToggle.Text = Loc.GetString(_randomEnabled ? "cmu-yautja-hall-round-random-on" : "cmu-yautja-hall-round-random-off");
    }

    private void UpdateRoundPage(YautjaClanHallEuiState state)
    {
        if (state.Round is not { } round)
            return;

        _roundInfo.Text = Loc.GetString("cmu-yautja-hall-round-info",
            ("round", round.RoundId),
            ("active", round.RoundActive ? "true" : "false"),
            ("initialized", round.Initialized ? "true" : "false"));
        _roundSlots.Text = Loc.GetString("cmu-yautja-hall-round-slot-info",
            ("active", round.ActiveHunterSlots),
            ("configured", round.HunterSlots));
        _randomInfo.Text = round.RandomEnabled
            ? Loc.GetString("cmu-yautja-hall-round-random-left", ("rounds", round.RandomRoundsRemaining))
            : Loc.GetString("cmu-yautja-hall-round-random-idle");

        // fill the inputs once and after an apply lands, not on every refresh someone else causes
        if (!_roundFilled || _pendingAfter < 0 && !_slotsEdit.HasKeyboardFocus() && !_randomMin.HasKeyboardFocus() && !_randomMax.HasKeyboardFocus())
        {
            _roundFilled = true;
            _slotsEdit.Text = round.HunterSlots.ToString();
            _randomMin.Text = round.RandomMinimumRounds.ToString();
            _randomMax.Text = round.RandomMaximumRounds.ToString();
            _randomEnabled = round.RandomEnabled;
            RefreshRandomToggle();
        }
    }
}
