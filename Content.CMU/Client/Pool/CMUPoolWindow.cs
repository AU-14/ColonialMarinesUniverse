using System.Numerics;
using Content.Client.UserInterface.Controls;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Localization;
using Robust.Shared.Maths;

namespace Content.Client.CMU14.Pool;

internal sealed class CMUPoolWindow : FancyWindow
{
    internal CMUPoolWindow(CMUPoolTableControl table)
    {
        Resizable = false;
        Title = Loc.GetString("cmu-pool-title");
        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 10,
            Margin = new Thickness(12),
        };
        var header = new BoxContainer { SeparationOverride = 12 };
        var remaining = new Label { HorizontalExpand = true };
        var rack = new Button { Text = Loc.GetString("cmu-pool-rack-again") };
        header.AddChild(remaining);
        header.AddChild(rack);
        root.AddChild(header);

        root.AddChild(table);

        var power = new BoxContainer { SeparationOverride = 10 };
        power.AddChild(new Label { Text = Loc.GetString("cmu-pool-power") });
        var meter = new PowerMeter { HorizontalExpand = true, MinHeight = 14 };
        power.AddChild(meter);
        root.AddChild(power);

        void UpdateHeader()
        {
            rack.Disabled = table.WaitingForTable || table.Game.Rolling;
            if (table.WaitingForTable)
            {
                remaining.Text = string.Empty;
                return;
            }

            remaining.Text = Loc.GetString("cmu-pool-remaining", ("count", table.Game.Remaining));
        }

        table.StateChanged += UpdateHeader;
        table.PowerChanged += value => meter.Value = value;
        rack.OnPressed += _ => table.Rack();
        UpdateHeader();
        XamlChildren.Add(root);
    }

    private sealed class PowerMeter : Control
    {
        private const int Steps = 64;
        private static readonly Color[] Gradient = BuildGradient();

        public float Value { get; set; }

        protected override void Draw(DrawingHandleScreen handle)
        {
            base.Draw(handle);
            var width = PixelSize.X;
            var height = PixelSize.Y;
            var inset = MathF.Max(1, MathF.Round(2 * UIScale));
            var innerWidth = width - inset * 2;
            var innerHeight = height - inset * 2;
            if (innerWidth <= 0 || innerHeight <= 0)
                return;

            handle.DrawRect(UIBox2.FromDimensions(0, 0, width, height), Color.FromHex("#4A555E"));
            handle.DrawRect(UIBox2.FromDimensions(1, 1, width - 2, height - 2), Color.FromHex("#171E25"));
            var fillWidth = MathF.Round(innerWidth * Math.Clamp(Value, 0, 1));
            for (var i = 0; i < Steps; i++)
            {
                var start = MathF.Round(innerWidth * i / Steps);
                if (start >= fillWidth)
                    break;

                var end = MathF.Min(fillWidth, MathF.Round(innerWidth * (i + 1) / Steps));
                if (end > start)
                    handle.DrawRect(UIBox2.FromDimensions(inset + start, inset, end - start, innerHeight), Gradient[i]);
            }
        }

        private static Color[] BuildGradient()
        {
            Vector3[] stops =
            [
                new(76, 189, 124), new(224, 198, 71), new(235, 144, 63), new(229, 73, 72),
            ];
            var gradient = new Color[Steps];
            for (var i = 0; i < gradient.Length; i++)
            {
                var t = (float) i / (Steps - 1) * (stops.Length - 1);
                var index = Math.Min((int) t, stops.Length - 2);
                var rgb = Vector3.Lerp(stops[index], stops[index + 1], t - index) / 255;
                gradient[i] = new Color(rgb.X, rgb.Y, rgb.Z, 1f);
            }

            return gradient;
        }
    }
}
