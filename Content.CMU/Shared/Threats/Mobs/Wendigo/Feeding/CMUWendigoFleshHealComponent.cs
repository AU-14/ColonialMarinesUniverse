using Content.Shared.Damage;

namespace Content.Shared.CMU14.Threats.Mobs.Wendigo.Feeding;

/// <summary>
/// A Wendigo that heals when it finishes eating a piece of human flesh: carved human meat, a human limb or a human organ.
/// Handled server side by CMUWendigoFleshHealSystem.
/// </summary>
[RegisterComponent]
public sealed partial class CMUWendigoFleshHealComponent : Component
{
    /// <summary>
    /// Damage applied (negative heals) for every finished piece of human flesh.
    /// </summary>
    [DataField]
    public DamageSpecifier Heal = new()
    {
        DamageDict =
        {
            ["Blunt"] = -50,
            ["Slash"] = -50,
            ["Piercing"] = -50,
        },
    };

    /// <summary>
    /// How much bleeding a finished piece of human flesh stops, for Wendigos with a bloodstream.
    /// </summary>
    [DataField]
    public float BleedReduction = 10f;

    [DataField]
    public LocId Popup = "cmu-wendigo-flesh-heal";
}
