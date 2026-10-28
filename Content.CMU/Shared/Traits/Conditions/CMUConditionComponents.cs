using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Traits.Conditions;

/// <summary>
///     Fear of blood: treating wounds takes three times as long, and being near blood or a body brings on dizziness and
///     sometimes vomiting.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUFearOfBloodComponent : Component
{
    /// <summary>How much longer bandaging, treating wounds and surgery take.</summary>
    [DataField]
    public float TreatmentDelayMultiplier = 3f;

    /// <summary>How close blood or a body has to be to set it off, in tiles.</summary>
    [DataField]
    public float Range = 3f;

    [DataField]
    public TimeSpan CheckInterval = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan Dizziness = TimeSpan.FromSeconds(4);

    /// <summary>Chance each check, while near blood, to throw up.</summary>
    [DataField]
    public float VomitChance = 0.03f;

    [DataField]
    public TimeSpan MessageCooldown = TimeSpan.FromSeconds(25);

    public TimeSpan NextCheck;
    public TimeSpan NextMessage;
    public bool WasScared;

    /// <summary>The multiplier to apply to a treatment do-after for <paramref name="user"/>.</summary>
    public static float GetTreatmentMultiplier(IEntityManager entities, EntityUid user)
    {
        return entities.TryGetComponent(user, out CMUFearOfBloodComponent? fear) ? fear.TreatmentDelayMultiplier : 1f;
    }
}

/// <summary>Hemophilia: every bleed, external and internal, loses blood faster.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUHemophiliaComponent : Component
{
    [DataField]
    public float BleedMultiplier = 1.75f;
}

/// <summary>
///     A heart condition: sprinting strains the heart. Too much brings on an episode that slows and dizzies them, and
///     it can end in cardiac arrest unless cardiocaine or epinephrine is in their blood.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUHeartConditionComponent : Component
{
    [DataField]
    public float Strain;

    [DataField]
    public float EpisodeThreshold = 100f;

    [DataField]
    public float SprintGainPerSecond = 1.4f;

    [DataField]
    public float RecoveryPerSecond = 0.7f;

    [DataField]
    public float WarnThreshold = 60f;

    [DataField]
    public TimeSpan EpisodeDuration = TimeSpan.FromSeconds(40);

    /// <summary>Chance an untreated episode ends with the heart stopping.</summary>
    [DataField]
    public float ArrestChance = 0.3f;

    [DataField]
    public float EpisodeSpeedModifier = 0.65f;

    [DataField]
    public TimeSpan WarnCooldown = TimeSpan.FromSeconds(30);

    /// <summary>Whether an episode is going on, which slows them down.</summary>
    [DataField, AutoNetworkedField]
    public bool InEpisode;

    [DataField]
    public TimeSpan Dizziness = TimeSpan.FromSeconds(3);

    /// <summary>Until when propranolol keeps sprinting from straining the heart.</summary>
    [DataField]
    public TimeSpan StrainBlockedUntil;

    public TimeSpan EpisodeEndsAt;
    public TimeSpan NextTick;
    public TimeSpan NextWarn;
}

/// <summary>Hard of hearing: speech from further than a few tiles away is partly lost.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUHardOfHearingComponent : Component
{
    /// <summary>Up to this many tiles away, speech is heard clearly.</summary>
    [DataField]
    public float ClearRange = 3f;

    /// <summary>At the edge of hearing, only this share of the letters gets through.</summary>
    [DataField]
    public float FarClarity = 0.25f;
}

/// <summary>Claustrophobia: being shut in a locker, crate or a cramped space brings on dizziness and panic.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUClaustrophobiaComponent : Component
{
    /// <summary>How many of the eight tiles around them have to be blocked for it to feel cramped.</summary>
    [DataField]
    public int BlockedNeighbours = 6;

    [DataField]
    public TimeSpan CheckInterval = TimeSpan.FromSeconds(1);

    [DataField]
    public TimeSpan Dizziness = TimeSpan.FromSeconds(3);

    /// <summary>Panic added each check while cramped, for someone who's also panic-prone.</summary>
    [DataField]
    public float PanicPerCheck = 4f;

    [DataField]
    public TimeSpan MessageCooldown = TimeSpan.FromSeconds(20);

    public TimeSpan NextCheck;
    public TimeSpan NextMessage;
    public bool WasScared;
}

/// <summary>
///     Can't walk: they get about by wheelchair, and spawn sitting in one. Out of it they can only drag themselves along
///     the floor.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUWheelchairBoundComponent : Component
{
    [DataField]
    public string Wheelchair = "RMCWheelchair";

    /// <summary>How fast they crawl out of the chair.</summary>
    [DataField]
    public float CrawlSpeed = 0.2f;
}

/// <summary>Hearty appetite: gets hungry faster.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUHeartyAppetiteComponent : Component
{
    [DataField]
    public float Multiplier = 1.5f;
}

/// <summary>High thirst: gets thirsty faster.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUHighThirstComponent : Component
{
    [DataField]
    public float Multiplier = 1.5f;
}

/// <summary>Weak throw: can't throw as far.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUWeakThrowComponent : Component
{
    [DataField]
    public float RangeMultiplier = 0.75f;
}
