using Content.Shared.CMU14.Fighter;

namespace Content.Server.CMU14.ForceOnForce;

public sealed partial class ForceOnForceBombardmentSystem
{
    private readonly record struct Pattern(int Variant, int Variation, int Pulses, float Gap,
        float Sweep, float LeadIn, float Scale, float BeamSeconds = 0)
    {
        public FighterWeaponKind Weapon => Variant switch
        {
            0 => FighterWeaponKind.Gau,
            1 => FighterWeaponKind.Rockets,
            _ => FighterWeaponKind.Missile,
        };
    }

    // Each main selection keeps its own visual identity. The four arrangements change
    // burst density, pauses, approach directions and impact size within that selection.
    private static Pattern GetPattern(int variant, int variation) => (variant, variation) switch
    {
        (0, 0) => new(0, 0, 4, .18f, .18f, .3f, 1),       // Raking salvo.
        (0, 1) => new(0, 1, 2, .5f, .4f, .4f, 1.2f),      // Staggered double.
        (0, 2) => new(0, 2, 3, .65f, 1.8f, .25f, .85f),    // Scattered shell pops.
        (0, 3) => new(0, 3, 4, .32f, .95f, .35f, 1.1f),    // Dense rolling barrage.
        (1, 0) => new(1, 0, 2, .85f, .3f, .35f, 1, 1.4f), // Sustained lances.
        (1, 1) => new(1, 1, 4, .18f, .12f, .2f, .8f, .35f),// Stuttering beams.
        (1, 2) => new(1, 2, 4, .3f, .5f, .3f, 1, .65f),    // Sweeping fan.
        (1, 3) => new(1, 3, 3, .5f, 2.1f, .4f, 1.2f, .9f),// Crossing lances.
        (2, 0) => new(2, 0, 1, 1, 0, 1.3f, 1.4f),         // Heavy fireball.
        (2, 1) => new(2, 1, 2, .4f, .65f, .9f, 1),         // Paired fireballs.
        (2, 2) => new(2, 2, 4, .22f, .8f, .65f, .75f),      // Fragment shower.
        (2, 3) => new(2, 3, 3, .72f, 2.2f, 1.1f, 1.1f),   // Sporadic meteor rain.
        (3, 0) => new(3, 0, 1, 1, 0, .5f, 1.4f),           // Heavy concussion.
        (3, 1) => new(3, 1, 2, .35f, 1.2f, .4f, 1.2f),     // Paired pressure waves.
        (3, 2) => new(3, 2, 3, .6f, 2.1f, .35f, .9f),      // Rippling blasts.
        _ => new(3, 3, 4, .3f, .8f, .45f, 1.1f),           // Rolling detonations.
    };
}
