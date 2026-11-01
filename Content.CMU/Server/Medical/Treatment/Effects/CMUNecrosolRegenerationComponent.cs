using Content.Shared.FixedPoint;

namespace Content.Server.CMU14.Medical.Treatment.Effects;

/// <summary>
/// Tracks a patient's Necrosol dose toward the next missing limb regeneration.
/// </summary>
[RegisterComponent]
public sealed partial class CMUNecrosolRegenerationComponent : Component
{
    public FixedPoint2 AccumulatedUnits;
}
