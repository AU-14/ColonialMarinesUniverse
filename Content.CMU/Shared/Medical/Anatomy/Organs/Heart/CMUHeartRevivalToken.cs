using Robust.Shared.GameObjects;

namespace Content.Shared.CMU14.Medical.Anatomy.Organs.Heart;

/// <summary>
/// The exact cardiac tissue approved for a single revival attempt. A donor transplant or
/// component replacement during an effect must not transfer approval to the new heart.
/// </summary>
public sealed record CMUHeartRevivalToken(
    EntityUid Body,
    EntityUid Heart,
    HeartComponent HeartComponent,
    OrganHealthComponent HealthComponent);
