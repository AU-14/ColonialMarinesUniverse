using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Yautja;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class YautjaBadBloodGearChoiceComponent : Component
{
    [DataField, AutoNetworkedField]
    public List<YautjaGearKind> Choices = new()
    {
        YautjaGearKind.WristBlades,
        YautjaGearKind.Scimitar,
        YautjaGearKind.ChainGauntlet,
    };

    [DataField, AutoNetworkedField]
    public YautjaGearKind? PendingChoice;

    [DataField, AutoNetworkedField]
    public bool Chosen;
}
