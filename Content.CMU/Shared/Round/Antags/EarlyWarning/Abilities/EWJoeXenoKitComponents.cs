namespace Content.Shared.CMU14.Round.Antags.EarlyWarning.Abilities;

// ECS-03 (node N10): the anti-xeno kit's Idea A/B components that the level table grants. Every effect
// here is xenofauna-only (EWJoeTargetRules.IsXenofauna) unless its summary says otherwise. The server's
// EWJoeXenoKitSystem applies them; the components live in Shared so the level table resolves on the client.

/// <summary>
/// 0B Acid-Rated Plating (level 0, passive): xeno acid and spit burn the Joe for
/// <see cref="AcidDamageMultiplier"/> of their heat and caustic damage, and acid on the casing clears
/// itself after <see cref="AcidClearTime"/> instead of burning for its full duration.
/// </summary>
[RegisterComponent]
public sealed partial class EWJoeAcidPlatingComponent : Component
{
    [DataField]
    public float AcidDamageMultiplier = 0.5f;

    [DataField]
    public TimeSpan AcidClearTime = TimeSpan.FromSeconds(5);

    /// <summary>When acid was first seen on the casing. Server bookkeeping.</summary>
    [DataField]
    public TimeSpan? AcidedSince;
}

/// <summary>
/// 1A Cutting Torch (level 1): a welder becomes Scour food. For <see cref="Duration"/> after one is
/// scoured, the Joe's hits (unarmed or the machete) burn xenofauna for <see cref="BurnBonus"/> heat and
/// break xeno resin structures and weeds in one hit (never the hive core).
/// </summary>
[RegisterComponent]
public sealed partial class EWJoeCuttingTorchComponent : Component
{
    [DataField]
    public float BurnBonus = 15f;

    [DataField]
    public TimeSpan Duration = TimeSpan.FromMinutes(3);

    /// <summary>When the scoured torch runs out; zero while no welder has been scoured.</summary>
    [DataField]
    public TimeSpan ActiveUntil;
}

/// <summary>3A Rending Edge (level 3): the machete pierces this much more armor on xenofauna.</summary>
[RegisterComponent]
public sealed partial class EWJoeRendingEdgeComponent : Component
{
    [DataField]
    public int ArmorPiercing = 20;
}

/// <summary>
/// 3B Overclock: Steady (level 3): while Overclock runs the Joe cannot be pounced (xeno leap) or lunged,
/// and knockdowns are refused while living xenofauna are within <see cref="XenoProximity"/> (tail sweep,
/// trip and lash carry no source, so a nearby xenofauna is the tell).
/// </summary>
[RegisterComponent]
public sealed partial class EWJoeSteadyComponent : Component
{
    [DataField]
    public float XenoProximity = 3f;
}

/// <summary>
/// 4A Purge Ground (level 4): a landed Consecrate clears xeno weeds within <see cref="Radius"/> of the Joe
/// and keeps them from growing back there for <see cref="BlockDuration"/>.
/// </summary>
[RegisterComponent]
public sealed partial class EWJoePurgeGroundComponent : Component
{
    [DataField]
    public float Radius = 3f;

    [DataField]
    public TimeSpan BlockDuration = TimeSpan.FromSeconds(30);
}

/// <summary>
/// 4B Unwind Recoil (level 4): for <see cref="Duration"/> after Unwind, <see cref="ReflectFraction"/> of
/// the melee damage xenofauna deal the Joe is dealt back to the attacker.
/// </summary>
[RegisterComponent]
public sealed partial class EWJoeUnwindRecoilComponent : Component
{
    [DataField]
    public float ReflectFraction = 0.25f;

    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(10);

    /// <summary>When the recoil window from the last Unwind ends.</summary>
    [DataField]
    public TimeSpan ActiveUntil;
}

/// <summary>
/// 5A Searing Light (level 5, beside THE LIGHT): xenofauna within the light's range and sight take
/// <see cref="BurnPerSecond"/> heat each second and receive no pheromones while seared.
/// </summary>
[RegisterComponent]
public sealed partial class EWJoeSearingLightComponent : Component
{
    [DataField]
    public float BurnPerSecond = 3f;

    [DataField]
    public TimeSpan Interval = TimeSpan.FromSeconds(1);

    /// <summary>How long a xenofauna stays cut off from pheromones after the light last touched it.</summary>
    [DataField]
    public TimeSpan SearDuration = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan NextBurn;
}

/// <summary>On a xenofauna THE LIGHT is searing: its pheromone receiver effects are stripped until <see cref="Until"/>.</summary>
[RegisterComponent]
public sealed partial class EWJoeSearedComponent : Component
{
    [DataField]
    public TimeSpan Until;
}

/// <summary>
/// 5B Saturate (xeno) (level 5): Saturate's cone also dazes xenofauna for <see cref="DazeTime"/> and
/// shreds <see cref="ArmorStrip"/> of their xeno armor for <see cref="ArmorStripDuration"/> through the
/// shared <see cref="EWJoeArmorShredSystem"/>.
/// </summary>
[RegisterComponent]
public sealed partial class EWJoeSaturateXenoComponent : Component
{
    [DataField]
    public TimeSpan DazeTime = TimeSpan.FromSeconds(4);

    [DataField]
    public int ArmorStrip = 15;

    [DataField]
    public TimeSpan ArmorStripDuration = TimeSpan.FromSeconds(6);
}
