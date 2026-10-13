using System.Numerics;
using Content.Client.CMU14.Interface;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.Overwatch;

/// <summary>
/// A small health bar for the overwatch roster: full when unhurt, empty at the incapacitation
/// threshold. Green, then amber below 60%, then red below 25%.
/// </summary>
public sealed class CMUOverwatchHealthBar : Control
{
    private float? _health;
    private bool _dead;

    public CMUOverwatchHealthBar()
    {
        MinSize = new Vector2(64, 10);
        VerticalAlignment = VAlignment.Center;
    }

    public void SetHealth(float? health, bool dead)
    {
        _health = health;
        _dead = dead;
    }

    public static Color ColorFor(float health)
    {
        return health switch
        {
            >= 0.6f => CrtTerminalPalette.Accent,
            >= 0.25f => CrtTerminalPalette.Caution,
            _ => CrtTerminalPalette.Alert,
        };
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var box = PixelSizeBox;
        handle.DrawRect(box, CrtTerminalPalette.Surface2);

        if (!_dead && _health is { } health && health > 0)
        {
            var fill = box;
            fill.Right = box.Left + (int) MathF.Round(box.Width * Math.Clamp(health, 0f, 1f));
            handle.DrawRect(fill, ColorFor(health));
        }

        handle.DrawRect(box, _dead ? CrtTerminalPalette.Alert : CrtTerminalPalette.Line, filled: false);
    }
}
