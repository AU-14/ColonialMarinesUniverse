using System;
using System.Collections.Generic;
using Content.Shared._CMU14.Medical.Injuries.Wounds;
using Content.Shared._RMC14.Medical.Wounds;
using Content.Shared.Body.Part;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Medical.Diagnostics.Examine;

/// <summary>
///     Compact body-level read model for public wound examine text. The raw
///     per-part ledger remains server-owned.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CMUMedicalExamineProjectionSystem))]
public sealed partial class CMUMedicalExamineProjectionComponent : Component
{
    [DataField, AutoNetworkedField]
    internal List<CMUMedicalExaminePartProjection> Parts = new();

    [DataField, AutoNetworkedField]
    internal FixedPoint2 BruteRemaining;

    [DataField, AutoNetworkedField]
    internal FixedPoint2 BurnRemaining;
}

[DataRecord]
[Serializable, NetSerializable]
public partial record struct CMUMedicalExaminePartProjection
{
    public BodyPartType Type { get; set; }
    public BodyPartSymmetry Symmetry { get; set; }
    public List<CMUMedicalVisibleWound> Wounds { get; set; } = new();
    public ExternalBleedTier ExternalBleeding { get; set; }

    public CMUMedicalExaminePartProjection()
    {
    }

    public CMUMedicalExaminePartProjection(
        BodyPartType type,
        BodyPartSymmetry symmetry,
        List<CMUMedicalVisibleWound> wounds,
        ExternalBleedTier externalBleeding)
    {
        Type = type;
        Symmetry = symmetry;
        Wounds = wounds;
        ExternalBleeding = externalBleeding;
    }
}

[DataRecord]
[Serializable, NetSerializable]
public partial record struct CMUMedicalVisibleWound
{
    public WoundType Type { get; set; }
    public WoundSize Size { get; set; }
    public FixedPoint2 Damage { get; set; }
    public WoundMechanism Mechanism { get; set; }
    public bool Treated { get; set; }
    public WoundCleanupFlags Cleanup { get; set; }

    public CMUMedicalVisibleWound()
    {
    }

    public CMUMedicalVisibleWound(
        WoundType type,
        WoundSize size,
        FixedPoint2 damage,
        WoundMechanism mechanism,
        bool treated,
        WoundCleanupFlags cleanup)
    {
        Type = type;
        Size = size;
        Damage = damage;
        Mechanism = mechanism;
        Treated = treated;
        Cleanup = cleanup;
    }
}
