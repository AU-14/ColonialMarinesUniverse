using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._RMC14.TacticalMap;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentPause]
[Access(typeof(SharedTacticalMapSystem))]
public sealed partial class TacticalMapComponent : Component // CMU14 Class: Custom Blips/Lines & Ordering
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextUpdate = TimeSpan.FromSeconds(1);

    [DataField] public bool MapDirty;

    // Per-faction next-update timers so updates/announcements can be staggered independently
    [DataField]
    public Dictionary<string, TimeSpan> NextUpdatePerFaction = new()
    {
       ["MARINES"] = TimeSpan.FromSeconds(1),
       ["XENONIDS"] = TimeSpan.FromSeconds(1),
       ["OPFOR"] = TimeSpan.FromSeconds(1),
       ["GOVFOR"] = TimeSpan.FromSeconds(1),
       ["CLF"] = TimeSpan.FromSeconds(1),
       ["WEYU"] = TimeSpan.FromSeconds(1),
       ["YAUTJA"] = TimeSpan.FromSeconds(1), // CMU14
    };

    // Default: Marines (legacy)
    [DataField]
    public Dictionary<int, TacticalMapBlip> MarineBlips = new();
    [DataField]
    public List<TacticalMapLine> MarineLines = new();
    [DataField]
    public Dictionary<int, TacticalMapBlip> LastUpdateMarineBlips = new();
    [DataField]
    public Dictionary<Vector2i, string> MarineLabels = new();

    // Xeno
    [DataField]
    public Dictionary<int, TacticalMapBlip> XenoBlips = new();
    [DataField]
    public List<TacticalMapLine> XenoLines = new();
    [DataField]
    public Dictionary<int, TacticalMapBlip> XenoStructureBlips = new();
    [DataField]
    public Dictionary<int, TacticalMapBlip> LastUpdateXenoBlips = new();
    [DataField]
    public Dictionary<int, TacticalMapBlip> LastUpdateXenoStructureBlips = new();
    [DataField]
    public Dictionary<Vector2i, string> XenoLabels = new();

    // Opfor
    [DataField]
    public Dictionary<int, TacticalMapBlip> OpforBlips = new();
    [DataField]
    public Dictionary<int, TacticalMapBlip> LastUpdateOpforBlips = new();
    [DataField]
    public List<TacticalMapLine> OpforLines = new();
    [DataField]
    public Dictionary<Vector2i, string> OpforLabels = new();

    // Govfor
    [DataField]
    public Dictionary<int, TacticalMapBlip> GovforBlips = new();
    [DataField]
    public Dictionary<int, TacticalMapBlip> LastUpdateGovforBlips = new();
    [DataField]
    public List<TacticalMapLine> GovforLines = new();
    [DataField]
    public Dictionary<Vector2i, string> GovforLabels = new();

    // CLF
    [DataField]
    public Dictionary<int, TacticalMapBlip> ClfBlips = new();
    [DataField]
    public Dictionary<int, TacticalMapBlip> LastUpdateClfBlips = new();
    [DataField]
    public List<TacticalMapLine> ClfLines = new();
    [DataField]
    public Dictionary<Vector2i, string> ClfLabels = new();

    // Weyland-Yutani
    [DataField]
    public Dictionary<int, TacticalMapBlip> WeYuBlips = new();
    [DataField]
    public Dictionary<int, TacticalMapBlip> LastUpdateWeYuBlips = new();
    [DataField]
    public List<TacticalMapLine> WeYuLines = new();
    [DataField]
    public Dictionary<Vector2i, string> WeYuLabels = new();

    // Abominations
    [DataField]
    public Dictionary<int, TacticalMapBlip> AbominationBlips = new();

    [DataField] // CMU14
    public Dictionary<int, TacticalMapBlip> YautjaBlips = new();
}
