using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Xenomorphs.Pathogen.Cyclone;

/// <summary>
/// Harbinger Cyclone ability.
/// Channels briefly, then executes SpinsPerCycle sweeping hits per "cycle".
/// If the first cycle hits MinHitsForCycles targets, triggers additional
/// expanding cycles — each one wider and slightly weaker.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CMUXenoCycloneSystem))]
public sealed partial class CMUXenoCycloneComponent : Component
{
    [DataField, AutoNetworkedField]
    public float PlasmaCost = 75f;

    /// <summary>Delay before the first spin fires after activation.</summary>
    [DataField, AutoNetworkedField]
    public TimeSpan ActivationDelay = TimeSpan.FromSeconds(2);

    [DataField, AutoNetworkedField]
    public float BaseRange = 2f;

    [DataField, AutoNetworkedField]
    public float BaseDamage = 30;

    /// <summary>
    /// Number of rapid hit-ticks per cycle.
    /// Each tick deals BaseDamage / SpinsPerCycle, fires SpinInterval apart.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int SpinsPerCycle = 3;

    [DataField, AutoNetworkedField]
    public TimeSpan SpinInterval = TimeSpan.FromMilliseconds(250);

    [DataField, AutoNetworkedField]
    public TimeSpan KnockdownTime = TimeSpan.FromSeconds(0.8);

    /// <summary>Min targets hit in first cycle to trigger extra cycles.</summary>
    [DataField, AutoNetworkedField]
    public int MinHitsForCycles = 2;

    [DataField, AutoNetworkedField]
    public int ExtraCycles = 3;

    [DataField, AutoNetworkedField]
    public float CycleDamageMultiplier = 0.6f;

    /// <summary>Delay between extra cycles, reduced by 0.4s each time.</summary>
    [DataField, AutoNetworkedField]
    public TimeSpan CycleDelay = TimeSpan.FromSeconds(3);

    /// <summary>Range grows this much per extra cycle.</summary>
    [DataField, AutoNetworkedField]
    public float RangeGrowthPerCycle = 0.75f;

    [DataField, AutoNetworkedField]
    public float MaxRange = 4.5f;

    [DataField, AutoNetworkedField]
    public SoundSpecifier WindupSound = new SoundPathSpecifier(
        "/Audio/_RMC14/Xeno/crusher_windup_sound.ogg",
        AudioParams.Default.WithVolume(-3));

    [DataField, AutoNetworkedField]
    public SoundSpecifier SpinHitSound = new SoundPathSpecifier(
        "/Audio/_RMC14/Xeno/alien_tail_attack.ogg",
        AudioParams.Default.WithVolume(-2));

    [DataField, AutoNetworkedField]
    public EntProtoId HitEffect = "RMCEffectTailswipe";

    /// <summary>
    /// Animation entity spawned attached to the xeno itself on each spin tick,
    /// so the xeno visibly plays a spin/slash animation rather than only
    /// showing hit effects on targets.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntProtoId? SpinAnimationId = "RMCEffectTailswipe";
}