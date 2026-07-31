using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Armor.ThermalCloak;

/// <summary>Authority for dynamic move opacity state, when cloak is active</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ThermalCloakUserComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Opacity;

    [DataField, AutoNetworkedField]
    public float MovingOpacity;

    [DataField, AutoNetworkedField]
    public float CurrentOpacity;

    [DataField, AutoNetworkedField]
    public float LerpSpeed;
}
