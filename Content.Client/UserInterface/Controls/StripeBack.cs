using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.UserInterface.Controls
{
    public sealed class StripeBack : Container
    {
        private const float PadSize = 4;
        private const float EdgeSize = 2;
        private static readonly Color DefaultEdgeColor = Color.FromHex("#525252ff");

        private bool _hasTopEdge = true;
        private bool _hasBottomEdge = true;
        private bool _hasMargins = true;

        public const string StylePropertyBackground = "background";

        /// <summary>
        ///     Colour of the edge lines above and below the strip.
        /// </summary>
        /// <remarks>
        ///     A style property rather than a constant because these lines are drawn outside the
        ///     background stylebox, so nothing in a stylesheet could otherwise reach them - a themed
        ///     UI got a stray light grey rule through it with no way to override it.
        /// </remarks>
        public const string StylePropertyEdgeColor = "edge-color";

        public bool HasTopEdge
        {
            get => _hasTopEdge;
            set
            {
                _hasTopEdge = value;
                InvalidateMeasure();
            }
        }

        public bool HasBottomEdge
        {
            get => _hasBottomEdge;
            set
            {
                _hasBottomEdge = value;
                InvalidateMeasure();
            }
        }

        public bool HasMargins
        {
            get => _hasMargins;
            set
            {
                _hasMargins = value;
                InvalidateMeasure();
            }
        }

        protected override Vector2 MeasureOverride(Vector2 availableSize)
        {
            var padSize = HasMargins ? PadSize : 0;
            var padSizeTotal = 0f;

            if (HasBottomEdge)
                padSizeTotal += padSize + EdgeSize;
            if (HasTopEdge)
                padSizeTotal += padSize + EdgeSize;

            var size = Vector2.Zero;

            availableSize.Y -= padSizeTotal;

            foreach (var child in Children)
            {
                child.Measure(availableSize);
                size = Vector2.Max(size, child.DesiredSize);
            }

            return size + new Vector2(0, padSizeTotal);
        }

        protected override Vector2 ArrangeOverride(Vector2 finalSize)
        {
            var box = new UIBox2(Vector2.Zero, finalSize);

            var padSize = HasMargins ? PadSize : 0;

            if (HasTopEdge)
            {
                box += (0, padSize + EdgeSize, 0, 0);
            }

            if (HasBottomEdge)
            {
                box += (0, 0, 0, -(padSize + EdgeSize));
            }

            foreach (var child in Children)
            {
                child.Arrange(box);
            }

            return finalSize;
        }


        protected override void Draw(DrawingHandleScreen handle)
        {
            UIBox2 centerBox = PixelSizeBox;

            var padSize = HasMargins ? PadSize : 0;
            var edgeColor = TryGetStyleProperty(StylePropertyEdgeColor, out Color styled)
                ? styled
                : DefaultEdgeColor;

            if (HasTopEdge)
            {
                centerBox += (0, (padSize + EdgeSize) * UIScale, 0, 0);
                handle.DrawRect(new UIBox2(0, padSize * UIScale, PixelWidth, centerBox.Top), edgeColor);
            }

            if (HasBottomEdge)
            {
                centerBox += (0, 0, 0, -((padSize + EdgeSize) * UIScale));
                handle.DrawRect(new UIBox2(0, centerBox.Bottom, PixelWidth, PixelHeight - padSize * UIScale),
                    edgeColor);
            }

            GetActualStyleBox()?.Draw(handle, centerBox, UIScale);
        }

        private StyleBox? GetActualStyleBox()
        {
            return TryGetStyleProperty(StylePropertyBackground, out StyleBox? box) ? box : null;
        }
    }
}
