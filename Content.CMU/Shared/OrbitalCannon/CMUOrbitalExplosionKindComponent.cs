using Content.Shared.CMU14.ZLevels.Ordnance;

namespace Content.Shared._RMC14.OrbitalCannon;

/// <summary>
/// Retains the permission gate selected by the legacy three-argument SpawnExplosion API.
/// </summary>
[RegisterComponent]
public sealed partial class CMUOrbitalExplosionKindComponent : Component
{
    public CMUTopDownOrdnanceKind Kind;
}
