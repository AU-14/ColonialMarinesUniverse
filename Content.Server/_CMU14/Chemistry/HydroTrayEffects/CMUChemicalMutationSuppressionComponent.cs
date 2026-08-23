using Robust.Shared.Timing;

namespace Content.Server._CMU14.Chemistry.HydroTrayEffects;

[RegisterComponent]
public sealed partial class CMUChemicalMutationSuppressionComponent : Component
{
    [DataField]
    public TimeSpan ExpiresAt;
}
