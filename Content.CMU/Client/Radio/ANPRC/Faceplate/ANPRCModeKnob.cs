using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Input;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Radio.ANPRC;

/// <summary>
///     The detents on the set's function switch, in the order they are engraved down the side of
///     the real radio.
/// </summary>
public enum ANPRCKnobPosition : byte
{
    /// <summary>Set off.</summary>
    Off,

    /// <summary>Secure. Throwing the switch here again steps through the secure waveforms.</summary>
    CipherText,

    /// <summary>In clear. Anybody on the frequency reads the traffic.</summary>
    PlainText,

    /// <summary>Load. Spring-returns: it throws the screen onto the COMSEC page to work the fill.</summary>
    Load,

    /// <summary>Zeroize. Spring-returns, and wants a second pull before it wipes the fill.</summary>
    Zeroize,
}

/// <summary>
///     The function switch off the top right corner of the set. Detents are engraved down the
///     right of the barrel the way they are on the radio, and clicking one throws the switch to
///     it. LD and Z are momentary pulls - the switch is sprung on those two and returns to
///     whichever mode the set is actually in once the pull has done its job.
///
///     The spring is the real hardware's behaviour and it is deliberate, so the plate says so:
///     the sprung detents are marked, the pull is held long enough to read, the detent the
///     switch is about to return to is shown under the pointer while it is held, and
///     <see cref="OnSpringBack"/> fires when it lets go so the panel can print what happened.
///     A switch that appeared to undo the operator's choice on its own was the single most
///     confusing thing about the set.
///
///     The secure detent carries the waveform it is currently sitting on next to its legend, and
///     throwing the switch onto it again steps to the next one, so every mode the set has is
///     reachable from the switch rather than only from the keypad.
/// </summary>
public sealed class ANPRCModeKnob : Control
{
    private static readonly ANPRCKnobPosition[] Positions =
    [
        ANPRCKnobPosition.Off,
        ANPRCKnobPosition.CipherText,
        ANPRCKnobPosition.PlainText,
        ANPRCKnobPosition.Load,
        ANPRCKnobPosition.Zeroize,
    ];

    // long enough that the pull reads as a pull. at half a second it looked like the panel
    // refusing the click rather than a sprung detent doing its job
    private const float MomentarySeconds = 1.6f;

    public event Action<ANPRCKnobPosition>? OnPosition;

    /// <summary>
    ///     A sprung detent has let go and the switch has returned to <see cref="Detent"/>. Carries
    ///     the detent it returned to, so the panel can say where it went instead of leaving the
    ///     operator to notice the pointer has moved on its own.
    /// </summary>
    public event Action<ANPRCKnobPosition>? OnSpringBack;

    /// <summary>Where the set actually is. The pointer rests here whenever nothing is being pulled.</summary>
    public ANPRCKnobPosition Detent { get; set; } = ANPRCKnobPosition.Off;

    /// <summary>
    ///     The secure waveform the set is on, printed beside the CT legend. Empty when the set is
    ///     not secure, so the detent never claims a waveform the radio is not using.
    /// </summary>
    public string Waveform { get; set; } = string.Empty;

    /// <summary>The detent under the pointer right now, including a momentary pull in progress.</summary>
    public ANPRCKnobPosition Shown { get; private set; } = ANPRCKnobPosition.Off;

    /// <summary>Whether a sprung detent is being held down at this moment.</summary>
    public bool Held => _momentary > 0f;

    private float _momentary;
    private int _hovered = -1;

    public ANPRCModeKnob()
    {
        MouseFilter = MouseFilterMode.Stop;
        MinSize = new Vector2(118f, 118f);
    }

    public static string Legend(ANPRCKnobPosition position) => Loc.GetString(position switch
    {
        ANPRCKnobPosition.Off => "anprc-fp-knob-off",
        ANPRCKnobPosition.CipherText => "anprc-fp-knob-ct",
        ANPRCKnobPosition.PlainText => "anprc-fp-knob-pt",
        ANPRCKnobPosition.Load => "anprc-fp-knob-ld",
        _ => "anprc-fp-knob-z",
    });

    private static bool IsMomentary(ANPRCKnobPosition position)
        => position is ANPRCKnobPosition.Load or ANPRCKnobPosition.Zeroize;

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_momentary > 0f)
        {
            _momentary = MathF.Max(0f, _momentary - args.DeltaSeconds);

            if (_momentary > 0f)
                return;

            // the spring has let go. say so, rather than letting the pointer walk back on its
            // own and leave the operator thinking the set overrode them
            Shown = Detent;
            OnSpringBack?.Invoke(Detent);

            return;
        }

        Shown = Detent;
    }

    protected override void MouseExited()
    {
        base.MouseExited();
        _hovered = -1;
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);
        _hovered = DetentAt(args.RelativePixelPosition.Y);
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        var index = DetentAt(args.RelativePixelPosition.Y);

        if (index < 0)
            return;

        args.Handle();

        var position = Positions[index];

        Shown = position;

        if (IsMomentary(position))
            _momentary = MomentarySeconds;

        OnPosition?.Invoke(position);
    }

    private int DetentAt(float pixelY)
    {
        var height = PixelHeight;

        if (height <= 0)
            return -1;

        var step = height / (float) Positions.Length;
        var index = (int) (pixelY / step);

        return index < 0 || index >= Positions.Length ? -1 : index;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var width = (float) PixelWidth;
        var height = (float) PixelHeight;

        if (width <= 0f || height <= 0f)
            return;

        var scale = UIScale;
        var step = height / Positions.Length;
        var labelX = width - 44f * scale;
        var font = ANPRCPanelStyle.Mono(10, true);

        // detent legends, engraved down the plate to the right of the barrel
        for (var i = 0; i < Positions.Length; i++)
        {
            var position = Positions[i];
            var centreY = step * (i + 0.5f);
            var selected = position == Shown;

            var color = selected
                ? ANPRCPanelStyle.KeyLit
                : _hovered == i
                    ? ANPRCPanelStyle.Engraved
                    : ANPRCPanelStyle.EngravedDim;

            if (_hovered == i && !selected)
            {
                handle.DrawRect(
                    new UIBox2(labelX - 6f * scale, centreY - step * 0.4f, width, centreY + step * 0.4f),
                    ANPRCPanelStyle.ChassisRaised);
            }

            // the index mark each detent clicks into. the sprung pair carry a second, shorter
            // mark under the first - the panel's own way of saying the switch will not stay here
            handle.DrawRect(
                new UIBox2(labelX - 8f * scale, centreY - scale, labelX - 2f * scale, centreY + scale),
                color);

            if (IsMomentary(position))
            {
                handle.DrawRect(
                    new UIBox2(labelX - 6f * scale, centreY + 2f * scale, labelX - 2f * scale, centreY + 3f * scale),
                    color);
            }

            // while a sprung detent is held, ring the detent the switch is about to drop back
            // onto, so where it is going is on the plate before it gets there
            if (Held && position == Detent)
            {
                handle.DrawRect(
                    new UIBox2(labelX - 12f * scale, centreY - scale, labelX - 10f * scale, centreY + scale),
                    ANPRCPanelStyle.KeyArmed);
            }

            // the secure detent carries the waveform it is on, so CT never stands for a
            // waveform the set is not actually using
            var legend = position == ANPRCKnobPosition.CipherText && Waveform.Length > 0
                ? Legend(position) + " " + Waveform
                : Legend(position);

            handle.DrawString(font, new Vector2(labelX, centreY - 6f * scale), legend, scale, color);
        }

        // the barrel itself
        var radius = MathF.Min(step * 1.9f, labelX * 0.44f);
        var centre = new Vector2(labelX - 12f * scale - radius, height * 0.5f);

        handle.DrawCircle(centre, radius + 3f * scale, ANPRCPanelStyle.ChassisEdge);
        handle.DrawCircle(centre, radius, ANPRCPanelStyle.ChassisRaised);
        handle.DrawCircle(centre, radius * 0.72f, Color.FromHex("#1B1F16"));

        // knurling, so the barrel reads as something a gloved hand turns
        for (var i = 0; i < 12; i++)
        {
            var angle = MathF.Tau * i / 12f;
            var from = centre + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (radius * 0.74f);
            var to = centre + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;

            handle.DrawLine(from, to, ANPRCPanelStyle.ChassisEdge);
        }

        // the pointer, aimed at whichever detent is under the switch
        var shownIndex = Array.IndexOf(Positions, Shown);

        if (shownIndex < 0)
            shownIndex = 0;

        var target = new Vector2(labelX - 6f * scale, step * (shownIndex + 0.5f));
        var direction = Vector2.Normalize(target - centre);

        // a held pull is drawn in the armed colour: the pointer is somewhere it will not stay
        var pointer = Held ? ANPRCPanelStyle.KeyArmed : ANPRCPanelStyle.KeyLit;

        handle.DrawLine(centre, centre + direction * radius, pointer);
        handle.DrawCircle(centre + direction * radius, 3f * scale, pointer);
        handle.DrawCircle(centre, 3f * scale, ANPRCPanelStyle.Engraved);

        // and the ghost of where the spring is taking it
        if (Held)
        {
            var restIndex = Array.IndexOf(Positions, Detent);

            if (restIndex >= 0)
            {
                var rest = new Vector2(labelX - 6f * scale, step * (restIndex + 0.5f));
                var restDirection = Vector2.Normalize(rest - centre);

                handle.DrawCircle(centre + restDirection * radius, 2f * scale, ANPRCPanelStyle.EngravedDim);
            }
        }
    }
}
