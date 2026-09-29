using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Xenonids.Despoiler;

[RegisterComponent]
public sealed partial class XenoDespoilerAcidBarrageActionComponent : Component
{
    [DataField]
    public float MaxChargeSeconds = 5f;

    [DataField]
    public int MinProjectiles = 1;

    [DataField]
    public int MaxProjectiles = 10;

    [DataField]
    public int EmpowerBonusProjectiles = 5;

    [DataField]
    public float ScatterDegrees = 60f;

    [DataField]
    public float ChargingSpeedMultiplier = 0.4f;

    [DataField]
    public EntProtoId ProjectileId = "RMCProjectileDespoilerAcidShot";

    [DataField]
    public float LingeringAcidChance = 1.0f;

    [DataField]
    public float ProjectileSpeed = 12f;

    [DataField]
    public int MinRangeTiles = 2;

    [DataField]
    public int MaxRangeTiles = 6;

    [DataField]
    public float MinProjectileScale = 0.9f;

    [DataField]
    public float MaxProjectileScale = 1.33f;

    [DataField]
    public TimeSpan PostFireCooldown = TimeSpan.FromSeconds(15);

    [DataField]
    public TimeSpan ChargeGracePeriod = TimeSpan.FromSeconds(30);

    [DataField]
    public SoundSpecifier? ChargeSound;

    [DataField]
    public SoundSpecifier? FireSound;
}
