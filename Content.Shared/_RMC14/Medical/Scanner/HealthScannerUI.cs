using Content.Shared.Chemistry.Components;
using Content.Shared.FixedPoint;
using Content.Shared._CMU14.Medical.Anatomy.Bones;
using Content.Shared._CMU14.Medical.Anatomy.Organs;
using Content.Shared._CMU14.Medical.Injuries.Wounds;
using Content.Shared.Body.Part;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Medical.Scanner;

/// <summary>
///     Pure scan state — no BUI dependency. Used by body scanner snapshots, stored medical
///     records, and as the payload inside <see cref="HealthScannerBuiState"/>.
/// </summary>
[DataRecord, Serializable, NetSerializable]
public readonly partial record struct HealthScanState(
    NetEntity Target,
    FixedPoint2 Blood,
    FixedPoint2 MaxBlood,
    float? Temperature,
    string Pulse,
    Solution? Chemicals,
    bool Bleeding,
    HealthScanDetailLevel DetailLevel);

/// <summary>
///     Thin BUI wrapper around <see cref="HealthScanState"/> for the health analyzer live-update path.
/// </summary>
[Serializable, NetSerializable]
public sealed class HealthScannerBuiState(HealthScanState scanState) : BoundUserInterfaceState
{
    public readonly HealthScanState ScanState = scanState;
    public Dictionary<BodyPartType, CMUBodyPartReadout>? CMUParts;
    public List<CMUOrganReadout>? CMUOrgans;
    public List<CMUFractureReadout>? CMUFractures;
    public List<CMUInternalBleedReadout>? CMUInternalBleeds;
    public int? CMUHeartBpm;
    public bool? CMUHeartStopped;
    public CMUPainShockRisk? CMUPainShockRisk;
    public bool CMUPainShockSuppressed;
    public bool CMUExternalBleeding;
    public bool CMUSyntheticPhysiology;
}

[Serializable, NetSerializable]
public readonly record struct CMUBodyPartReadout(
    BodyPartType Type,
    BodyPartSymmetry Symmetry,
    FixedPoint2 Current,
    FixedPoint2 Max,
    WoundSize? WoundDescriptor,
    WoundMechanism? WoundMechanism,
    FixedPoint2 WoundDamage,
    int ShrapnelFragments,
    float ShrapnelSeverity,
    bool Eschar,
    bool Splinted,
    bool Cast,
    bool Tourniquet);

[Serializable, NetSerializable]
public readonly record struct CMUOrganReadout(
    string OrganName,
    OrganDamageStage Stage,
    FixedPoint2 Current,
    FixedPoint2 Max,
    bool Removed);

[Serializable, NetSerializable]
public readonly record struct CMUFractureReadout(
    BodyPartType Part,
    BodyPartSymmetry Symmetry,
    FractureSeverity Severity,
    bool ExactSeverity,
    bool Suppressed);

[Serializable, NetSerializable]
public readonly record struct CMUInternalBleedReadout(
    BodyPartType Part,
    BodyPartSymmetry Symmetry,
    bool ExactLocationKnown,
    float BloodlossPerSecond);

[Serializable, NetSerializable]
public enum CMUPainShockRisk : byte
{
    Low,
    Elevated,
    High,
    Imminent,
    Active,
}

[Serializable, NetSerializable]
public enum HealthScannerUIKey
{
    Key
}

[Serializable, NetSerializable]
public enum HealthScanDetailLevel : byte
{
    HealthAnalyzer = 0,
    BodyScan = 1,
    Full = 2,
}
