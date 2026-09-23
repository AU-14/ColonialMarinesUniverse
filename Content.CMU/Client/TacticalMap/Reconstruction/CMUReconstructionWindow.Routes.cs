using System.Linq;
using Content.Shared.CMU14.TacticalMap.Reconstruction;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.CMU14.TacticalMap.Reconstruction;

public sealed partial class CMUReconstructionWindow
{
    private readonly List<Button> _colors = [];

    private void InitializeRoutes()
    {
        MarkerText.OnTextChanged += _ =>
        {
            if (MarkerText.Text.Length > CMUReconRoutes.MaxTextLength)
                MarkerText.Text = MarkerText.Text[..CMUReconRoutes.MaxTextLength];
        };
        foreach (var ink in Enum.GetValues<CMUReconInk>())
        {
            var button = new Button
            {
                Text = "●", ToggleMode = true, HorizontalExpand = true,
                Modulate = CMUReconstructionControl.InkColor(ink),
                ToolTip = Loc.GetString($"cmu-recon-ink-{ink.ToString().ToLowerInvariant()}"),
                Pressed = ink == CMUReconInk.Yellow,
            };
            _colors.Add(button);
            Colors.AddChild(button);
            button.OnPressed += _ =>
            {
                View.FinishStroke();
                View.Ink = ink;
                foreach (var other in _colors) other.Pressed = other == button;
                Pencil.Pressed = true;
                PlaceText.Pressed = false;
                UpdateDrawingControls();
            };
        }
        Pencil.OnToggled += _ => { View.FinishStroke(); UpdateDrawingControls(); };
        PlaceText.OnToggled += args =>
        {
            View.FinishStroke();
            if (args.Pressed) Pencil.Pressed = false;
            UpdateDrawingControls();
        };
        Pencil.OnToggled += args => { if (args.Pressed) PlaceText.Pressed = false; UpdateDrawingControls(); };
        foreach (var width in new[] { 1, 3, 5, 8 })
            StrokeWidth.AddItem(Loc.GetString("cmu-recon-width-value", ("width", width)), width);
        StrokeWidth.SelectId(3);
        StrokeWidth.OnItemSelected += args => { View.FinishStroke(); StrokeWidth.SelectId(args.Id); View.StrokeWidth = args.Id; };
        View.OnStroke += (points, depth, ink) =>
        {
            if (_canOrder && !IsRefreshing && View.Scene is { } scene)
                View.Draft.Add(new CMUReconAnnotation(depth, points, ink, View.StrokeWidth));
            UpdateDrawingControls();
        };
        View.OnTextPoint += (point, depth) =>
        {
            if (_canOrder && !IsRefreshing && !string.IsNullOrWhiteSpace(MarkerText.Text))
                View.Draft.Add(new CMUReconAnnotation(depth, [point], View.Ink, View.StrokeWidth, MarkerText.Text.Trim()));
            UpdateDrawingControls();
        };
        Undo.OnPressed += _ =>
        {
            if (!_canOrder || IsRefreshing || View.Scene is not { } scene) return;
            View.Draft.Undo(View.Orders);
            UpdateDrawingControls();
        };
        Clear.OnPressed += _ => { View.Draft.Clear(View.Orders); UpdateDrawingControls(); };
        Send.OnPressed += _ =>
        {
            View.FinishStroke();
            if (_canOrder && !IsRefreshing && View.Scene is { } scene && View.Draft.Send(scene.Generation) is { } message)
                OnSend?.Invoke(message);
            UpdateDrawingControls();
        };
    }

    private void UpdateDrawingControls()
    {
        DrawingToolbar.Visible = DrawingSidebar.Visible = _canOrder && !IsRefreshing;
        var enabled = _canOrder && !IsRefreshing && !View.Draft.Sending;
        Pencil.Disabled = !enabled;
        PlaceText.Disabled = !enabled;
        MarkerText.Editable = enabled;
        StrokeWidth.Disabled = !enabled;
        foreach (var color in _colors) color.Disabled = !enabled;
        View.DrawingEnabled = enabled && Pencil.Pressed;
        View.TextEnabled = enabled && PlaceText.Pressed;
        if (!enabled) View.CancelStroke();
        Clear.Disabled = Undo.Disabled = !enabled || View.Draft.Additions.Count == 0 && !View.Orders.Any(o => !View.Draft.Removals.Contains(o.Id));
        Send.Disabled = !enabled || !View.Draft.Changed;
        Send.Text = Loc.GetString(View.Draft.Sending ? "cmu-recon-sending" : "cmu-recon-send");
        Controls.Text = Loc.GetString(View.TextEnabled ? "cmu-recon-text-controls" : View.DrawingEnabled ? "cmu-recon-pencil-controls" : "cmu-recon-controls");
    }
}
