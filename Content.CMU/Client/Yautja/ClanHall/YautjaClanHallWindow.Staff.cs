using Content.Shared.CMU14.Yautja;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.CMU14.Yautja.ClanHall;

// staff tabs. the server leaves Whitelist/Round null for everyone else, so these never get data
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
    private LineEdit _whitelistSearch = default!;

    private HudTag _roundTag = default!;
    private Label _roundInfo = default!;
    private LineEdit _slots = default!;
    private HudButton _randomToggle = default!;
    private LineEdit _randomMin = default!;
    private LineEdit _randomMax = default!;
    private Label _randomInfo = default!;
    private bool _randomEnabled;
    private bool _roundFilled;

    private Control BuildWhitelistPage()
    {
        var page = VBox(10);

        var grant = Frame("cmu-yautja-hall-grant-title");
        var row = HBox(6);
        _grantName = Hud.LineEdit(Loc.GetString("cmu-yautja-hall-player"));
        _grantName.HorizontalExpand = true;
        row.AddChild(_grantName);
        row.AddChild(Button("cmu-yautja-hall-grant", () =>
        {
            if (string.IsNullOrWhiteSpace(_grantName.Text))
                return;

            Request(new YautjaClanHallSetWhitelistMessage(_grantName.Text.Trim(), YautjaWhitelistFlags.Yautja));
            _grantName.Text = string.Empty;
        }));
        row.AddChild(Button("cmu-yautja-hall-open", () =>
        {
            if (!string.IsNullOrWhiteSpace(_grantName.Text))
                Request(new YautjaClanHallFindMessage(_grantName.Text.Trim()));
        }));
        grant.Body.AddChild(row);
        page.AddChild(grant);

        var list = Frame("cmu-yautja-hall-tab-whitelists");
        _whitelistSearch = Hud.LineEdit(Loc.GetString("cmu-yautja-hall-search"));
        _whitelistSearch.OnTextChanged += _ =>
        {
            if (_state != null)
                UpdateWhitelistPage(_state);
        };
        list.Body.AddChild(_whitelistSearch);
        _whitelistRows = VBox(2);
        list.Body.AddChild(_whitelistRows);
        page.AddChild(list);
        return page;
    }

    private void UpdateWhitelistPage(YautjaClanHallEuiState state)
    {
        _whitelistRows.RemoveAllChildren();
        if (state.Whitelist is not { } rows)
            return;

        var search = _whitelistSearch.Text.Trim();
        foreach (var entry in rows)
        {
            if (search.Length > 0 && !entry.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
                continue;

            var line = HBox(8);
            var name = new HudRow();
            name.MinWidth = 170;
            name.HorizontalExpand = false;
            var label = Hud.Write(entry.Name, _bodyBold, Hud.Text, clip: true);
            label.Margin = new Thickness(8, 3);
            name.AddChild(label);
            var id = entry.PlayerId;
            name.OnPressed += _ => OpenHunter(id);
            line.AddChild(name);
            line.AddChild(WhitelistToggles(entry.PlayerId.ToString(), entry.Flags));
            _whitelistRows.AddChild(line);
        }

        if (_whitelistRows.ChildCount == 0)
            _whitelistRows.AddChild(Empty("cmu-yautja-hall-no-whitelist"));
    }

    // one lit toggle per flag, each press flips just that flag. remove clears all of them, and the
    // server wipes the clan profile with it
    private Control WhitelistToggles(string player, YautjaWhitelistFlags flags)
    {
        var row = HBox(4);
        row.HorizontalExpand = true;
        foreach (var flag in FlagOrder)
        {
            var toggle = new HudButton(Loc.GetString($"cmu-yautja-hall-flag-{flag.ToString().ToLowerInvariant()}"), _small)
            {
                Selected = flags.HasFlag(flag),
            };
            var toggled = flags ^ flag;
            toggle.Activated += () => Request(new YautjaClanHallSetWhitelistMessage(player, toggled));
            row.AddChild(toggle);
        }

        row.AddChild(Gap());
        if (flags != YautjaWhitelistFlags.None)
            row.AddChild(Button("cmu-yautja-hall-revoke", () => Request(new YautjaClanHallSetWhitelistMessage(player, YautjaWhitelistFlags.None)), confirm: true));

        return row;
    }

    private Control BuildRoundPage()
    {
        var page = VBox(10);

        var hunt = Frame("cmu-yautja-hall-hunt");
        var status = HBox(8);
        _roundTag = new HudTag(string.Empty, _small);
        status.AddChild(_roundTag);
        _roundInfo = Hud.Write(string.Empty, _body, Hud.Text);
        _roundInfo.VerticalAlignment = VAlignment.Center;
        status.AddChild(_roundInfo);
        status.AddChild(Gap());
        status.AddChild(Button("cmu-yautja-hall-round-init", () => Request(new YautjaClanHallRoundInitializeMessage()), confirm: true));
        hunt.Body.AddChild(status);
        page.AddChild(hunt);

        var settings = Frame("cmu-yautja-hall-settings");
        var slots = HBox(6);
        slots.AddChild(Caption(Loc.GetString("cmu-yautja-hall-round-slots")));
        slots.AddChild(Button("cmu-yautja-hall-minus", () => BumpSlots(-1)));
        _slots = Hud.LineEdit();
        _slots.MinWidth = 60;
        slots.AddChild(_slots);
        slots.AddChild(Button("cmu-yautja-hall-plus", () => BumpSlots(1)));
        slots.AddChild(Button("cmu-yautja-hall-set", () =>
        {
            if (int.TryParse(_slots.Text.Trim(), out var value))
                Request(new YautjaClanHallRoundSlotsMessage(value));
        }));
        settings.Body.AddChild(slots);

        var random = HBox(6);
        _randomToggle = new HudButton(Loc.GetString("cmu-yautja-hall-round-random"), _small);
        _randomToggle.Activated += () =>
        {
            _randomEnabled = !_randomEnabled;
            _randomToggle.Selected = _randomEnabled;
        };
        random.AddChild(_randomToggle);
        random.AddChild(Caption(Loc.GetString("cmu-yautja-hall-round-every")));
        _randomMin = Hud.LineEdit();
        _randomMin.MinWidth = 50;
        random.AddChild(_randomMin);
        random.AddChild(Caption("-"));
        _randomMax = Hud.LineEdit();
        _randomMax.MinWidth = 50;
        random.AddChild(_randomMax);
        random.AddChild(Caption(Loc.GetString("cmu-yautja-hall-round-rounds")));
        random.AddChild(Button("cmu-yautja-hall-set", () =>
        {
            if (int.TryParse(_randomMin.Text.Trim(), out var min) && int.TryParse(_randomMax.Text.Trim(), out var max))
                Request(new YautjaClanHallRoundRandomMessage(_randomEnabled, min, max));
        }));
        settings.Body.AddChild(random);
        _randomInfo = Hud.Write(string.Empty, _body, Hud.Muted);
        settings.Body.AddChild(_randomInfo);
        page.AddChild(settings);
        return page;
    }

    private void BumpSlots(int by)
    {
        if (!int.TryParse(_slots.Text.Trim(), out var value))
            value = 0;
        _slots.Text = Math.Max(0, value + by).ToString();
    }

    private void UpdateRoundPage(YautjaClanHallEuiState state)
    {
        if (state.Round is not { } round)
            return;

        _roundTag.Text = Loc.GetString(round.Initialized ? "cmu-yautja-hall-hunt-on" : "cmu-yautja-hall-hunt-off");
        _roundTag.Fill = round.Initialized ? Hud.Hot : Hud.Muted;
        _roundTag.Outline = !round.Initialized;
        _roundInfo.Text = Loc.GetString("cmu-yautja-hall-round-info",
            ("round", round.RoundId),
            ("active", round.RoundActive ? "true" : "false"),
            ("initialized", round.Initialized ? "true" : "false"),
            ("slots", round.ActiveHunterSlots));
        _randomInfo.Text = round.RandomEnabled
            ? Loc.GetString("cmu-yautja-hall-round-random-left", ("rounds", round.RandomRoundsRemaining))
            : string.Empty;

        // fill the inputs once, not on every refresh someone else causes
        if (_roundFilled)
            return;

        _roundFilled = true;
        _slots.Text = round.HunterSlots.ToString();
        _randomMin.Text = round.RandomMinimumRounds.ToString();
        _randomMax.Text = round.RandomMaximumRounds.ToString();
        _randomEnabled = round.RandomEnabled;
        _randomToggle.Selected = _randomEnabled;
    }
}
