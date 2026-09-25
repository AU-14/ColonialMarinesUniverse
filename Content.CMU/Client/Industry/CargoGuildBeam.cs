using System.Numerics;

namespace Content.Client.CMU14.Industry;

/// <summary>The guild beacon's luminous column, also used for aircraft designation.</summary>
internal static class CargoGuildBeam
{
    private static readonly Color BeamColor = Color.FromHex("#87CEDA");

    public static void Draw(CavernParticleBatch particles, Vector2 at, float power, float age, int seed = 0, Color? color = null)
    {
        if (power <= 0) return;
        var tint = color ?? BeamColor;
        var flicker = .83f + .17f * MathF.Sin(age * 37 + seed * 13);
        var strength = power * flicker;
        var top = at + new Vector2(0, 24);
        particles.Trail(at, top, .075f, tint.WithAlpha(.36f * strength));
        particles.Filament(at, top, .012f, tint.WithAlpha(.92f * strength));
        particles.Filament(at, at + new Vector2(0, 1.3f), .023f, (color ?? Color.White).WithAlpha(.8f * strength));
        particles.Mote(at, new Vector2(.23f, .09f), 0, tint.WithAlpha(.85f * strength));
        for (var i = 0; i < 8; i++)
        {
            var phase = age * .7f + i * .127f + seed * .071f;
            var p = phase - MathF.Floor(phase);
            var particle = at + new Vector2(MathF.Sin(i * 9 + seed) * .09f, p * 9);
            particles.Trail(particle, particle + new Vector2(0, .14f + p * .4f), .025f,
                tint.WithAlpha((1 - p) * strength * .6f));
        }
    }
}
