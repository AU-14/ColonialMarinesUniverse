using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Guidance;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMULaserCodeComponent : Component
{
    public const string Default = "1111-1";

    [DataField, AutoNetworkedField]
    public string Code = Default;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUMunitionGuidanceComponent : Component
{
    [DataField, AutoNetworkedField]
    public CMUGuidanceMode Mode;

    [DataField, AutoNetworkedField]
    public bool GpsOnly;

    [DataField, AutoNetworkedField]
    public MapCoordinates? Coordinates;

    [DataField, AutoNetworkedField]
    public Vector2i? PlanetCoordinates;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class CMUGuidanceControlComponent : Component;

[Serializable, NetSerializable]
public enum CMUGuidanceMode : byte { Laser, Gps }

[Serializable, NetSerializable]
public enum CMUGuidanceUi : byte { Key }

[Serializable, NetSerializable]
public sealed record CMUGuidanceAmmo(NetEntity Id, string Name, CMUGuidanceMode Mode, bool GpsOnly,
    NetEntity? Map, int X, int Y);

[Serializable, NetSerializable]
public sealed record CMUGuidanceReport(int Id, string Observer, string Map, int X, int Y);

[Serializable, NetSerializable]
public sealed class CMUGuidanceState(string? receiverCode, string? designatorCode,
    List<CMUGuidanceAmmo> ammo, List<CMUGuidanceReport> reports) : BoundUserInterfaceState
{
    public readonly string? ReceiverCode = receiverCode;
    public readonly string? DesignatorCode = designatorCode;
    public readonly List<CMUGuidanceAmmo> Ammo = ammo;
    public readonly List<CMUGuidanceReport> Reports = reports;
}

[Serializable, NetSerializable]
public sealed class CMUSetLaserCodeMessage(string code, bool designator) : BoundUserInterfaceMessage
{
    public readonly string Code = code;
    public readonly bool Designator = designator;
}

[Serializable, NetSerializable]
public sealed class CMUProgramGpsMessage(NetEntity ammo, CMUGuidanceMode mode, int? reportId)
    : BoundUserInterfaceMessage
{
    public readonly NetEntity Ammo = ammo;
    public readonly CMUGuidanceMode Mode = mode;
    public readonly int? ReportId = reportId;
}

[Serializable, NetSerializable]
public sealed class CMUOpenGuidanceMessage : BoundUserInterfaceMessage;
