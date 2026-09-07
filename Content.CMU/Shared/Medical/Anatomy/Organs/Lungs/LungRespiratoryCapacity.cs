using Content.Shared.FixedPoint;

namespace Content.Shared.CMU14.Medical.Anatomy.Organs.Lungs;

/// <summary>Current respiratory capacity supplied by one best attached lung.</summary>
public readonly record struct LungRespiratoryCapacity(
    EntityUid Organ,
    float Efficiency,
    OrganDamageStage Stage,
    FixedPoint2 AsphyxiationPerSecond);
