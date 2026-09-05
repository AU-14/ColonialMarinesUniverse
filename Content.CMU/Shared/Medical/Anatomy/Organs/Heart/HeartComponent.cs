using System.Collections.Generic;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Medical.Anatomy.Organs.Heart;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedHeartSystem))]
public sealed partial class HeartComponent : Component
{
    [DataField, AutoNetworkedField]
    public int BeatsPerMinute = 70;

    [DataField, AutoNetworkedField]
    public bool Stopped;

    [DataField]
    public int MaxBpm = 200;

    /// <summary>
    ///     Below this intrinsic pulse floor the grace period starts; compensatory
    ///     display pulse from unrelated injuries cannot restore circulation. Failing
    ///     tissue uses at least 60 BPM without overwriting this configured floor. If still below
    ///     for the full <see cref="StopGracePeriod"/> it transitions to
    ///     <see cref="Stopped"/>.
    /// </summary>
    [DataField]
    public int MinBpmBeforeStop = 30;

    [DataField]
    public TimeSpan StopGracePeriod = TimeSpan.FromSeconds(30);

    /// <summary>
    ///     When did BPM first dip below <see cref="MinBpmBeforeStop"/>? Null while
    ///     above the floor.
    /// </summary>
    [DataField]
    public TimeSpan? BelowThresholdSince;

    /// <summary>
    ///     When did circulation fully stop? Used for collapse timing.
    /// </summary>
    [DataField]
    public TimeSpan? NoPulseSince;

    // The owner settles both heart-entity and patient pause boundaries. Generated
    // offsets on the heart alone cannot handle a paused patient with unpaused organs.
    [DataField]
    public TimeSpan LastPhysiologyUpdate;

    [DataField]
    public TimeSpan NextPulseUpdate;

    [DataField]
    public TimeSpan PulseUpdateInterval = TimeSpan.FromSeconds(5);

    [DataField]
    public TimeSpan NextCardiacArrestTick;

    [DataField]
    public FixedPoint2 CardiacArrestAsphyxPerSecond = FixedPoint2.New(6);

    [DataField]
    public TimeSpan CardiacArrestUnconsciousDelay = TimeSpan.FromSeconds(5);

    [DataField]
    public Dictionary<OrganDamageStage, FixedPoint2> AsphyxPerSecond = new()
    {
        { OrganDamageStage.Healthy, FixedPoint2.Zero },
        { OrganDamageStage.Bruised, FixedPoint2.New(0.1) },
        { OrganDamageStage.Damaged, FixedPoint2.New(0.5) },
        { OrganDamageStage.Failing, FixedPoint2.New(1.5) },
        { OrganDamageStage.Dead, FixedPoint2.Zero },
    };

    [DataField]
    public Dictionary<OrganDamageStage, FixedPoint2> ToxinPerSecond = new()
    {
        { OrganDamageStage.Healthy, FixedPoint2.Zero },
        { OrganDamageStage.Bruised, FixedPoint2.Zero },
        { OrganDamageStage.Damaged, FixedPoint2.New(0.5) },
        { OrganDamageStage.Failing, FixedPoint2.New(0.5) },
        { OrganDamageStage.Dead, FixedPoint2.Zero },
    };

    [DataField]
    public TimeSpan NextOrganDamageTick;

    [DataField]
    public OrganDamageStage PhysiologyStage;

    [DataField]
    public bool CriticalBloodVolume;

    // Source expiry follows the body's pause clock, not stasis or heart-entity pause.
    [DataField]
    public TimeSpan PacingUntil;

    [DataField]
    public double AsphyxRemainder;

    [DataField]
    public double ToxinRemainder;
}

[RegisterComponent]
[Access(typeof(SharedHeartSystem))]
public sealed partial class MissingHeartComponent : Component
{
    [DataField]
    public TimeSpan NoPulseElapsed;

    [DataField]
    public TimeSpan LastCardiacArrestUpdate;

    [DataField]
    public TimeSpan NextCardiacArrestTick;

    [DataField]
    public double AsphyxRemainder;
}
