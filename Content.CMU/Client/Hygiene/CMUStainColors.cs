using Robust.Shared.Maths;
using System;

namespace Content.Client.CMU14.Hygiene;

public static class CMUStainColors
{
    public static Color Brighten(Color color)
    {
        var peak = Math.Max(color.R, Math.Max(color.G, color.B));
        var gain = peak > 0f ? Math.Min(1.65f, 1f / peak) : 1f;
        return new Color(color.R * gain, color.G * gain, color.B * gain, color.A);
    }
}
