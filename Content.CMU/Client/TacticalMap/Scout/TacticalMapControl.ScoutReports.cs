using System.Numerics;
using Content.Client.CMU14.TacticalMap.Scout;
using Content.Shared.CMU14.TacticalMap.Scout;
using Robust.Client.Graphics;

namespace Content.Client._RMC14.TacticalMap;

public sealed partial class TacticalMapControl
{
    private void DrawScoutReport(DrawingHandleScreen handle, CMUScoutReportInfo report,
        Vector2 position, float blipSize, TimeSpan now)
    {
        var text = CMUScoutReportPresentation.Label(report, now);
        var label = position + new Vector2(blipSize + 3, 0);
        var size = handle.GetDimensions(_font, text, UIScale);
        if (label.X + size.X > PixelWidth)
            label.X = Math.Max(0, position.X - size.X - 3);
        handle.DrawRect(UIBox2.FromDimensions(label - new Vector2(2), size + new Vector2(4)),
            Color.Black.WithAlpha(0.85f));
        handle.DrawString(_font, label, text, UIScale, Color.Orange);
    }
}
