using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.Lobby;

/// <summary>A recessed, outlined input that stays visible inside tinted sheets.</summary>
public sealed class CmuInputStyleBox(Control input) : StyleBox
{
    private readonly StyleBoxFlat _box = new();
    private Color? _lastAccent;
    private Color? _lastSurface;
    private bool _lastFocus;

    protected override void DoDraw(DrawingHandleScreen handle, UIBox2 box, float uiScale)
    {
        var accent = CmuLobbyPresentation.Accent;
        var surface = CmuLobbyPresentation.Surface;
        if (_lastAccent != accent || _lastSurface != surface || _lastFocus != input.HasKeyboardFocus())
        {
            _lastAccent = accent;
            _lastSurface = surface;
            _lastFocus = input.HasKeyboardFocus();
            _box.BackgroundColor = Color.InterpolateBetween(surface, Color.Black, 0.65f);
            _box.BorderColor = CmuLobbyPresentation.ReadableColor(
                Color.InterpolateBetween(CmuLobbyPresentation.Muted, accent, _lastFocus ? 1f : 0.5f), _box.BackgroundColor, 4.5f);
            _box.BorderThickness = new Thickness(_lastFocus ? 2 : 1);
        }
        _box.Draw(handle, box, uiScale);
    }
}
