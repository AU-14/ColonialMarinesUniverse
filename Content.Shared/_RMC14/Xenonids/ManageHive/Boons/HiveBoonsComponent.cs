using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Xenonids.ManageHive.Boons;

[RegisterComponent, NetworkedComponent]
[Access(typeof(HiveBoonSystem))]
public sealed partial class HiveBoonsComponent : Component
{
    [DataField]
    public int RoyalResin;

    [DataField]
    public int RoyalResinMax = 10;

    [DataField]
    public Dictionary<EntProtoId<HiveBoonDefinitionComponent>, TimeSpan> UnlockAt = new();

    [DataField]
    public Dictionary<EntProtoId<HiveBoonDefinitionComponent>, TimeSpan> UsedAt = new();

    [DataField]
    public Dictionary<EntProtoId<HiveBoonDefinitionComponent>, EntityUid> Active = new();

    [DataField]
    public bool KingAnnounced;
}
