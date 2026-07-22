using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Weapons.Ranged.Prediction;

/// <summary>
/// Sends authoritative impact feedback to the predicting shooter. The client
/// correlates this with its local collision so sound, flash, and damage popup
/// are shown exactly once regardless of which result arrives first.
/// </summary>
[Serializable, NetSerializable]
public sealed class PredictedProjectileImpactFeedbackEvent(
    int projectile,
    NetEntity target,
    NetCoordinates coordinates,
    DamageSpecifier? damage,
    FixedPoint2? damageTotal,
    SoundSpecifier? impactSound,
    bool varyPitch,
    bool flashTarget,
    bool deleteOnCollide,
    bool projectileSpent) : EntityEventArgs
{
    public readonly int Projectile = projectile;
    public readonly NetEntity Target = target;
    public readonly NetCoordinates Coordinates = coordinates;
    public readonly DamageSpecifier? Damage = damage;
    public readonly FixedPoint2? DamageTotal = damageTotal;
    public readonly SoundSpecifier? ImpactSound = impactSound;
    public readonly bool VaryPitch = varyPitch;
    public readonly bool FlashTarget = flashTarget;
    public readonly bool DeleteOnCollide = deleteOnCollide;
    public readonly bool ProjectileSpent = projectileSpent;
}
