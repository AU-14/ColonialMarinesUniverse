using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.StatusEffects;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class PainShockComponent : Component
{
    [DataField]
    public FixedPoint2 Pain;

    [DataField]
    public FixedPoint2 PainMax = 100;

    [DataField]
    public bool InShock;

    [DataField, AutoPausedField]
    public TimeSpan NextUpdate;

    [DataField, AutoPausedField]
    public TimeSpan NextUnconsciousRefresh;

    /// <summary>
    ///     Discrete tier derived from <see cref="Pain"/> before painkiller suppression.
    /// </summary>
    [DataField]
    public PainTier RawTier = PainTier.None;

    /// <summary>
    ///     Discrete tier after painkiller suppression. Player-facing pain
    ///     effects should use this or <see cref="SharedPainShockSystem.GetEffectiveTier"/>.
    /// </summary>
    [DataField, AutoNetworkedField]
    public PainTier Tier = PainTier.None;

    /// <summary>
    ///     Injury pressure floor. Untreated sources make pain drift toward this
    ///     target instead of always decaying to zero.
    /// </summary>
    [DataField]
    public FixedPoint2 PainTarget;

    /// <summary>
    ///     Event-driven cache of source rise rate. Refreshed on state changes
    ///     (fractures, organ damage, etc.) to avoid per-tick body walks.
    /// </summary>
    [DataField]
    public FixedPoint2 CachedRiseRate;

    public bool AccumulationRateDirty;

    [DataField, AutoPausedField]
    public TimeSpan LastEventRecompute;

    [DataField, AutoPausedField]
    public TimeSpan NextShockPulse;

    [DataField, AutoPausedField]
    public TimeSpan NextTierAlertRefresh;

    [DataField, AutoPausedField]
    public TimeSpan NextPainReflection;

    [DataField, AutoPausedField]
    public TimeSpan NextPainRelief;

    [DataField, AutoNetworkedField]
    public int ShockPulseSerial;
}
