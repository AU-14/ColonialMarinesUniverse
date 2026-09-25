using System.Numerics;
using Content.Shared.CMU14.Fighter;

namespace Content.Client.CMU14.Fighter;

public sealed partial class FighterStrikeOverlay
{
    private static readonly Color Dust = Color.FromHex("#A59576");
    private static readonly Color Soot = Color.FromHex("#242827");
    private static readonly Color Spray = Color.FromHex("#C7EAF1");
    private static readonly Color Spark = Color.FromHex("#FFE2A1");
    private static readonly Color Blast = Color.FromHex("#FFAC56");

    private void DrawImpact(Vector2 point, float age, FighterWeaponKind kind, bool water, float seed)
    {
        if (age < 0 || age >= FighterEffects.GroundLifetime) return;
        var gau = kind == FighterWeaponKind.Gau;
        var size = gau ? .65f : kind == FighterWeaponKind.Rockets ? 2f : 3f;
        var fade = 1 - age / FighterEffects.GroundLifetime;
        if (!water)
            _particles.Mote(point, new Vector2(size * .85f, size * .6f), Soot.WithAlpha(fade * .75f));
        if (age > (gau ? 1.5f : 4.5f)) return;
        if (age < .3f)
        {
            _particles.Mote(point, new Vector2(size * (1 + age * 3)), (water ? Spray : Blast).WithAlpha(1 - age / .3f));
            _particles.Mote(point, new Vector2(size * .3f), Color.White.WithAlpha(1 - age / .3f));
        }
        // A low ring makes the impact readable from above without covering the area.
        if (age < 1)
        {
            var radius = size * (.3f + age * 2);
            for (var i = 0; i < 24; i++)
            {
                var angle = i * MathF.Tau / 24;
                _particles.Mote(point + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius,
                    new Vector2(size * .17f), (water ? Spray : Dust).WithAlpha((1 - age) * .6f));
            }
        }
        for (var i = 0; i < (gau ? 7 : 12); i++)
        {
            var angle = i * 2.4f + seed;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var lifetime = gau ? 1.2f : 3.8f;
            var opacity = Math.Max(0, 1 - age / lifetime);
            var plume = point + (direction * age * .7f + new Vector2(.12f * age, age * .6f)) * size;
            _particles.Mote(plume, new Vector2(.18f + age * .6f, .22f + age * .75f) * size,
                (water ? Spray : Dust).WithAlpha(opacity * (gau ? .45f : .6f)), smoke: true, seed: seed + i);
            if (age < .8f)
            {
                var fragment = point + (direction * age * (1.5f + i % 3) + new Vector2(0, age * (1 - age) * 2)) * size;
                _particles.Trail(fragment, fragment - direction * size * .18f, size * .055f,
                    (water ? Spray : Spark).WithAlpha(1 - age / .8f));
            }
        }
    }
}
