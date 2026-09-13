using System;
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Maths;

namespace Content.Client._CMU14.UserInterface.ColorPicker;

/// <summary>
///     A draggable colour field. Paints a <see cref="ColorSelectorStyleBox"/> and reports the pointer
///     position as a normalised value on each axis.
/// </summary>
/// <remarks>
///     <para>
///     This exists because every slider the engine ships is one-dimensional, and a saturation/value
///     square needs two axes at once. The gradient itself is not custom: ColorSelectorStyleBox is
///     shader-backed and takes its axes as plain public fields, so a square is the same amount of
///     setup as a strip.
///     </para>
///     <para>
///     Y is measured from the bottom, matching the direction the gradient is drawn.
///     </para>
/// </remarks>
public sealed class ColorFieldControl : Control
{
    /// <summary>
    ///     Colour of the 1px frame drawn around the field. Comes from the stylesheet so it tracks the
    ///     CRT palette; a gradient with no frame reads as floating on the background.
    /// </summary>
    public const string StylePropertyBorderColor = "border-color";

    private readonly ColorSelectorStyleBox _styleBox = new();
    private readonly bool _twoAxis;
    private bool _dragging;

    /// <summary>
    ///     Normalised position on each axis, 0..1.
    /// </summary>
    public Vector2 Value { get; private set; }

    public event Action<Vector2>? OnValueChanged;

    /// <param name="xAxis">Which colour component the horizontal axis varies, as an HSVa mask.</param>
    /// <param name="yAxis">Which colour component the vertical axis varies. Zero for a strip.</param>
    public ColorFieldControl(Vector4 xAxis, Vector4 yAxis)
    {
        _styleBox.Hsv = true;
        _styleBox.XAxis = xAxis;
        _styleBox.YAxis = yAxis;
        _twoAxis = xAxis != Vector4.Zero && yAxis != Vector4.Zero;

        MouseFilter = MouseFilterMode.Stop;
        DefaultCursorShape = CursorShape.Hand;
    }

    /// <summary>
    ///     Sets the colour the gradient is drawn around. SetBaseColor already strips whatever the
    ///     axes control, so the current HSVa can be handed straight in.
    /// </summary>
    public void SetBaseColorHsv(Vector4 hsv)
    {
        _styleBox.SetBaseColor(hsv);
    }

    /// <summary>
    ///     Moves the marker without raising <see cref="OnValueChanged"/>.
    /// </summary>
    public void SetValueWithoutEvent(Vector2 value)
    {
        Value = Vector2.Clamp(value, Vector2.Zero, Vector2.One);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        if (PixelWidth <= 0 || PixelHeight <= 0)
            return;

        _styleBox.Draw(handle, new UIBox2(0, 0, PixelWidth, PixelHeight), UIScale);

        var x = Value.X * PixelWidth;
        var y = (1f - Value.Y) * PixelHeight;
        var thickness = MathF.Max(1f, UIScale);

        var border = TryGetStyleProperty<Color>(StylePropertyBorderColor, out var styled)
            ? styled
            : Color.White.WithAlpha(0.35f);

        DrawOutline(handle, 0, 0, PixelWidth, PixelHeight, thickness, border);

        // Drawn as rects rather than primitives: the UI render path batches quads, and every custom
        // draw in this codebase stays on DrawRect for that reason.
        if (_twoAxis)
        {
            var half = 4f * UIScale;
            DrawOutline(handle, x - half, y - half, x + half, y + half, thickness, Color.White);
            return;
        }

        handle.DrawRect(new UIBox2(0, y - thickness, PixelWidth, y + thickness), Color.White);
    }

    private static void DrawOutline(
        DrawingHandleScreen handle,
        float left,
        float top,
        float right,
        float bottom,
        float thickness,
        Color color)
    {
        handle.DrawRect(new UIBox2(left, top, right, top + thickness), color);
        handle.DrawRect(new UIBox2(left, bottom - thickness, right, bottom), color);
        handle.DrawRect(new UIBox2(left, top, left + thickness, bottom), color);
        handle.DrawRect(new UIBox2(right - thickness, top, right, bottom), color);
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        _dragging = true;
        UpdateFromPointer(args.RelativePixelPosition);
        args.Handle();
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);
        if (args.Function != EngineKeyFunctions.UIClick || !_dragging)
            return;

        _dragging = false;
        args.Handle();
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);

        if (_dragging)
            UpdateFromPointer(args.RelativePixelPosition);
    }

    private void UpdateFromPointer(Vector2 relativePixel)
    {
        if (PixelWidth <= 0 || PixelHeight <= 0)
            return;

        var x = Math.Clamp(relativePixel.X / PixelWidth, 0f, 1f);
        var y = Math.Clamp(1f - relativePixel.Y / PixelHeight, 0f, 1f);

        Value = new Vector2(x, y);
        OnValueChanged?.Invoke(Value);
    }
}
