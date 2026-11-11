using System.Numerics;
using Content.Shared.CMU14.Dropship.TacticalLand;

namespace Content.Client.CMU14.Dropship.TacticalLand;

internal static class MohawkApproachAnimation
{
    public const int BeaconCount = 8;

    public static float Progress(MohawkLandingBorderComponent approach, TimeSpan now)
    {
        var duration = (approach.Touchdown - approach.ApproachStart).TotalSeconds;
        return duration <= 0 ? 1f : Math.Clamp((float) ((now - approach.ApproachStart).TotalSeconds / duration), 0f, 1f);
    }

    public static float Height(float progress)
    {
        // Ease into touchdown; the roof and its shadow arrive at the exact landing transform.
        var remaining = 1f - progress;
        return remaining * remaining * 5f;
    }

    public static float RoofOpacity(float progress)
        => Math.Clamp(progress / 0.12f, 0f, 1f) * Math.Clamp((1f - progress) / 0.08f, 0f, 1f);

    public static float Pulse(float seconds, int beacon)
        => 0.5f + 0.5f * MathF.Sin(seconds * MathF.Tau * 0.8f + (beacon % 2) * MathF.PI);

    public static Vector2 BeaconPosition(Box2 bounds, int beacon)
    {
        return beacon switch
        {
            0 => new(bounds.Left + 0.6f, bounds.Top - 0.6f),
            1 => new(bounds.Right - 0.6f, bounds.Top - 0.6f),
            2 => new(bounds.Right - 0.6f, bounds.Bottom + 0.6f),
            3 => new(bounds.Left + 0.6f, bounds.Bottom + 0.6f),
            4 => new(bounds.Left, bounds.Center.Y + bounds.Height * 0.22f),
            5 => new(bounds.Right, bounds.Center.Y + bounds.Height * 0.22f),
            6 => new(bounds.Left, bounds.Center.Y - bounds.Height * 0.22f),
            _ => new(bounds.Right, bounds.Center.Y - bounds.Height * 0.22f),
        };
    }
}
