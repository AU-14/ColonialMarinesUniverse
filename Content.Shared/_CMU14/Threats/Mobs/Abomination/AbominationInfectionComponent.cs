using Content.Shared.Damage;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._CMU14.Threats.Mobs.Abomination;

/// <summary>
///     A silent marker applied to a humanoid hit by an abomination (or injected
///     with abomination venom). There are no visible symptoms — no cough, no
///     jitter, no vomit, no drunkenness, no scream — so neither the host nor
///     anyone around them can tell they carry it. The infection still works
///     quietly: a poison tick drains the host until they die, and any
///     death while infected reclaims the body as an abomination (seeding flesh
///     kudzu at the corpse). The flesh roots in one limb — severing that limb
///     while the infection is still local cures it (the limb is destroyed,
///     prosthetic or nothing); past that window it goes systemic, the poison
///     ramps past what anti-toxin can hold, and only the WY counteragent
///     still cures. There is no free timeout.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class AbominationInfectionComponent : Component
{
    /// <summary>
    ///     Extremity (arm, hand, leg or foot) the flesh is anchored to. Which
    ///     one is hidden from the host and from medbay — amputation inside the
    ///     window is a dice roll. Null when the host has no severable
    ///     extremities (animals).
    /// </summary>
    [DataField]
    public EntityUid? AnchoredPart;

    /// <summary>
    ///     How long after infection the flesh stays local. Severing
    ///     <see cref="AnchoredPart" /> inside this window cures; afterwards the
    ///     infection is systemic and the poison starts ramping.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan AmputationWindow = TimeSpan.FromMinutes(4);

    /// <summary>
    ///     Poison added to every tick after the amputation window closes —
    ///     anti-toxin can buy time but can no longer keep up.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int PostWindowTickDamageGain = 2;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan InfectedAt;

    /// <summary>Next scheduled silent poison tick.</summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NextTickAt;

    /// <summary>Damage applied on each silent poison tick. Flat — not scaled by severity.</summary>
    [DataField, AutoNetworkedField]
    public DamageSpecifier TickDamage = new();

    /// <summary>How often the silent poison tick fires.</summary>
    [DataField, AutoNetworkedField]
    public TimeSpan TickInterval = TimeSpan.FromSeconds(6);
}
