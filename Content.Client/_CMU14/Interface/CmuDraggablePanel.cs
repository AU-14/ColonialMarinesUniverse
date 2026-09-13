using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Maths;

namespace Content.Client._CMU14.Interface;

/// <summary>
///     A panel the player can pick up and put somewhere else.
/// </summary>
/// <remarks>
///     <para>
///     Written for the lobby's round clock, which is deliberately parked in the middle of the screen
///     - the one place nothing else occupies, and also the one place a player might want their own
///     view of the ship instead. Rather than pick a compromise position that suits nobody, let them
///     move it.
///     </para>
///     <para>
///     The position is reported through <see cref="PositionChanged"/> as a fraction of the parent's
///     size rather than in pixels, so whatever stores it survives a resolution change or the CRT
///     panel being collapsed. The control does not save anything itself - it does not know where the
///     caller wants to keep it.
///     </para>
///     <para>
///     Must be a child of a <see cref="LayoutContainer"/>: that is what allows a control to sit at an
///     arbitrary offset rather than wherever its container decides.
///     </para>
/// </remarks>
public sealed class CmuDraggablePanel : PanelContainer
{
    private bool _dragging;

    /// <summary>Where in the panel it was grabbed, so it does not jump under the cursor.</summary>
    private Vector2 _grabOffset;

    /// <summary>
    ///     Raised while dragging, with the panel's top-left as a fraction of the parent's size.
    /// </summary>
    public event Action<Vector2>? PositionChanged;

    public CmuDraggablePanel()
    {
        // Stop, not Pass: the lobby background sits under this and a drag that fell through to it
        // would start whatever that does instead of moving the panel.
        MouseFilter = MouseFilterMode.Stop;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        _dragging = true;
        _grabOffset = args.PointerLocation.Position / UIScale - GlobalPosition;
        args.Handle();
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);

        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        _dragging = false;
        args.Handle();
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);

        if (!_dragging || Parent is not { } parent)
            return;

        var target = args.GlobalPosition - parent.GlobalPosition - _grabOffset;

        // Clamped so the panel cannot be dropped off the edge and become unreachable. The whole
        // panel stays on screen, not just a grab strip of it - there is no title bar to aim for.
        var room = parent.Size - Size;
        var clamped = new Vector2(
            MathHelper.Clamp(target.X, 0f, MathF.Max(0f, room.X)),
            MathHelper.Clamp(target.Y, 0f, MathF.Max(0f, room.Y)));

        LayoutContainer.SetPosition(this, clamped);

        if (room.X > 0f && room.Y > 0f)
            PositionChanged?.Invoke(clamped / room);
    }

    /// <summary>
    ///     Place the panel from a stored fraction of the parent's free space.
    /// </summary>
    /// <remarks>
    ///     Takes the same fraction <see cref="PositionChanged"/> reports, so a caller can hand back
    ///     exactly what it was given. Does nothing until the parent has been laid out and the panel
    ///     has a size - before that there is no free space to take a fraction of, and the result
    ///     would be a panel pinned to the top-left.
    /// </remarks>
    public bool TryPlaceAtFraction(Vector2 fraction)
    {
        if (Parent is not { } parent)
            return false;

        var room = parent.Size - Size;
        if (room.X <= 0f || room.Y <= 0f)
            return false;

        LayoutContainer.SetPosition(this, new Vector2(
            MathHelper.Clamp(fraction.X, 0f, 1f) * room.X,
            MathHelper.Clamp(fraction.Y, 0f, 1f) * room.Y));

        return true;
    }
}
