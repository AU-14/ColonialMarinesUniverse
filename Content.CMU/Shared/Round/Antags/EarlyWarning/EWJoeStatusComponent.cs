using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.CMU14.Round.Antags.EarlyWarning;

/// <summary>
/// What the Joe's own level and objective panel shows (playtest 3): the level, its name and the
/// objectives on offer for the next one with their progress. The server fills it from the objective
/// ladder; only the Joe's own client receives it, so it never identifies the antag to anyone else.
/// The upstream character window no longer lists the ladder's objectives; this panel replaces them.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class EWJoeStatusComponent : Component
{
    public override bool SendOnlyToOwner => true;

    [DataField, AutoNetworkedField]
    public int Level;

    /// <summary>The level name's locale id, resolved on the client.</summary>
    [DataField, AutoNetworkedField]
    public string LevelName = string.Empty;

    /// <summary>The last level: nothing more is offered.</summary>
    [DataField, AutoNetworkedField]
    public bool FinalLevel;

    [AutoNetworkedField]
    public List<EWJoeObjectiveEntry> Objectives = new();

    [DataField, AutoNetworkedField]
    public int CreditEarned;

    [DataField, AutoNetworkedField]
    public int CreditRequired;

    /// <summary>The level needs a finished console among its completions (D8, level 4).</summary>
    [DataField, AutoNetworkedField]
    public bool RequireAssimilation;

    /// <summary>A-17: completions count double right now.</summary>
    [DataField, AutoNetworkedField]
    public bool DoubleCredit;

    /// <summary>Playtest 4: the names of the Joe's River-Melded thralls.</summary>
    [AutoNetworkedField]
    public List<string> Thralls = new();

    /// <summary>Playtest 4: the Joe holds its one respawn.</summary>
    [DataField, AutoNetworkedField]
    public bool RespawnReady;

    /// <summary>ARCH-02: the Joe may ask for one unfinished directive to be swapped right now.</summary>
    [DataField, AutoNetworkedField]
    public bool RerollReady;
}

/// <summary>
/// One offered objective as the panel shows it. <see cref="Slot"/> is its place in the Joe's current offer,
/// which the reroll request names; <see cref="Rerollable"/> is false for finished and forced directives.
/// </summary>
[Serializable, NetSerializable]
public readonly record struct EWJoeObjectiveEntry(string Title, string Description, float Progress, bool Completed,
    int Slot = -1, bool Rerollable = false);

/// <summary>
/// ARCH-02: sent by the Joe's client from its status panel to swap the unfinished directive in
/// <see cref="Slot"/> for another from the level's pool. The server checks the limit (once per level, or
/// again after a cooldown while the level lasts) and that the sender is the Joe.
/// </summary>
[Serializable, NetSerializable]
public sealed class EWJoeRequestDirectiveRerollEvent(int slot) : EntityEventArgs
{
    public readonly int Slot = slot;
}
