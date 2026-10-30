using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Traits.MotionSick;

/// <summary>
///     Motion sickness: riding in a flying dropship, being inside or on top of a moving vehicle, or floating in zero-g
///     for too long makes this mob a little dizzy, now and then sick enough to puke. Each time they puke the dizziness gets worse, until they've been back
///     on solid ground for a while.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUMotionSickComponent : Component
{
    [DataField]
    public TimeSpan CheckInterval = TimeSpan.FromSeconds(2);

    /// <summary>How long the dizziness lasts each check while in motion. Short, so the screen only sways a little.</summary>
    [DataField]
    public TimeSpan BaseDizziness = TimeSpan.FromSeconds(4);

    /// <summary>Extra dizziness for every time they've puked this trip.</summary>
    [DataField]
    public TimeSpan DizzinessPerPuke = TimeSpan.FromSeconds(4);

    [DataField]
    public TimeSpan MaxDizziness = TimeSpan.FromSeconds(20);

    /// <summary>Chance to puke for every <see cref="PukeInterval"/> spent in motion.</summary>
    [DataField]
    public float PukeChance = 0.05f;

    [DataField]
    public TimeSpan PukeInterval = TimeSpan.FromMinutes(2);

    /// <summary>Chance per check of a queasy message.</summary>
    [DataField]
    public float MessageChance = 0.06f;

    /// <summary>How long on solid ground before the stomach settles and the puke count resets.</summary>
    [DataField]
    public TimeSpan RecoveryTime = TimeSpan.FromSeconds(60);

    /// <summary>How long they can float weightless before it starts to turn their stomach.</summary>
    [DataField]
    public TimeSpan ZeroGGrace = TimeSpan.FromSeconds(30);

    /// <summary>When they last had weight under them, or null if they're weightless now and haven't since.</summary>
    public TimeSpan? WeightlessSince;

    public TimeSpan NextCheck;
    public TimeSpan LastInMotion;
    public int Pukes;

    /// <summary>Time in motion since the last puke roll.</summary>
    public TimeSpan Riding;
}
