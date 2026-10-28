using System.Linq;
using System.Numerics;
using Content.Client.CMU14.Interface;
using Content.Shared.Fax;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.CMU14.Fax;

/// <summary>
///     The fax machine in the CRT theme. The destination is picked from a searchable list instead of a dropdown, so
///     long fax names wrap rather than being cut off, and the paper tray, paper type, copy and send sit below it.
/// </summary>
public sealed class CMUFaxWindow : DefaultWindow
{
    public event Action? FileButtonPressed;
    public event Action? CopyButtonPressed;
    public event Action? SendButtonPressed;
    public event Action? RefreshButtonPressed;
    public event Action<string>? PeerSelected;

    public bool OfficePaper;

    private readonly CrtConsoleKit _kit = new();

    private readonly Label _subheading;
    private readonly PanelContainer _trayTag;
    private readonly Label _trayText;
    private readonly BoxContainer _from;
    private readonly LineEdit _search;
    private readonly BoxContainer _peers;
    private readonly BoxContainer _to;
    private readonly Button _paper;
    private readonly Button _copy;
    private readonly Button _send;
    private readonly Label _footer;

    private FaxUiState? _state;

    public CMUFaxWindow()
    {
        Title = Loc.GetString("fax-machine-ui-window");
        MinSize = new Vector2(420, 460);
        SetSize = new Vector2(480, 580);

        _trayTag = _kit.Tag(out _trayText);
        _kit.Root.AddChild(_kit.Header(Loc.GetString("cmu-ui-fax-heading"), out _subheading, _trayTag));

        // Where it's sent from and to.
        var route = _kit.Section(Loc.GetString("cmu-ui-fax-route"), out var routeColumn);
        _from = CrtConsoleKit.Column(0);
        routeColumn.AddChild(_from);
        _to = CrtConsoleKit.Column(0);
        routeColumn.AddChild(_to);
        _kit.Root.AddChild(route);

        // The destination list.
        var refresh = _kit.Button(Loc.GetString("fax-machine-ui-refresh-button"));
        refresh.OnPressed += _ => RefreshButtonPressed?.Invoke();
        var destinations = _kit.Section(Loc.GetString("cmu-ui-fax-destinations"), out var destColumn, null, refresh);
        destinations.VerticalExpand = true;
        _search = new LineEdit
        {
            HorizontalExpand = true,
            PlaceHolder = Loc.GetString("cmu-ui-fax-search"),
        };
        _search.OnTextChanged += _ => BuildPeers();
        destColumn.AddChild(_search);
        var scroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        _peers = CrtConsoleKit.Column(4);
        _peers.HorizontalExpand = true;
        _peers.Margin = new Thickness(0, 0, 6, 0);
        scroll.AddChild(_peers);
        destColumn.AddChild(scroll);
        _kit.Root.AddChild(destinations);

        // Paper type and printing a file.
        var paperRow = CrtConsoleKit.Row(6);
        _paper = _kit.Button(Loc.GetString("fax-machine-ui-paper-button-normal"));
        _paper.HorizontalExpand = true;
        _paper.OnPressed += _ => TogglePaper();
        paperRow.AddChild(_paper);
        var file = _kit.Button(Loc.GetString("fax-machine-ui-file-button"));
        file.HorizontalExpand = true;
        file.OnPressed += _ => FileButtonPressed?.Invoke();
        paperRow.AddChild(file);
        _kit.Root.AddChild(paperRow);

        // Copy and send.
        var actions = CrtConsoleKit.Strip(CrtTerminalPalette.Surface2);
        var actionRow = CrtConsoleKit.Row(6);
        _copy = _kit.Button(Loc.GetString("fax-machine-ui-copy-button"));
        _copy.HorizontalExpand = true;
        _copy.Disabled = true;
        _copy.OnPressed += _ => CopyButtonPressed?.Invoke();
        actionRow.AddChild(_copy);
        _send = _kit.Button(Loc.GetString("fax-machine-ui-send-button"), CmuButtonStyles.Variant.Affirm);
        _send.HorizontalExpand = true;
        _send.Disabled = true;
        _send.Label.FontOverride = _kit.Bold;
        _send.OnPressed += _ => SendButtonPressed?.Invoke();
        actionRow.AddChild(_send);
        actions.AddChild(actionRow);
        _kit.Root.AddChild(actions);

        _kit.Root.AddChild(_kit.Footer(out _footer));
        _footer.Text = Loc.GetString("cmu-ui-fax-footer");

        Contents.AddChild(_kit.Tube);
    }

    public void UpdateState(FaxUiState state)
    {
        _state = state;

        // There must always be a destination picked.
        if (string.IsNullOrEmpty(state.DestinationAddress) && state.AvailablePeers.Count != 0)
            PeerSelected?.Invoke(state.AvailablePeers.OrderBy(p => p.Value).First().Key);

        _subheading.Text = Loc.GetString("cmu-ui-fax-subheading", ("count", state.AvailablePeers.Count));
        CrtConsoleKit.SetTag(_trayTag, _trayText,
            Loc.GetString(state.IsPaperInserted ? "cmu-ui-fax-tray-loaded" : "cmu-ui-fax-tray-empty"),
            state.IsPaperInserted ? CrtTerminalPalette.Accent : CrtTerminalPalette.Alert);

        _from.RemoveAllChildren();
        _from.AddChild(_kit.Field(Loc.GetString("cmu-ui-fax-from"), state.DeviceName, CrtTerminalPalette.TextBright, 70));

        _to.RemoveAllChildren();
        var destination = state.DestinationAddress != null && state.AvailablePeers.TryGetValue(state.DestinationAddress, out var name)
            ? name
            : Loc.GetString("fax-machine-ui-no-peers");
        _to.AddChild(_kit.Field(Loc.GetString("cmu-ui-fax-to"), destination,
            state.DestinationAddress != null ? CrtTerminalPalette.Accent : CrtTerminalPalette.TextDim, 70));

        _copy.Disabled = !state.CanCopy;
        _send.Disabled = !state.CanSend;

        BuildPeers();
    }

    private void BuildPeers()
    {
        _peers.RemoveAllChildren();
        if (_state is not { } state)
            return;

        if (state.AvailablePeers.Count == 0)
        {
            _peers.AddChild(CrtConsoleKit.Wrapping(Loc.GetString("cmu-ui-fax-no-destinations"), CrtTerminalPalette.TextDim));
            return;
        }

        var filter = _search.Text.Trim();
        var shown = 0;
        foreach (var (address, name) in state.AvailablePeers.OrderBy(p => p.Value))
        {
            if (filter.Length > 0 && !name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                continue;

            _peers.AddChild(PeerCard(address, name, address == state.DestinationAddress));
            shown++;
        }

        if (shown == 0)
            _peers.AddChild(CrtConsoleKit.Wrapping(Loc.GetString("cmu-ui-fax-no-match"), CrtTerminalPalette.TextDim));
    }

    private Control PeerCard(string address, string name, bool selected)
    {
        var button = new ContainerButton { HorizontalExpand = true };
        var panel = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = selected ? CrtTerminalPalette.Surface4 : CrtTerminalPalette.Surface2 },
        };
        var row = CrtConsoleKit.Row(8);
        row.AddChild(new PanelContainer
        {
            MinWidth = 4,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = selected ? CrtTerminalPalette.Accent : CrtTerminalPalette.Accent.WithAlpha(0.35f),
            },
        });
        var label = CrtConsoleKit.BoldWrapping(name.ToUpperInvariant(),
            selected ? CrtTerminalPalette.TextBright : CrtTerminalPalette.Text);
        label.Margin = new Thickness(0, 5, 6, 5);
        row.AddChild(label);
        if (selected)
        {
            var chip = _kit.Chip(Loc.GetString("cmu-ui-fax-selected"), CrtTerminalPalette.Accent);
            chip.Margin = new Thickness(0, 0, 6, 0);
            row.AddChild(chip);
        }

        panel.AddChild(row);
        button.AddChild(panel);

        if (!selected)
        {
            button.OnMouseEntered += _ => panel.PanelOverride = new StyleBoxFlat { BackgroundColor = CrtTerminalPalette.Surface3 };
            button.OnMouseExited += _ => panel.PanelOverride = new StyleBoxFlat { BackgroundColor = CrtTerminalPalette.Surface2 };
            button.OnPressed += _ => PeerSelected?.Invoke(address);
        }

        return button;
    }

    private void TogglePaper()
    {
        OfficePaper = !OfficePaper;
        _paper.Text = Loc.GetString(OfficePaper ? "fax-machine-ui-paper-button-office" : "fax-machine-ui-paper-button-normal");
    }
}
