using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Yautja.BadBlood;

/// <summary>
/// ARCH-02: an armory ware that forges the buyer's bracer weapon, tier II of the weapon tiers (starter → forged →
/// golden). The armory deletes the ware and puts <see cref="BadBloodForgedComponent"/> on the bracer weapon.
/// </summary>
[RegisterComponent]
public sealed partial class BadBloodForgeUpgradeComponent : Component
{
    /// <summary>
    /// What the forged weapon's damage is multiplied by.
    /// </summary>
    [DataField]
    public float Multiplier = 1.2f;
}

/// <summary>
/// A forged bracer weapon: every swing deals <see cref="Multiplier"/> times its damage.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BadBloodForgedComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Multiplier = 1.2f;
}
