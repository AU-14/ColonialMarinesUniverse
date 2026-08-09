using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Xenonids.Hive;

public sealed partial class HiveComponent
{
    [DataField, AutoNetworkedField]
    public bool BurrowedLarvaEnabled = true;
}
