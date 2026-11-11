using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Traits.Quirks;

/// <summary>
///     Heavy sleeper: takes a few shakes to wake, and twice the usual hit.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUHeavySleeperComponent : Component
{
    /// <summary>How many shakes it takes to wake them.</summary>
    [DataField]
    public int ShakesToWake = 3;

    /// <summary>Shakes further apart than this start the count over.</summary>
    [DataField]
    public TimeSpan ShakeWindow = TimeSpan.FromSeconds(10);

    [DataField]
    public float DamageToWakeMultiplier = 2f;

    [DataField, AutoNetworkedField]
    public int Shakes;

    [DataField, AutoNetworkedField]
    public TimeSpan LastShake;
}

/// <summary>
///     Can't talk at all. Vocal emotes come out as silent pantomime.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUMuteComponent : Component;

/// <summary>
///     Clumsy: now and then fumbles whatever they're in the middle of doing.
/// </summary>
[RegisterComponent]
public sealed partial class CMUClumsyComponent : Component
{
    /// <summary>Chance per second, while busy with something, of fumbling it.</summary>
    [DataField]
    public float FumbleChancePerSecond = 0.035f;

    public TimeSpan NextCheck;
}

/// <summary>
///     Slow thinking: the odd "um..." or "uh..." creeps into what they say.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUSlowThinkingComponent : Component
{
    /// <summary>Chance a message gets a filler.</summary>
    [DataField]
    public float FillerChance = 0.3f;

    /// <summary>Chance a longer message gets a second one.</summary>
    [DataField]
    public float SecondFillerChance = 0.15f;
}

/// <summary>
///     Set from the player's options: while they're lying down on purpose, nobody can shake them back up.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUNoShakeWhenRestingComponent : Component;
