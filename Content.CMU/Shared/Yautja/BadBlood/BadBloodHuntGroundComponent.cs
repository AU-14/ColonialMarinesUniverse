using Content.Shared.Actions;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Yautja.BadBlood;

/// <summary>
/// CMU14 FLOW-703: a Hunt Ground, the marker of an open Hunt Order. The Clan Leader orders the clan to seize it: living
/// Bad Bloods standing in <see cref="Radius"/> with no marine fill the seize meter, marines alone drain it, both
/// together freeze it. Full, a Clan Totem rises on it and the marker goes. Not seized by <see cref="ExpiresAt"/>, the
/// order lapses. BadBloodHuntOrderSystem owns all of it; the clan's list of open orders is on the rule entity.
/// </summary>
[RegisterComponent]
public sealed partial class BadBloodHuntGroundComponent : Component
{
    /// <summary>
    /// Which order of the round this is, counting from 1.
    /// </summary>
    [ViewVariables]
    public int Order;

    /// <summary>
    /// The order was picked in the near band (the first order of the round).
    /// </summary>
    [ViewVariables]
    public bool Near;

    [ViewVariables]
    public TimeSpan IssuedAt;

    /// <summary>
    /// The order lapses at this time unless it is seized first. A Bad Blood still standing on it holds it open.
    /// </summary>
    [ViewVariables]
    public TimeSpan ExpiresAt;

    /// <summary>
    /// 0 to 1. A Totem rises at 1.
    /// </summary>
    [ViewVariables]
    public float Progress;

    /// <summary>
    /// Seconds of one Bad Blood (contest weight 1) standing on it alone to fill the meter. Copied from the rule when
    /// the order is issued.
    /// </summary>
    [DataField]
    public TimeSpan SeizeTime = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Marines alone on it drain the meter this many times as fast as one Bad Blood fills it.
    /// </summary>
    [DataField]
    public float DrainMultiplier = 1f;

    /// <summary>
    /// In tiles: who counts as standing on it.
    /// </summary>
    [DataField]
    public float Radius = 7f;

    /// <summary>
    /// Bad Bloods and marines both stand on it: the meter is frozen.
    /// </summary>
    [ViewVariables]
    public bool Contested;

    /// <summary>
    /// Bad Bloods alone stand on it and the meter fills.
    /// </summary>
    [ViewVariables]
    public bool Filling;

    /// <summary>
    /// When each Bad Blood was last seen on it. Those seen within the rule's reward window share the seize Honour.
    /// </summary>
    [ViewVariables]
    public readonly Dictionary<EntityUid, TimeSpan> LastPresent = new();

    /// <summary>
    /// Marines who have had the popup on stepping onto it.
    /// </summary>
    [ViewVariables]
    public readonly HashSet<EntityUid> MarinesWarned = new();

    /// <summary>
    /// The clan is told the ground is contested at most this often.
    /// </summary>
    [DataField]
    public TimeSpan ContestAnnounceCooldown = TimeSpan.FromSeconds(60);

    [ViewVariables]
    public TimeSpan? LastContestAnnouncedAt;

    [ViewVariables]
    public bool Seized;

    // ---- the marine map (FLOW-604 pattern) ----

    /// <summary>
    /// How long after the later of the order and the landing the ground shows on the marine map.
    /// </summary>
    [DataField]
    public TimeSpan MapRevealDelay = TimeSpan.FromSeconds(120);

    [ViewVariables]
    public bool MapRevealed;

    /// <summary>
    /// Added once the ground shows on the marine map.
    /// </summary>
    [DataField]
    public ComponentRegistry RevealComponents = new();

    // ---- the light ----

    [DataField]
    public float IdleLightEnergy = 0.6f;

    /// <summary>
    /// While filling the light pulses between <see cref="IdleLightEnergy"/> and this, faster as the meter fills.
    /// </summary>
    [DataField]
    public float PulseLightEnergy = 2f;

    [DataField]
    public float ContestedLightEnergy = 0.3f;

    [ViewVariables]
    public int PulseTick;

    // ---- CMU14 FLOW-711: sounds, played at the ground with PlayPvs. Null plays nothing ----

    /// <summary>
    /// Bad Bloods alone start filling the meter.
    /// </summary>
    [DataField]
    public SoundSpecifier? SeizeStartSound;

    /// <summary>
    /// Every <see cref="SeizePulseInterval"/> while the meter fills.
    /// </summary>
    [DataField]
    public SoundSpecifier? SeizePulseSound;

    [DataField]
    public TimeSpan SeizePulseInterval = TimeSpan.FromSeconds(10);

    [ViewVariables]
    public TimeSpan NextPulseAt;

    /// <summary>
    /// Marines step onto it while Bad Bloods hold it.
    /// </summary>
    [DataField]
    public SoundSpecifier? ContestedSound;

    /// <summary>
    /// The horn: a Totem rises on the ground. Played at the new Totem.
    /// </summary>
    [DataField]
    public SoundSpecifier? RisenSound;
}

/// <summary>
/// CMU14 FLOW-706: on the Hunt Leader. Carries the Petition the Clan Leader action, which re-rolls an open order the
/// clan has not started on. Added and removed by BadBloodHuntOrderSystem as leadership moves.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BadBloodHuntPetitionComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId Action = "CMUActionBadBloodPetitionClanLeader";

    [DataField, AutoNetworkedField]
    public EntityUid? ActionEntity;
}

public sealed partial class BadBloodPetitionClanLeaderActionEvent : InstantActionEvent;

/// <summary>
/// CMU14 FLOW-706: the Hunt Leader's site dialog sends this back. <see cref="Pick"/> names the offer, so a stale
/// dialog cannot choose for a later one; <see cref="Index"/> is the site in it.
/// </summary>
[Serializable, NetSerializable]
public sealed record BadBloodHuntOrderPickedEvent(NetEntity User, int Pick, int Index);
