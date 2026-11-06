using System.Collections.Generic;
using Content.Shared.CMU14.Dropship.Integrity;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Dropship.AttachmentPoint;

/// <summary>Added to the helmet while its crew HUD is lowered.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUGunshipCrewVisorComponent : Component;

/// <summary>Server snapshot of the wearer's linked ship, including guns outside their PVS.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUGunshipCrewHudComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Dropship;

    [DataField, AutoNetworkedField]
    public string GunType = string.Empty;

    [DataField, AutoNetworkedField]
    public int Ammo;

    [DataField, AutoNetworkedField]
    public int Capacity;

    [DataField, AutoNetworkedField]
    public List<CMUGunshipCrewGunStatus> OtherGuns = new();

    [DataField, AutoNetworkedField]
    public float Integrity;

    [DataField, AutoNetworkedField]
    public float MaxIntegrity;

    [DataField, AutoNetworkedField]
    public List<DropshipMalfunction> Malfunctions = new();

    [DataField, AutoNetworkedField]
    public List<DropshipAlarm> Alarms = new();

    [DataField, AutoNetworkedField]
    public bool MasterAlarmSilenced;
}

[DataRecord, Serializable, NetSerializable]
public readonly partial record struct CMUGunshipCrewGunStatus(Direction Side, string GunType, int Ammo, int Capacity);
