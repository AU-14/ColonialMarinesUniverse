using System;
using System.Collections.Generic;
using Content.Shared.Damage.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Tools;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Vehicle;

[RegisterComponent, NetworkedComponent]
[Access(typeof(HardpointSystem), typeof(HardpointSlotSystem))]
public sealed partial class HardpointItemComponent : Component
{
    public const string ComponentId = "HardpointItem";

    [DataField(required: true)]
    public string HardpointType = string.Empty;

    [DataField]
    public ProtoId<HardpointVehicleFamilyPrototype>? VehicleFamily;

    [DataField]
    public ProtoId<HardpointSlotTypePrototype>? SlotType;

    [DataField]
    public string? CompatibilityId;

    [DataField]
    public float DamageMultiplier = 1f;

    [DataField]
    public float RepairRate = 0.01f;

    [DataField]
    public float DisabledIntegrityFraction = 0.15f;

    [DataField]
    public float MinimumPerformanceMultiplier = 0.35f;
}


[RegisterComponent, NetworkedComponent]
[Access(typeof(HardpointSystem), typeof(HardpointSlotSystem))]
public sealed partial class HardpointSlotsComponent : Component
{
    [DataField]
    public ProtoId<HardpointVehicleFamilyPrototype>? VehicleFamily;

    [DataField(required: true)]
    public List<HardpointSlot> Slots = new();

    [DataField]
    public float FrameDamageFractionWhileIntact = 0.25f;

    [DataField]
    public ProtoId<ToolQualityPrototype> RemoveToolQuality = "VehicleServicing";
}

[RegisterComponent]
[Access(typeof(HardpointSystem), typeof(HardpointSlotSystem))]
public sealed partial class HardpointStateComponent : Component
{
    [NonSerialized]
    public HashSet<string> PendingInserts = new();

    [NonSerialized]
    public HashSet<string> CompletingInserts = new();

    [NonSerialized]
    public HashSet<string> PendingRemovals = new();

    [NonSerialized]
    public HashSet<EntityUid> PendingInsertUsers = new();

    [NonSerialized]
    public string? LastUiError;
}

[Serializable, NetSerializable, DataDefinition]
public sealed partial class HardpointSlot
{
    [DataField(required: true)]
    public string Id { get; set; } = string.Empty;

    [DataField(required: true)]
    public string HardpointType { get; set; } = string.Empty;

    [DataField]
    public ProtoId<HardpointSlotTypePrototype>? SlotType { get; set; }

    [DataField]
    public string? CompatibilityId { get; set; }

    [DataField]
    public string VisualLayer { get; set; } = string.Empty;

    [DataField]
    public bool Required { get; set; } = true;

    [DataField]
    public float InsertDelay { get; set; } = 1f;

    [DataField]
    public float RemoveDelay { get; set; } = -1f;

    [DataField]
    public bool DisableEject { get; set; }

    [DataField]
    public EntityWhitelist? Whitelist { get; set; }
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HardpointIntegrityComponent : Component
{
    [DataField, AutoNetworkedField]
    public float MaxIntegrity = 100f;

    [DataField, AutoNetworkedField]
    public float Integrity;

    [DataField]
    public FixedPoint2 RepairFuelCost = FixedPoint2.New(5);

    [DataField]
    public SoundSpecifier? RepairSound;

    [DataField]
    public ProtoId<ToolQualityPrototype> RepairToolQuality = "Welding";

    [DataField]
    public ProtoId<ToolQualityPrototype> FrameFinishToolQuality = "Anchoring";

    [DataField]
    public float FrameWeldCapFraction = 0.75f;

    [DataField]
    public float FrameRepairEpsilon = 0.01f;

    [DataField]
    public float RepairChunkFraction = 0.05f;

    [DataField]
    public float RepairChunkMinimum = 0.01f;

    [DataField]
    public float FrameRepairChunkSeconds = 2f;

    [DataField, AutoNetworkedField]
    public bool BypassEntryOnZero;

    [NonSerialized]
    public bool Repairing;
}

[RegisterComponent]
public sealed partial class HardpointDamageModifierComponent : Component
{
    [DataField("modifierSets")]
    public List<ProtoId<DamageModifierSetPrototype>> ModifierSets = new();
}

[Serializable, NetSerializable]
public sealed partial class HardpointInsertDoAfterEvent : DoAfterEvent
{
    [DataField(required: true)]
    public string SlotId = string.Empty;

    public HardpointInsertDoAfterEvent()
    {
    }

    public HardpointInsertDoAfterEvent(string slotId)
    {
        SlotId = slotId;
    }

    public override DoAfterEvent Clone()
    {
        return new HardpointInsertDoAfterEvent(SlotId);
    }

    public override bool IsDuplicate(DoAfterEvent other)
    {
        return other is HardpointInsertDoAfterEvent hardpoint
               && hardpoint.SlotId == SlotId;
    }
}

[Serializable, NetSerializable]
public sealed partial class HardpointRemoveDoAfterEvent : DoAfterEvent
{
    [DataField(required: true)]
    public string SlotId = string.Empty;

    public HardpointRemoveDoAfterEvent()
    {
    }

    public HardpointRemoveDoAfterEvent(string slotId)
    {
        SlotId = slotId;
    }

    public override DoAfterEvent Clone()
    {
        return new HardpointRemoveDoAfterEvent(SlotId);
    }

    public override bool IsDuplicate(DoAfterEvent other)
    {
        return other is HardpointRemoveDoAfterEvent remove
               && remove.SlotId == SlotId;
    }
}

[Serializable, NetSerializable]
public sealed partial class HardpointRepairDoAfterEvent : DoAfterEvent
{
    public override DoAfterEvent Clone()
    {
        return new HardpointRepairDoAfterEvent();
    }
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(HardpointSystem), typeof(VehicleWeaponsSystem))]
public sealed partial class VehicleHardpointFailureComponent : Component
{
    [DataField, AutoNetworkedField]
    public List<VehicleHardpointFailure> ActiveFailures = new();

    [DataField]
    public int MaxActiveFailures = 2;

    [NonSerialized]
    public Dictionary<VehicleHardpointFailure, bool> Repairing = new();

    [NonSerialized]
    public Dictionary<VehicleHardpointFailure, int> RepairProgress = new();

    [NonSerialized]
    public TimeSpan NextRunawayFireAt = TimeSpan.Zero;
}

[Serializable, NetSerializable]
public enum VehicleHardpointFailure : byte
{
    ArmorCompromised,
    FeedJam,
    RunawayTrigger,
    TurretTraverseDamage,
    EngineMisfire,
    TransmissionSlip,
    WarpedFrame,
    DamagedMount,
}

[Serializable, NetSerializable]
public sealed partial class VehicleHardpointFailureRepairDoAfterEvent : DoAfterEvent
{
    [DataField(required: true)]
    public VehicleHardpointFailure Failure;

    [DataField]
    public int Step;

    public VehicleHardpointFailureRepairDoAfterEvent()
    {
    }

    public VehicleHardpointFailureRepairDoAfterEvent(VehicleHardpointFailure failure, int step)
    {
        Failure = failure;
        Step = step;
    }

    public override DoAfterEvent Clone()
    {
        return new VehicleHardpointFailureRepairDoAfterEvent(Failure, Step);
    }
}

public readonly record struct HardpointSlotsChangedEvent(EntityUid Vehicle);
