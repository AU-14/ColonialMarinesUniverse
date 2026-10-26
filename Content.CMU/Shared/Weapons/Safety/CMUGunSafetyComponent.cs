using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Weapons.Safety;

/// <summary>
/// A gun whose safety has been touched. Guns start without this, which means off safety; the right click toggle adds
/// it the first time and flips <see cref="On"/> after that. Give a gun prototype this with <c>on: true</c> to have it
/// spawn on safety.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CMUGunSafetySystem))]
public sealed partial class CMUGunSafetyComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool On;

    /// <summary>
    /// Holding the trigger tries to fire every tick, so the "safety is on" popup only shows this often.
    /// </summary>
    [DataField]
    public TimeSpan PopupCooldown = TimeSpan.FromSeconds(1);

    public TimeSpan NextPopup;
}
