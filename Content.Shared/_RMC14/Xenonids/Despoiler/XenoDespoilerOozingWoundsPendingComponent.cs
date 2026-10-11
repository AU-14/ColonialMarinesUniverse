using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Xenonids.Despoiler;

[RegisterComponent]
public sealed partial class XenoDespoilerOozingWoundsPendingComponent : Component
{
    public readonly List<XenoDespoilerOozingWoundsPendingTile> Pending = new();
}

public struct XenoDespoilerOozingWoundsPendingTile
{
    public required HashSet<EntityUid> HitTargets; // CMU14: shared by every delayed tile of one cast.
    public TimeSpan SpawnAt;
    public EntityCoordinates Tile;
    public EntProtoId SprayProto;
    public EntProtoId PuddleProto;
    public float PuddleChance;
}
