using System.Collections.Generic;
using System.Numerics;
using Content.Shared.CMU14.Dropship.Integrity;
using Robust.Client.Graphics;

namespace Content.Client.CMU14.Dropship.TacticalLand;

/// <summary>Shared hull and warning rendering for the pilot and crew displays.</summary>
public sealed class GunshipHudStatusDisplay(Font smallFont)
{
    private readonly Font _smallFont = smallFont;
    private readonly List<string> _warningLines = new();
    private static readonly Color HudColor = new(0.25f, 0.88f, 1f, 0.95f);
    private static readonly Color HudBackground = new(0.015f, 0.06f, 0.08f, 0.78f);
    private static readonly Color IntegrityColor = new(0.92f, 0.08f, 0.08f, 0.95f);
    private static readonly Color IntegrityBorder = new(1f, 0.22f, 0.22f, 0.8f);
    private static readonly Color MalfunctionColor = new(1f, 0.24f, 0.12f, 0.98f);

    public void DrawIntegrityBar(
        DrawingHandleScreen handle,
        UIBox2i bounds,
        float integrity, float maxIntegrity)
    {
        const float width = 320f;
        const float height = 24f;
        const float bottomMargin = 24f;
        var left = bounds.Left + (bounds.Width - width) * 0.5f;
        var top = bounds.Bottom - bottomMargin - height;
        var box = new UIBox2(left, top, left + width, top + height);
        var ratio = maxIntegrity > 0f
            ? Math.Clamp(integrity / maxIntegrity, 0f, 1f)
            : 0f;

        handle.DrawRect(box, HudBackground);
        if (ratio > 0f)
        {
            var fill = new UIBox2(box.Left + 3f,
                box.Top + 3f,
                box.Left + 3f + (box.Width - 6f) * ratio,
                box.Bottom - 3f);
            handle.DrawRect(fill, IntegrityColor);
        }

        handle.DrawRect(box, IntegrityBorder, false);

        var text = Loc.GetString("cmu-gunship-hud-hull-percent",
            ("percent", MathF.Round(ratio * 100f)));
        var textSize = handle.GetDimensions(_smallFont, text, 1);
        var textPosition = box.Center - new Vector2(textSize.X / 2f, 0);
        handle.DrawString(_smallFont, textPosition + Vector2.One, text, Color.Black);
        handle.DrawString(_smallFont, textPosition, text, Color.White);
    }

    public void DrawWarnings(
        DrawingHandleScreen handle,
        UIBox2i bounds,
        IReadOnlyList<DropshipMalfunction> malfunctions, IReadOnlyList<DropshipAlarm> alarms,
        bool masterAlarmSilenced, TimeSpan time, float bottomMargin = 24f)
    {
        if (malfunctions.Count == 0 && alarms.Count == 0)
            return;

        const float margin = 24f;
        const float lineHeight = 20f;
        const float padding = 10f;
        var blinkOn = (int)time.TotalSeconds % 2 == 0;
        if (!blinkOn)
            return;

        var lines = _warningLines;
        lines.Clear();
        lines.Add(Loc.GetString("cmu-gunship-hud-system-warnings"));
        foreach (var alarm in alarms)
            lines.Add(DropshipAlarmData.GetAlertName(alarm));
        foreach (var malfunction in malfunctions)
            lines.Add(Loc.GetString("cmu-gunship-malfunction-detected",
                ("malfunction", DropshipMalfunctionData.GetAlertName(malfunction))));
        if (masterAlarmSilenced && alarms.Count > 0)
            lines.Add(Loc.GetString("cmu-gunship-hud-master-alarm-silenced"));

        var width = 260f;
        foreach (var text in lines)
            width = MathF.Max(width, handle.GetDimensions(_smallFont, text, 1).X + padding * 2f);

        var height = padding * 2f + lineHeight * lines.Count;
        var box = new UIBox2(bounds.Right - margin - width,
            bounds.Bottom - bottomMargin - height,
            bounds.Right - margin,
            bounds.Bottom - bottomMargin);

        var warningColor = MalfunctionColor;
        handle.DrawRect(box, HudBackground);
        handle.DrawRect(box, warningColor, false);
        for (var i = 0; i < lines.Count; i++)
        {
            handle.DrawString(_smallFont,
                new Vector2(box.Left + padding, box.Top + padding + lineHeight * i),
                lines[i],
                i == lines.Count - 1 && masterAlarmSilenced && alarms.Count > 0 ? HudColor : warningColor);
        }
    }
}
