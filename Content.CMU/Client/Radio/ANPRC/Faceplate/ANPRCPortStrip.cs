using System.Numerics;
using Content.Shared.CMU14.Radio;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.CMU14.Radio.ANPRC;

/// <summary>
///     The connector plate down the side of the set. Each jack reports the state of whatever is
///     screwed into it, which is the only honest thing for a port to do on a panel: they are
///     readouts, not buttons, and the set has four things worth knowing at a glance.
/// </summary>
public sealed class ANPRCPortStrip : Control
{
    private readonly ANPRCPort _antenna;
    private readonly ANPRCPort _audio;
    private readonly ANPRCPort _fill;
    private readonly ANPRCPort _power;

    public ANPRCPortStrip()
    {
        var column = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
        };

        AddChild(column);

        _antenna = new ANPRCPort("J8/J5", Loc.GetString("anprc-fp-port-ant"));
        _audio = new ANPRCPort("J1", Loc.GetString("anprc-fp-port-audio"));
        _fill = new ANPRCPort("J4", Loc.GetString("anprc-fp-port-fill"));
        _power = new ANPRCPort("J2", Loc.GetString("anprc-fp-port-power"));

        column.AddChild(_antenna);
        column.AddChild(_audio);
        column.AddChild(_fill);
        column.AddChild(_power);
    }

    public void SetAntenna(string label, bool fitted)
        => _antenna.Set(fitted ? label : Loc.GetString("anprc-fp-none"), fitted ? ANPRCPortState.Live : ANPRCPortState.Empty);

    public void SetAudio(bool handsetOut)
    {
        // the audio goes wherever it is actually being heard: the handset while it is off its
        // hook, the operator's own headset otherwise
        _audio.Set(Loc.GetString(handsetOut ? "anprc-fp-handset" : "anprc-fp-headset"), ANPRCPortState.Live);
    }

    public void SetFill(string designation, bool secured, bool stale)
    {
        if (string.IsNullOrEmpty(designation))
            _fill.Set(Loc.GetString("anprc-fp-sec-no-fill"), ANPRCPortState.Empty);
        else if (stale)
            _fill.Set(designation, ANPRCPortState.Fault);
        else
            _fill.Set(designation, secured ? ANPRCPortState.Live : ANPRCPortState.Empty);
    }

    public void SetPower(bool hasBattery, float fraction, bool enabled)
    {
        if (!hasBattery)
            _power.Set(Loc.GetString("anprc-fp-rd-bit-no-cell"), ANPRCPortState.Fault);
        else
            _power.Set(
                $"{(int) MathF.Round(fraction * 100f)}%",
                !enabled ? ANPRCPortState.Empty
                    : fraction <= 0.2f ? ANPRCPortState.Fault
                    : ANPRCPortState.Live);
    }

    private enum ANPRCPortState : byte
    {
        Empty,
        Live,
        Fault,
    }

    /// <summary>One jack: the drawn connector, its engraved designation and what is on it.</summary>
    private sealed class ANPRCPort : BoxContainer
    {
        private readonly Jack _jack;
        private readonly Label _value;

        public ANPRCPort(string designation, string function)
        {
            Orientation = LayoutOrientation.Horizontal;
            SeparationOverride = 5;

            _jack = new Jack();
            AddChild(_jack);

            var text = new BoxContainer
            {
                Orientation = LayoutOrientation.Vertical,
                SeparationOverride = 0,
                VerticalAlignment = VAlignment.Center,
                // the value below clips, so it measures as no width at all and the column would
                // otherwise be sized by the engraved designation alone - which is narrower than
                // readings like SPKR HIGH, and cut them off
                HorizontalExpand = true,
            };

            text.AddChild(new Label
            {
                Text = $"{designation} {function}",
                FontOverride = ANPRCPanelStyle.Mono(8),
                FontColorOverride = ANPRCPanelStyle.EngravedDim,
            });

            _value = new Label
            {
                Text = "---",
                FontOverride = ANPRCPanelStyle.Mono(9, true),
                FontColorOverride = ANPRCPanelStyle.Muted,
                ClipText = true,
            };

            text.AddChild(_value);
            AddChild(text);
        }

        public void Set(string value, ANPRCPortState state)
        {
            _value.Text = value;
            _value.FontColorOverride = state switch
            {
                ANPRCPortState.Live => ANPRCPanelStyle.Good,
                ANPRCPortState.Fault => ANPRCPanelStyle.Bad,
                _ => ANPRCPanelStyle.Muted,
            };

            _jack.State = state;
        }

        /// <summary>A threaded circular connector, drawn so the plate reads as metal.</summary>
        private sealed class Jack : Control
        {
            public ANPRCPortState State = ANPRCPortState.Empty;

            public Jack()
            {
                MinSize = new Vector2(22f, 22f);
                MouseFilter = MouseFilterMode.Ignore;
            }

            protected override void Draw(DrawingHandleScreen handle)
            {
                var centre = new Vector2(PixelWidth * 0.5f, PixelHeight * 0.5f);
                var radius = MathF.Min(PixelWidth, PixelHeight) * 0.45f;

                handle.DrawCircle(centre, radius, ANPRCPanelStyle.ChassisEdge);
                handle.DrawCircle(centre, radius - 2f, ANPRCPanelStyle.ChassisRaised);
                handle.DrawCircle(centre, radius - 5f, Color.FromHex("#14170F"));

                var pin = State switch
                {
                    ANPRCPortState.Live => ANPRCPanelStyle.Good,
                    ANPRCPortState.Fault => ANPRCPanelStyle.Bad,
                    _ => ANPRCPanelStyle.EngravedDim,
                };

                handle.DrawCircle(centre, MathF.Max(1.5f, radius - 8f), pin);
            }
        }
    }
}
