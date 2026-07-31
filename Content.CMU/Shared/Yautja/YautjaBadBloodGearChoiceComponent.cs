using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Yautja;

[RegisterComponent, NetworkedComponent]
public sealed partial class YautjaBadBloodGearChoiceComponent : Component
{
    [DataField]
    public List<YautjaGearKind> Choices = new()
    {
        YautjaGearKind.WristBlades,
        YautjaGearKind.Scimitar,
        YautjaGearKind.ChainGauntlet,
    };

    [DataField]
    public YautjaGearKind? PendingChoice;

    [DataField]
    public bool Chosen;
}
