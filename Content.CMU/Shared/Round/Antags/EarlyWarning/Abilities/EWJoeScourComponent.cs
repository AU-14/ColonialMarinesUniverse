using Content.Shared.Radio;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Round.Antags.EarlyWarning.Abilities;

/// <summary>
/// The active Scour buffs (phase E4, balance A-6). The Joe digests a piece of colony technology and
/// keeps its bonus until that kind's expiry (playtest 3: each kind keeps its own timer, shown as its
/// own status alert; playtest 4: one-of-a-kind gear never expires). Not networked: the armor bonus is read
/// by the shared armor event on the server, where damage is decided, and everything else is applied there
/// too. The component goes away when the last kind expires.
/// </summary>
[RegisterComponent]
public sealed partial class EWJoeScourComponent : Component
{
    /// <summary>When each active kind ends. A fuel buff's entry is its self-repair window.</summary>
    [DataField]
    public Dictionary<EWJoeScourKind, TimeSpan> Expiries = new();

    /// <summary>
    /// Playtest 4: kinds taken from one-of-a-kind specialist gear (scout cloak, pyro armor, sniper ghillie).
    /// They never expire (<see cref="Expiries"/> holds <see cref="TimeSpan.MaxValue"/>) and a later timed
    /// scour of the same kind does not shorten them. Stand-down still removes them.
    /// </summary>
    [DataField]
    public HashSet<EWJoeScourKind> Permanent = new();

    /// <summary>Flat bullet armor from digested body armor.</summary>
    [DataField]
    public int BulletArmor;

    /// <summary>Extra stamina damage on every punch from a digested stun baton.</summary>
    [DataField]
    public double PunchStamina;

    /// <summary>Ignition immunity granted for the buff (pyro armor); removed on expiry if added.</summary>
    [DataField]
    public bool IgnitionImmunityAdded;

    [DataField]
    public bool FireSlowImmunityAdded;

    [DataField]
    public TimeSpan NextRepairTick;

    /// <summary>Radio channels granted for the buff (a digested radio); revoked on expiry.</summary>
    [DataField]
    public List<ProtoId<RadioChannelPrototype>> RadioChannels = new();

    [DataField]
    public bool AddedActiveRadio;

    [DataField]
    public bool AddedReceiver;

    /// <summary>The Joe was given its own thermal cloak and cloak action by a scoured scout cloak.</summary>
    [DataField]
    public bool CloakAdded;

    [DataField]
    public bool TurnInvisibleAdded;
}

/// <summary>What a scoured item was, which decides the buff.</summary>
public enum EWJoeScourKind : byte
{
    Baton,
    Armor,
    Fuel,
    Radio,
    Pyro,
    Cloak,
}
