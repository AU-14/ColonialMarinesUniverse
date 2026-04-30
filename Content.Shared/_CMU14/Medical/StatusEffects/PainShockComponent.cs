using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.StatusEffects;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class PainShockComponent : Component
{
    [DataField, AutoNetworkedField]
    public FixedPoint2 Pain;

    [DataField, AutoNetworkedField]
    public FixedPoint2 PainMax = 100;

    [DataField, AutoNetworkedField]
    public bool InShock;

    [DataField, AutoPausedField]
    public TimeSpan NextUpdate;

    [DataField, AutoPausedField]
    public TimeSpan NextUnconsciousRefresh;

    /// <summary>
    ///     Discrete tier derived from <see cref="Pain"/> with hysteresis.
    /// </summary>
    [DataField, AutoNetworkedField]
    public PainTier Tier = PainTier.None;

    /// <summary>
    ///     Event-driven cache of accumulation rate. Refreshed on state changes
    ///     (fractures, organ damage, etc.) to avoid per-tick body walks.
    /// </summary>
    [DataField, AutoNetworkedField]
    public FixedPoint2 CachedAccumulationRate;

    public bool AccumulationRateDirty;

    [DataField, AutoPausedField]
    public TimeSpan LastEventRecompute;
}
