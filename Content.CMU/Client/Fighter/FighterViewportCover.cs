using Content.Client.UserInterface.Controls;
using Content.Client.Viewport;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.Fighter;

/// <summary>Uses the normal, sharp world view on the ground and suspends it while the air view is open.</summary>
public sealed class FighterViewportCover : IDisposable
{
    private Control? _screen;
    private ScalingViewport? _viewport;
    private bool _visible;
    private bool _blur;

    public void Update(Control screen, bool covered)
    {
        if (_screen != screen)
        {
            Dispose();
            _screen = screen;
            _viewport = FindViewport(screen);
            if (_viewport != null)
            {
                _visible = _viewport.Visible;
                _blur = _viewport.ApplyZLevelBlur;
            }
        }
        if (_viewport == null) return;
        _viewport.Visible = !covered && _visible;
        _viewport.ApplyZLevelBlur = false;
    }

    public void Dispose()
    {
        if (_viewport is { Disposed: false })
        {
            _viewport.Visible = _visible;
            _viewport.ApplyZLevelBlur = _blur;
        }
        _viewport = null;
        _screen = null;
    }

    private static ScalingViewport? FindViewport(Control control)
    {
        if (control is MainViewport world) return world.Viewport;
        foreach (var child in control.Children)
            if (FindViewport(child) is { } viewport) return viewport;
        return null;
    }
}
