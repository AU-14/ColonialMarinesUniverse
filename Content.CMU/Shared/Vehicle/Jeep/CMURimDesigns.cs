namespace Content.Shared.CMU14.Vehicle.Jeep;

/// <summary>
/// How a rim design looks, pixel by pixel. A rim is 11 pixels across, centred on its hub pixel: u
/// runs across it and v up it, from -5 to 5. Its four frames turn it a quarter of one repeat of its
/// pattern each, the way the wheels' frames turn the tyre, so a moving wheel's loop is seamless. The
/// client bakes a vehicle's design into a small texture of colours from this for its shader, and the
/// painter draws its previews from it.
/// </summary>
public static class CMURimDesigns
{
    public const int Radius = 5;
    public const int Size = 2 * Radius + 1;
    public const int Frames = 4;

    // The rim reaches this far from the hub, as the art's wheels draw it (wheel_disc in jeep_pixel_art.py).
    private const float Edge = 5.6f;

    public static bool OnRim(int u, int v)
    {
        return MathF.Sqrt(u * u + v * v) <= Edge;
    }

    /// <summary>
    /// Which colour a pixel of the rim takes, and how it is lit.
    /// </summary>
    public static (CMURimRegion Region, CMURimTone Tone) Sample(CMURimDesignPrototype design, int u, int v, int frame)
    {
        var count = Math.Max(1, design.Count);
        var dist = MathF.Sqrt(u * u + v * v);
        // A feature at angle a shows where angle + turn is a: the pattern turns the tyre's way.
        var turn = frame * MathF.Tau / (Frames * count);

        if (dist > design.Lip)
        {
            if (design.LipBolts > 0 && OnRing(u, v, design.LipBolts, design.Lip + 0.4f, turn))
                return Part(design, CMURimRegion.Cap, CMURimTone.Highlight);

            return Part(design, CMURimRegion.Lip, dist > Edge - 0.3f ? CMURimTone.Shadow : CMURimTone.Plain);
        }

        if (dist < design.Cap)
            return Part(design, CMURimRegion.Cap, dist < 0.5f ? CMURimTone.Highlight : CMURimTone.Plain);

        if (design.Nuts > 0 && OnRing(u, v, design.Nuts, design.NutRadius, turn))
            return Part(design, CMURimRegion.Cap, CMURimTone.Shadow);

        var angle = MathF.Atan2(v, u) + turn;
        var slice = MathF.Tau / count;
        var across = Math.Clamp((dist - design.Cap) / MathF.Max(0.1f, design.Lip - design.Cap), 0f, 1f);
        switch (design.Pattern)
        {
            case CMURimPattern.Spokes:
            {
                var spoke = Centred(angle + design.Twist * slice * across, slice) < design.Width / 2;
                return spoke ? Part(design, CMURimRegion.Accent, CMURimTone.Plain) : (CMURimRegion.Base, CMURimTone.Hole);
            }
            case CMURimPattern.Mesh:
            {
                var twist = design.Twist * slice * across;
                var spoke = Centred(angle + twist, slice) < design.Width / 2 ||
                            Centred(angle - twist + slice / 2, slice) < design.Width / 2;
                return spoke ? Part(design, CMURimRegion.Accent, CMURimTone.Plain) : (CMURimRegion.Base, CMURimTone.Hole);
            }
            case CMURimPattern.Holes:
            {
                for (var n = 0; n < count; n++)
                {
                    var a = n * slice - turn;
                    var hx = design.HoleRadius * MathF.Cos(a);
                    var hy = design.HoleRadius * MathF.Sin(a);
                    if (MathF.Sqrt((u - hx) * (u - hx) + (v - hy) * (v - hy)) < design.HoleSize)
                        return (CMURimRegion.Base, CMURimTone.Hole);
                }

                return (CMURimRegion.Base, CMURimTone.Plain);
            }
            case CMURimPattern.Slots:
            {
                var slot = MathF.Abs(dist - design.HoleRadius) < design.HoleSize &&
                           Centred(angle + slice / 2, slice) < design.Width / 2;
                return slot ? (CMURimRegion.Base, CMURimTone.Hole) : (CMURimRegion.Base, CMURimTone.Plain);
            }
            case CMURimPattern.Disc:
                return Part(design, CMURimRegion.Accent, dist > design.Lip - 0.8f ? CMURimTone.Shadow : CMURimTone.Plain);
            default:
                return (CMURimRegion.Base, CMURimTone.Plain);
        }
    }

    /// <summary>
    /// A pixel's colour, from the design's base, accent, lip and cap colours.
    /// </summary>
    public static Color Shade(CMURimRegion region, CMURimTone tone, Color baseColor, Color accent, Color lip, Color cap)
    {
        var colour = region switch
        {
            CMURimRegion.Accent => accent,
            CMURimRegion.Lip => lip,
            CMURimRegion.Cap => cap,
            _ => baseColor,
        };

        return tone switch
        {
            CMURimTone.Shadow => new Color(colour.R * 0.62f, colour.G * 0.62f, colour.B * 0.62f),
            CMURimTone.Highlight => Color.InterpolateBetween(colour, Color.White, 0.35f),
            // An open gap shows the dark drum behind, with a little of the rim's colour.
            CMURimTone.Hole => new Color(0.07f + colour.R * 0.1f, 0.07f + colour.G * 0.1f, 0.07f + colour.B * 0.1f),
            _ => colour,
        };
    }

    /// <summary>
    /// A part of the rim in its colour, or, where the design has no colour of its own for it, in a
    /// shade of the base: the lip darker and the cap brighter, like the plain rims.
    /// </summary>
    private static (CMURimRegion Region, CMURimTone Tone) Part(CMURimDesignPrototype design, CMURimRegion region, CMURimTone tone)
    {
        if (design.Regions.Contains(region))
            return (region, tone);

        return region switch
        {
            CMURimRegion.Lip => (CMURimRegion.Base, CMURimTone.Shadow),
            CMURimRegion.Cap => (CMURimRegion.Base, tone == CMURimTone.Shadow ? CMURimTone.Shadow : CMURimTone.Highlight),
            _ => (CMURimRegion.Base, tone),
        };
    }

    /// <summary>
    /// How far an angle is from the middle of its slice, as a share of the slice.
    /// </summary>
    private static float Centred(float angle, float slice)
    {
        var s = angle / slice - MathF.Floor(angle / slice);
        return MathF.Min(s, 1f - s);
    }

    /// <summary>
    /// Whether a pixel is one of a ring of single pixel points, like the art's lug nuts.
    /// </summary>
    private static bool OnRing(int u, int v, int count, float radius, float turn)
    {
        for (var n = 0; n < count; n++)
        {
            var a = n * MathF.Tau / count - turn;
            if (u == (int) MathF.Round(radius * MathF.Cos(a)) && v == (int) MathF.Round(radius * MathF.Sin(a)))
                return true;
        }

        return false;
    }
}

public enum CMURimTone : byte
{
    Plain,
    Shadow,
    Highlight,
    Hole,
}
