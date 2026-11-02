using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Fob;

/// <summary>Deployable CMSS13 FOB machinery; server owns deployment, power and consumption.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentPause]
public sealed partial class CMUFobEquipmentComponent : Component
{
    [DataField] public CMUFobEquipmentKind Kind;
    [DataField] public string StowedState = string.Empty;
    [DataField] public string OffState = string.Empty;
    [DataField] public string OnState = string.Empty;
    [DataField] public float SensorRange = 7;
    [DataField] public DamageSpecifier ShockDamage = new() { DamageDict = new() { ["Shock"] = 12 } };
    [ViewVariables] public bool Deployed;
    [ViewVariables] public bool Powered;
    [ViewVariables] public bool Spent;
    [ViewVariables] public bool Charging;
    [ViewVariables] public EntityUid? Network;
    [ViewVariables] public DoAfterId? Work;
    [ViewVariables, AutoPausedField] public TimeSpan NextShock;
}

[Serializable, NetSerializable]
public enum CMUFobEquipmentKind : byte { Illuminator, Sensor, Fence, Generator }

[Serializable, NetSerializable]
public enum CMUFobEquipmentVisuals : byte { State }

[Serializable, NetSerializable]
public sealed partial class CMUFobEquipmentDoAfterEvent : SimpleDoAfterEvent
{
    public bool Deploy;
    public bool Generator;
}
