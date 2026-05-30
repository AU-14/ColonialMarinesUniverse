using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._RMC14.TacticalMap;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true), AutoGenerateComponentPause]
[Access(typeof(SharedTacticalMapSystem))]
public sealed partial class TacticalMapUserComponent : Component
{
    public override bool SendOnlyToOwner => true;

    [DataField(required: true), AutoNetworkedField]
    public EntProtoId ActionId;

    [DataField, AutoNetworkedField]
    public EntityUid? Action;

    [DataField, AutoNetworkedField]
    public EntityUid? Map;

    [DataField, AutoNetworkedField]
    public bool LiveUpdate;

    [DataField, AutoNetworkedField]
    public bool Marines;

    [DataField, AutoNetworkedField]
    public bool Xenos;

    [DataField, AutoNetworkedField]
    public bool Opfor;

    [DataField, AutoNetworkedField]
    public bool Govfor;

    [DataField, AutoNetworkedField]
    public bool Clf;

    [DataField, AutoNetworkedField]
    public Dictionary<int, TacticalMapBlip> MarineBlips = new();

    [DataField, AutoNetworkedField]
    public Dictionary<int, TacticalMapBlip> XenoBlips = new();

    [DataField, AutoNetworkedField]
    public Dictionary<int, TacticalMapBlip> XenoStructureBlips = new();

    [DataField, AutoNetworkedField]
    public Dictionary<int, TacticalMapBlip> OpforBlips = new();

    [DataField, AutoNetworkedField]
    public Dictionary<int, TacticalMapBlip> GovforBlips = new();

    [DataField, AutoNetworkedField]
    public Dictionary<int, TacticalMapBlip> ClfBlips = new();

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan LastAnnounceAt;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NextAnnounceAt;

    [DataField, AutoNetworkedField]
    public bool CanDraw;

    [DataField, AutoNetworkedField]
    public bool HasSquad;

    [DataField, AutoNetworkedField]
    public Dictionary<int, TacticalMapBlip> SquadBlips = new();

    [DataField, AutoNetworkedField]
    public List<TacticalMapLine> SquadLines = new();

    [DataField, AutoNetworkedField]
    public Dictionary<Vector2i, string> SquadLabels = new();

    [DataField, AutoNetworkedField]
    public SoundSpecifier Sound = new SoundCollectionSpecifier("XenoQueenCommand", AudioParams.Default.WithVolume(-6));


    [DataField, AutoNetworkedField]
    public EntityUid? Controller;
}
