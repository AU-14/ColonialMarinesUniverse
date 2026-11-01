using System.Numerics;
using Content.Client.CMU14.Interface;
using Content.Shared.CMU14.Vehicle.Open;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.Vehicle;

/// <summary>
/// A vehicle's field manual: the vehicle on the left, turned to any facing and with its panels shut or
/// open; the index of its parts and pages in the middle; the page picked on the right. A part is
/// picked from the index or by clicking it on the vehicle. Drawn through the CRT pass when the
/// player has the CRT interface on.
/// </summary>
public sealed class CMUVehicleManualWindow : DefaultWindow
{
    private readonly Button[] _facings = new Button[4];
    private readonly Dictionary<string, Button> _index = new();
    private CMUVehicleManualPrototype _manual = default!;
    private CMUVehicleManualDiagram _diagram = default!;
    private Button _panels = default!;
    private Label _shown = default!;
    private Label _pageTitle = default!;
    private RichTextLabel _page = default!;
    private ScrollContainer _pageScroll = default!;

    public CMUVehicleManualWindow()
    {
        MinSize = new Vector2(880, 480);
        SetSize = new Vector2(900, 520);
    }

    /// <summary>
    /// Fills the window in from a manual. Called once, as it opens.
    /// </summary>
    public void Load(CMUVehicleManualPrototype manual)
    {
        _manual = manual;
        Title = Loc.GetString(manual.Title);

        _diagram = new CMUVehicleManualDiagram(manual)
        {
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        _diagram.PartClicked += Select;

        var facingRow = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
        for (var i = 0; i < _facings.Length; i++)
        {
            var facing = i;
            var direction = CMUVehicleManualDiagram.Facings[i].ToString().ToLowerInvariant();
            var button = new Button
            {
                Text = Loc.GetString($"cmu-vehicle-manual-facing-{direction}"),
                ToggleMode = true,
                HorizontalExpand = true,
            };
            button.OnPressed += _ => SetFacing(facing);
            _facings[i] = button;
            facingRow.AddChild(button);
        }

        _panels = new Button { ToggleMode = true, MinWidth = 96 };
        _panels.OnToggled += args => SetPanelsOpen(args.Pressed);
        facingRow.AddChild(_panels);

        _shown = new Label { FontColorOverride = CrtTerminalPalette.TextDim, ClipText = true };

        var left = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 4,
            MinWidth = 300,
        };
        left.AddChild(_diagram);
        left.AddChild(facingRow);
        left.AddChild(_shown);

        var indexList = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 2 };
        foreach (var entry in manual.Entries)
        {
            var id = entry.Id;
            var button = new Button
            {
                Text = Loc.GetString(entry.Name),
                ToggleMode = true,
                ClipText = true,
            };
            if (entry.Highlight)
                button.ModulateSelfOverride = CrtTerminalPalette.Caution;

            button.OnPressed += _ => Select(id);
            _index[id] = button;
            indexList.AddChild(button);
        }

        var index = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, MinWidth = 170, SeparationOverride = 4 };
        index.AddChild(new Label { Text = Loc.GetString("cmu-vehicle-manual-index"), FontColorOverride = CrtTerminalPalette.TextBright });
        index.AddChild(new ScrollContainer
        {
            VerticalExpand = true,
            HScrollEnabled = false,
            Children = { indexList },
        });

        _pageTitle = new Label { FontColorOverride = CrtTerminalPalette.TextBright };
        _page = new RichTextLabel { HorizontalExpand = true };
        _pageScroll = new ScrollContainer
        {
            VerticalExpand = true,
            HScrollEnabled = false,
            Children = { _page },
        };
        var page = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            MinWidth = 300,
            HorizontalExpand = true,
            SeparationOverride = 4,
        };
        page.AddChild(_pageTitle);
        page.AddChild(_pageScroll);

        var body = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 10,
            Margin = new Thickness(6),
        };
        body.AddChild(left);
        body.AddChild(index);
        body.AddChild(page);

        // The CRT pass redraws only its source, so the source carries its own dark ground: the
        // window's panel behind it isn't part of what's redrawn.
        var tube = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = CrtTerminalPalette.Void },
            Children = { body },
        };

        // A manual is read: the tube without its roll bar or grain crawling under the text.
        var screen = new Control { HorizontalExpand = true, VerticalExpand = true };
        screen.AddChild(tube);
        screen.AddChild(new CrtScreenControl { Source = tube, Roll = false, Grain = false });
        Contents.AddChild(screen);

        SetFacing(0);
        SetPanelsOpen(false);
        if (manual.Entries.Count > 0)
            Select(manual.Entries[0].Id);
    }

    private void SetFacing(int facing)
    {
        _diagram.Facing = facing;
        for (var i = 0; i < _facings.Length; i++)
        {
            _facings[i].Pressed = i == facing;
        }

        UpdateShown();
    }

    private void SetPanelsOpen(bool open)
    {
        _diagram.Open = open;
        _panels.Pressed = open;
        _panels.Text = Loc.GetString(open ? "cmu-vehicle-manual-panels-open" : "cmu-vehicle-manual-panels-shut");
        UpdateShown();
    }

    /// <summary>
    /// Says whether the part picked can be seen on the vehicle as it is shown.
    /// </summary>
    private void UpdateShown()
    {
        var entry = _manual.Entries.Find(e => e.Id == _diagram.Selected);
        if (entry == null || entry.Masks.Count == 0 && entry.OpenMasks is not { Count: > 0 })
        {
            _shown.Text = Loc.GetString("cmu-vehicle-manual-hint");
            return;
        }

        _shown.Text = Loc.GetString(_diagram.Shows(entry) ? "cmu-vehicle-manual-shown" : "cmu-vehicle-manual-not-shown");
    }

    private void Select(string id)
    {
        if (_manual.Entries.Find(e => e.Id == id) is not { } entry)
            return;

        _diagram.Selected = id;
        foreach (var (entryId, button) in _index)
        {
            button.Pressed = entryId == id;
        }

        _pageTitle.Text = Loc.GetString(entry.Name).ToUpperInvariant();
        var message = new FormattedMessage();
        Section(message, "cmu-vehicle-manual-about", entry.About);
        Section(message, "cmu-vehicle-manual-use", entry.Use);
        Section(message, "cmu-vehicle-manual-damage", entry.Damage);
        Section(message, "cmu-vehicle-manual-repair", entry.Repair);
        Section(message, "cmu-vehicle-manual-replace", entry.Replace);
        _page.SetMessage(message, defaultColor: CrtTerminalPalette.Text);
        _pageScroll.SetScrollValue(Vector2.Zero);
        UpdateShown();
    }

    private static void Section(FormattedMessage message, string heading, LocId? text)
    {
        if (text is not { } id)
            return;

        if (!message.IsEmpty)
        {
            message.PushNewline();
            message.PushNewline();
        }

        message.PushColor(CrtTerminalPalette.Accent);
        message.AddText(Loc.GetString(heading));
        message.Pop();
        message.PushNewline();
        message.AddMarkupPermissive(Loc.GetString(id));
    }
}
