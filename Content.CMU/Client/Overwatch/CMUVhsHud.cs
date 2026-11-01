using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client.CMU14.Overwatch;

/// <summary>
/// The on-screen display of a camcorder over an overwatch feed: a blinking REC light, the channel,
/// tape speed and a running timecode.
/// </summary>
public sealed class CMUVhsHud : BoxContainer
{
    [Dependency] private IGameTiming _timing = default!;

    private readonly Label _timecode;

    public CMUVhsHud(int channel)
    {
        IoCManager.InjectDependencies(this);

        Orientation = LayoutOrientation.Vertical;
        HorizontalExpand = true;
        VerticalExpand = true;
        MouseFilter = MouseFilterMode.Ignore;
        Margin = new Thickness(10, 8);

        _timecode = Osd(string.Empty);

        AddChild(new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 6,
            Children =
            {
                new RecLight(),
                Osd(Loc.GetString("cmu-overwatch-feeds-rec")),
                new Control { HorizontalExpand = true },
                Osd(Loc.GetString("cmu-overwatch-feeds-channel", ("channel", channel))),
            },
        });

        AddChild(new Control { VerticalExpand = true });

        AddChild(new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            Children =
            {
                Osd(Loc.GetString("cmu-overwatch-feeds-speed")),
                new Control { HorizontalExpand = true },
                _timecode,
            },
        });
    }

    private static Label Osd(string text)
    {
        return new Label
        {
            Text = text,
            FontColorOverride = Color.White,
            FontColorShadowOverride = Color.Black,
            ShadowOffsetXOverride = 1,
            ShadowOffsetYOverride = 1,
            MouseFilter = MouseFilterMode.Ignore,
            VerticalAlignment = VAlignment.Center,
        };
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        var time = _timing.CurTime;
        _timecode.Text = $"{(int) time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}";
    }

    private sealed class RecLight : Control
    {
        [Dependency] private IGameTiming _timing = default!;

        public RecLight()
        {
            IoCManager.InjectDependencies(this);
            MinSize = new Vector2(10, 10);
            VerticalAlignment = VAlignment.Center;
            MouseFilter = MouseFilterMode.Ignore;
        }

        protected override void Draw(DrawingHandleScreen handle)
        {
            base.Draw(handle);

            // On for most of each second, the way a camcorder's light blinks.
            if (_timing.RealTime.TotalSeconds % 1.0 > 0.7)
                return;

            var radius = Math.Min(PixelWidth, PixelHeight) / 2f;
            handle.DrawCircle(PixelSize / 2f, radius, Color.FromHex("#FF2A2A"));
        }
    }
}
