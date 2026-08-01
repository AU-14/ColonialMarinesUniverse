using Content.Shared.Radio;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Shared._AU14.Radio;

[RegisterComponent]
public sealed partial class ANPRCRelayAnchorComponent : Component
{
    [DataField]
    public HashSet<ProtoId<RadioChannelPrototype>> Channels = new();

    [DataField]
    public float FullRange = 30f;

    [DataField]
    public float PartialRange = 45f;

    [DataField]
    public bool RequiresStationary;

    [DataField]
    public float RangeMultiplier = 1f;

    [DataField]
    public float MovingRangeMultiplier = 1f;

    [DataField]
    public bool Planted;

    // how many z-levels of vertical separation this anchor still covers within its
    // z network. 0 = own level only, -1 = every level (whole ship / whole site)
    [DataField]
    public int LevelReach = 1;
}
