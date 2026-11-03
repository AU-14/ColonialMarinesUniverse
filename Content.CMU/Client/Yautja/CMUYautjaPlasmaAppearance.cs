using Content.Shared.CMU14.Yautja;

namespace Content.Client.CMU14.Yautja;

/// <summary>One palette for the caster's projectile, muzzle, impact and world illumination.</summary>
internal readonly record struct CMUYautjaPlasmaAppearance(Color Glow, Color Shell, Color Core,
    float Length, float Width, float Trail, float Scale)
{
    private static readonly CMUYautjaPlasmaAppearance Stun = new(
        Color.FromHex("#42B8FF"), Color.FromHex("#8ADFFF"), Color.FromHex("#E8FBFF"), 7, 2, 10, .65f);
    private static readonly CMUYautjaPlasmaAppearance Immobilizer = new(
        Color.FromHex("#298DFF"), Color.FromHex("#66C7FF"), Color.FromHex("#E0F6FF"), 10, 3, 16, .9f);
    private static readonly CMUYautjaPlasmaAppearance Lethal = new(
        Color.FromHex("#FF392C"), Color.FromHex("#FF7962"), Color.FromHex("#FFF2DD"), 13, 2.5f, 22, 1);
    private static readonly CMUYautjaPlasmaAppearance Eradicator = new(
        Color.FromHex("#FF241A"), Color.FromHex("#FF5C3B"), Color.FromHex("#FFE3B0"), 17, 5, 28, 1.5f);

    public static CMUYautjaPlasmaAppearance For(CMUYautjaPlasmaMode mode) => mode switch
    {
        CMUYautjaPlasmaMode.Stun => Stun,
        CMUYautjaPlasmaMode.Immobilizer => Immobilizer,
        CMUYautjaPlasmaMode.Eradicator => Eradicator,
        _ => Lethal,
    };
}
