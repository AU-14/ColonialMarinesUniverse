using System.Numerics;
using Robust.Client.Graphics;

namespace Content.Client.Popups;

public sealed partial class PopupSystem
{
    public abstract partial class PopupLabel
    {
        private Font? _cmuMeasuredFont;
        private string? _cmuMeasuredText;
        private float _cmuMeasuredScale;
        private Vector2 _cmuDimensions;

        internal Vector2 CMUGetDimensions(DrawingHandleScreen handle, Font font, float scale)
        {
            // Repeat stacking changes Text; type and UI-scale changes can select different font metrics.
            if (_cmuMeasuredFont == font && _cmuMeasuredText == Text && _cmuMeasuredScale == scale)
                return _cmuDimensions;

            _cmuDimensions = handle.GetDimensions(font, Text, scale);
            _cmuMeasuredFont = font;
            _cmuMeasuredText = Text;
            _cmuMeasuredScale = scale;
            return _cmuDimensions;
        }
    }
}
