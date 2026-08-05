using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Aura;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedAuraSystem))]
public sealed partial class AuraComponent : Component
{
    [DataField, AutoNetworkedField]
    public Color Color;

    [DataField, AutoNetworkedField]
    public bool Flash;

    [DataField, AutoNetworkedField]
    public float FlashFrequency = 4;

    [DataField, AutoNetworkedField]
    public float FlashMinAlpha = 0.2f;

    [DataField, AutoNetworkedField]
    public float FlashMaxAlpha = 1;

    [DataField, AutoNetworkedField]
    public TimeSpan? ExpiresAt;

    [DataField, AutoNetworkedField]
    public float OutlineWidth = 2;
}
