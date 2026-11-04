namespace Content.Shared.CMU14.util;

/// <summary>
/// Stamps when the loose-item sweep first saw this entity on the ground, so slow
/// despawns can age entities without per-entity timers. Server-only bookkeeping,
/// never networked.
/// </summary>
[RegisterComponent]
public sealed partial class CMULooseSinceComponent : Component
{
    /// <summary>Game time the entity was first seen loose; zero until stamped.</summary>
    public TimeSpan FirstSeen;
}
