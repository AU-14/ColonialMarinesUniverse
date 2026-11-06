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
}

/// <summary>One offered objective as the panel shows it.</summary>
[Serializable, NetSerializable]
public readonly record struct EWJoeObjectiveEntry(string Title, string Description, float Progress, bool Completed);
