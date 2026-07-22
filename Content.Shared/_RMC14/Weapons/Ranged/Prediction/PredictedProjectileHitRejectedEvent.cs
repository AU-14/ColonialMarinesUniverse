using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Weapons.Ranged.Prediction;

/// <summary>
/// Tells the predicting client that the server rejected a reported projectile hit.
/// The client can then reveal the still-authoritative projectile and allow its real
/// collision feedback to arrive normally.
/// </summary>
[Serializable, NetSerializable]
public sealed class PredictedProjectileHitRejectedEvent(int projectile) : EntityEventArgs
{
    public readonly int Projectile = projectile;
}
