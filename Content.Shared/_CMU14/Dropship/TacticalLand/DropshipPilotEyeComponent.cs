using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Dropship.TacticalLand;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedDropshipTacticalLandSystem))]
public sealed partial class DropshipPilotEyeComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Pilot;

    [DataField, AutoNetworkedField]
    public EntityUid? Console;

    [DataField, AutoNetworkedField]
    public Vector2i Footprint = new(11, 21);

    [DataField, AutoNetworkedField]
    public byte RotationQuarterTurns;

    [DataField, AutoNetworkedField]
    public List<Vector2i> BlockedTiles = new();

    [DataField, AutoNetworkedField]
    public bool ClearForLanding;

    // Tactical-landing previews update several times per second, but their
    // hull geometry only changes when the source grid or quarter-turn changes.
    public EntityUid? CachedFootprintGrid;
    public byte CachedFootprintRotation = byte.MaxValue;
    public readonly List<Vector2i> CachedFootprintOffsets = new();

    // Landing-zone markers are static for almost every preview tick. The
    // owning system invalidates this set when a destination moves or changes.
    public EntityUid? CachedDestinationGrid;
    public int CachedDestinationRevision = -1;
    public readonly HashSet<Vector2i> CachedDestinationTiles = new();

    // Reuse the validation buffer; allocate a new networked list only when the
    // blocked result actually changes.
    public readonly List<Vector2i> BlockedTilesScratch = new();
}
