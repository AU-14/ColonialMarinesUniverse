using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Marines.LastStand;

/// <summary>
/// Tunables for the Last Stand ability. Edit the <c>CMUDefaultLastStand</c> prototype in
/// <c>Content.CMU/Resources/Prototypes/CMU14/Marines/last_stand.yml</c> to rebalance it, or point a
/// job's <see cref="CMULastStandComponent"/> at a different prototype for per-role tuning.
/// </summary>
[Prototype("cmuLastStand")]
public sealed partial class CMULastStandPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Gun damage is multiplied by this while the ability is active.
    /// </summary>
    [DataField]
    public FixedPoint2 DamageMultiplier = 2.0;

    /// <summary>
    /// Added to the weapon accuracy multiplier while the ability is active (0.25 = +25%).
    /// </summary>
    [DataField]
    public FixedPoint2 AccuracyBonus = 0.25;

    /// <summary>
    /// How long the surge lasts.
    /// </summary>
    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Cooldown between uses, started on activation.
    /// </summary>
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Replaces <see cref="Cooldown"/> when the user dies while the ability is active,
    /// counted from the moment of activation.
    /// </summary>
    [DataField]
    public TimeSpan DeathCooldown = TimeSpan.FromMinutes(5);
}
