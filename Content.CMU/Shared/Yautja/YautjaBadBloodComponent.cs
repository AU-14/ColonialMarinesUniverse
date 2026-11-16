using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Yautja;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class YautjaBadBloodComponent : Component
{
    // not networked: only spaces out the "your hands reject it" popup while a trigger is held
    [ViewVariables]
    public TimeSpan NextWeaponRefusePopup;

    /// <summary>
    /// Speed multiplier while pulling or carrying a mob. Honourable hunters drag at full speed, Bad Bloods crawl so
    /// marines can catch them before a kill reaches the Lodge.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float DragSpeedMultiplier = 0.35f;
}
