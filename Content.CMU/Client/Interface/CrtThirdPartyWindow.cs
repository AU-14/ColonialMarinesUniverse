using System.Linq;
using System.Numerics;
using Content.Client.CMU14.Interface;
using Content.Client.UserInterface.Controls;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Interface;

/// <summary>
///     A third-party support menu, in the CRT theme, shared by every console that can call in outside help: one card
///     per party with its cost and whether it's already been called, and a confirm button for the picked one.
/// </summary>
public sealed class CrtThirdPartyWindow : DefaultWindow
{
    public event Action<string>? OnCallThirdParty;

    [Dependency] private IGameTiming _timing = default!;

    private readonly CrtConsoleKit _kit = new();

    private readonly Label _subheading;
    private readonly PanelContainer _callsTag;
    private readonly Label _callsText;
    private readonly BoxContainer _list;
    private readonly Label _selectedName;
    private readonly Label _selectedCost;
    private readonly Label _limit;
    private readonly ConfirmButton _call;
    private readonly Label _footer;

    private SupportState? _state;

    /// <summary>What the menu shows: the budget it's paid from and who can be called.</summary>
    private sealed record SupportState(
        float Budget,
        Dictionary<string, (string DisplayName, float Cost)> CallableParties,
        HashSet<string> CalledParties,
        int CallsLeft,
        TimeSpan NextCallInAt);
    private string? _selected;
    private bool _requested;

    public CrtThirdPartyWindow()
    {
        IoCManager.InjectDependencies(this);

        Title = Loc.GetString("ambassador-third-party-title");
        MinSize = new Vector2(480, 500);
        SetSize = new Vector2(520, 600);

        _callsTag = _kit.Tag(out _callsText);
        _kit.Root.AddChild(_kit.Header(Loc.GetString("cmu-third-party-heading"), out _subheading, _callsTag));

        var scroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        _list = CrtConsoleKit.Column(6);
        _list.HorizontalExpand = true;
        _list.Margin = new Thickness(0, 0, 6, 0);
        scroll.AddChild(_list);
        _kit.Root.AddChild(scroll);

        // The pick and the button that sends for them.
        var request = CrtConsoleKit.Strip(CrtTerminalPalette.Surface2);
        var requestRow = CrtConsoleKit.Row();
        var info = CrtConsoleKit.Column(0);
        info.HorizontalExpand = true;
        _selectedName = CrtConsoleKit.Text(string.Empty, _kit.Bold, CrtTerminalPalette.TextBright);
        _selectedName.ClipText = true;
        info.AddChild(_selectedName);
        _selectedCost = CrtConsoleKit.Text(string.Empty, _kit.Small, CrtTerminalPalette.Caution);
        _selectedCost.ClipText = true;
        info.AddChild(_selectedCost);
        requestRow.AddChild(info);
        _call = new ConfirmButton
        {
            Text = Loc.GetString("cmu-third-party-request"),
            ConfirmationText = Loc.GetString("cmu-third-party-confirm"),
            MinWidth = 170,
            VerticalAlignment = VAlignment.Center,
        };
        CmuButtonStyles.Apply(_call, CmuButtonStyles.Variant.Affirm);
        _call.Label.FontOverride = _kit.Bold;
        _call.OnPressed += _ =>
        {
            if (_selected is { } id && CanCall(id))
            {
                _requested = true;
                OnCallThirdParty?.Invoke(id);
                Refresh();
            }
        };
        requestRow.AddChild(_call);
        request.AddChild(requestRow);
        _kit.Root.AddChild(request);

        _limit = CrtConsoleKit.Text(string.Empty, _kit.Small, CrtTerminalPalette.TextDim);
        _limit.ClipText = true;
        _kit.Root.AddChild(_limit);

        _kit.Root.AddChild(_kit.Footer(out _footer));
        _footer.Text = Loc.GetString("cmu-third-party-footer");

        Contents.AddChild(_kit.Tube);
    }

    public void UpdateState(
        float budget,
        Dictionary<string, (string DisplayName, float Cost)> callableParties,
        HashSet<string> calledParties,
        int callsLeft,
        TimeSpan nextCallInAt)
    {
        var s = new SupportState(budget, callableParties, calledParties, callsLeft, nextCallInAt);
        _state = s;
        _requested = false;

        if (_selected == null || !s.CallableParties.ContainsKey(_selected))
            _selected = s.CallableParties.Keys.FirstOrDefault(id => !s.CalledParties.Contains(id));

        Refresh();
    }

    private void Refresh()
    {
        if (_state is not { } s)
            return;

        _subheading.Text = Loc.GetString("cmu-third-party-subheading", ("budget", CrtConsoleKit.Money(s.Budget)));

        var cooling = s.CallsLeft > 0 && _timing.CurTime < s.NextCallInAt;
        CrtConsoleKit.SetTag(_callsTag, _callsText,
            Loc.GetString("cmu-third-party-calls", ("left", Math.Max(0, s.CallsLeft))),
            s.CallsLeft <= 0 ? CrtTerminalPalette.Alert : cooling ? CrtTerminalPalette.Caution : CrtTerminalPalette.Accent);

        if (s.CallsLeft <= 0)
        {
            _limit.Text = Loc.GetString("ambassador-third-party-no-calls-left");
        }
        else
        {
            var wait = s.NextCallInAt - _timing.CurTime;
            _limit.Text = wait > TimeSpan.Zero
                ? Loc.GetString("ambassador-third-party-cooldown", ("left", s.CallsLeft), ("minutes", (int) Math.Ceiling(wait.TotalMinutes)))
                : Loc.GetString("ambassador-third-party-calls-left", ("left", s.CallsLeft));
        }

        _list.RemoveAllChildren();
        if (s.CallableParties.Count == 0)
        {
            var none = CrtConsoleKit.Text(Loc.GetString("ambassador-third-party-none"), _kit.Bold, CrtTerminalPalette.TextDim);
            none.HorizontalAlignment = HAlignment.Center;
            none.Margin = new Thickness(0, 24, 0, 0);
            _list.AddChild(none);
        }

        foreach (var (id, party) in s.CallableParties.OrderBy(p => p.Value.Cost))
            _list.AddChild(Card(id, party.DisplayName, party.Cost, s));

        if (_selected is { } selected && s.CallableParties.TryGetValue(selected, out var pick))
        {
            _selectedName.Text = pick.DisplayName.ToUpperInvariant();
            _selectedCost.Text = Loc.GetString("cmu-third-party-cost", ("cost", CrtConsoleKit.Money(pick.Cost)));
        }
        else
        {
            _selectedName.Text = Loc.GetString("cmu-third-party-pick");
            _selectedCost.Text = string.Empty;
        }

        _call.Disabled = _requested || _selected is not { } chosen || !CanCall(chosen);
        _call.Text = Loc.GetString(_requested ? "ambassador-third-party-requesting" : "cmu-third-party-request");
    }

    private bool CanCall(string id)
    {
        return _state is { } s &&
               s.CallableParties.ContainsKey(id) &&
               !s.CalledParties.Contains(id) &&
               s.CallsLeft > 0 &&
               _timing.CurTime >= s.NextCallInAt;
    }

    private Control Card(string id, string name, float cost, SupportState s)
    {
        var called = s.CalledParties.Contains(id);
        var affordable = cost <= s.Budget;
        var selected = id == _selected;

        var button = new ContainerButton { HorizontalExpand = true, Disabled = called };
        var panel = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = selected ? CrtTerminalPalette.Surface4 : CrtTerminalPalette.Surface2 },
        };
        var row = CrtConsoleKit.Row();
        row.AddChild(new PanelContainer
        {
            MinWidth = 4,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = selected ? CrtTerminalPalette.Accent : called ? CrtTerminalPalette.Line : CrtTerminalPalette.Accent.WithAlpha(0.45f),
            },
        });

        var column = CrtConsoleKit.Column(2);
        column.HorizontalExpand = true;
        column.Margin = new Thickness(0, 6);
        var title = CrtConsoleKit.Text(name.ToUpperInvariant(), _kit.Bold, called ? CrtTerminalPalette.TextDim : CrtTerminalPalette.TextBright);
        title.ClipText = true;
        column.AddChild(title);
        var price = CrtConsoleKit.Text(CrtConsoleKit.Money(cost), _kit.Small, affordable ? CrtTerminalPalette.Caution : CrtTerminalPalette.Alert);
        column.AddChild(price);
        row.AddChild(column);

        if (called)
        {
            var chip = _kit.Chip(Loc.GetString("cmu-third-party-called"), CrtTerminalPalette.TextDim);
            chip.Margin = new Thickness(0, 0, 8, 0);
            row.AddChild(chip);
        }
        else if (!affordable)
        {
            var chip = _kit.Chip(Loc.GetString("cmu-third-party-short"), CrtTerminalPalette.Alert);
            chip.Margin = new Thickness(0, 0, 8, 0);
            row.AddChild(chip);
        }

        panel.AddChild(row);
        button.AddChild(panel);

        if (!selected && !called)
        {
            button.OnMouseEntered += _ => panel.PanelOverride = new StyleBoxFlat { BackgroundColor = CrtTerminalPalette.Surface3 };
            button.OnMouseExited += _ => panel.PanelOverride = new StyleBoxFlat { BackgroundColor = CrtTerminalPalette.Surface2 };
        }

        button.OnPressed += _ =>
        {
            _selected = id;
            _call.IsConfirming = false;
            Refresh();
        };
        return button;
    }
}
