using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Traits.MotionSick;

/// <summary>
///     Motion sickness: riding in a flying dropship, or inside or on top of a moving vehicle, makes this mob a little
///     dizzy, now and then sick enough to puke. Each time they puke the dizziness gets worse, until they've been back
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

    /// <summary>Chance per check to puke.</summary>
    [DataField]
    public float PukeChance = 0.015f;

    /// <summary>Chance per check of a queasy message.</summary>
    [DataField]
    public float MessageChance = 0.06f;

    /// <summary>How long on solid ground before the stomach settles and the puke count resets.</summary>
    [DataField]
    public TimeSpan RecoveryTime = TimeSpan.FromSeconds(60);

    public TimeSpan NextCheck;
    public TimeSpan LastInMotion;
    public int Pukes;
}
