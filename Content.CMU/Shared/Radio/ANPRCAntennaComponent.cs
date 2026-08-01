using Robust.Shared.GameStates;

namespace Content.Shared._AU14.Radio;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ANPRCAntennaComponent : Component
{
    [DataField, AutoNetworkedField]
    public string Label = "WHIP";

    [DataField, AutoNetworkedField]
    public float FullRange = 30f;

    [DataField, AutoNetworkedField]
    public float PartialRange = 45f;

    [DataField, AutoNetworkedField]
    public bool RequiresStationary;

    [DataField, AutoNetworkedField]
    public float MovingRangeMultiplier = 1f;
}
