using Content.Shared._RMC14.Dialog;
using Content.Shared.DeviceLinking;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Basketball;

/// <summary>
///     A hoop that scores for one side. Scores go to every scoreboard linked to <see cref="ScorePort"/>
///     with a multitool.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(CMUBasketballSystem))]
public sealed partial class CMUBasketballHoopComponent : Component
{
    [DataField]
    public CMUBasketballTeam Side;

    [DataField]
    public int ShotPoints = 2;

    [DataField]
    public float ShotChance = 0.5f;

    [DataField]
    public int DunkPoints = 2;

    [DataField]
    public string SensorFixtureId = "basketball";

    [DataField]
    public ProtoId<SourcePortPrototype> ScorePort = "CMUBasketballScore";
}

/// <summary>
///     Shows the score of every hoop linked to it. Scoreboards linked to the same hoops stay in step
///     because each one receives the same scores and resets.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(CMUBasketballSystem))]
public sealed partial class CMUBasketballScoreboardComponent : Component
{
    [DataField]
    public int MaxScore = 99;

    [DataField, AutoNetworkedField]
    public int LeftScore;

    [DataField, AutoNetworkedField]
    public int RightScore;

    /// <summary>
    ///     Player-chosen name for the left side; null falls back to "Left".
    /// </summary>
    [DataField, AutoNetworkedField]
    public string? LeftTeamName;

    /// <summary>
    ///     Player-chosen name for the right side; null falls back to "Right".
    /// </summary>
    [DataField, AutoNetworkedField]
    public string? RightTeamName;

    [DataField]
    public int TeamNameLimit = 24;

    /// <summary>
    ///     The first side to reach this score wins and the scoreboard stops counting until it is reset.
    ///     0 means there is no limit.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int TargetScore = 21;

    /// <summary>
    ///     Winning scores players can pick from the scoreboard's menu, besides no limit.
    /// </summary>
    [DataField]
    public List<int> TargetScoreOptions = new() { 11, 21, 31 };

    [DataField, AutoNetworkedField]
    public CMUBasketballTeam? Winner;

    [DataField]
    public ProtoId<SinkPortPrototype> ScorePort = "CMUBasketballScore";

    [DataField]
    public ProtoId<SinkPortPrototype> ResetPort = "CMUBasketballReset";

    [DataField]
    public SoundSpecifier? BuzzerSound = new SoundPathSpecifier("/Audio/CMU14/Machines/scoreboard_buzzer.ogg");
}

/// <summary>
///     Zeroes every scoreboard linked to <see cref="ResetPort"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true), AutoGenerateComponentPause]
[Access(typeof(CMUBasketballSystem))]
public sealed partial class CMUBasketballResetComponent : Component
{
    [DataField]
    public TimeSpan ResetCooldown = TimeSpan.FromSeconds(5);

    [DataField]
    public TimeSpan PressedDuration = TimeSpan.FromSeconds(0.3);

    [DataField]
    public ProtoId<SourcePortPrototype> ResetPort = "CMUBasketballReset";

    [DataField, AutoNetworkedField]
    public bool Pressed;

    [DataField, AutoPausedField]
    public TimeSpan PressedUntil;

    [DataField, AutoPausedField]
    public TimeSpan NextResetAt;
}

/// <summary>
///     Remembers which throw already rolled for a basket, since one throw can raise several collisions.
/// </summary>
[RegisterComponent]
[Access(typeof(CMUBasketballSystem))]
public sealed partial class CMUBasketballShotAttemptComponent : Component
{
    public bool Attempted;
    public TimeSpan? ThrownTime;
}

[Serializable, NetSerializable]
public sealed record CMUBasketballTeamNameEvent(NetEntity User, CMUBasketballTeam Side, string Message = "") : DialogInputEvent(Message);

[Serializable, NetSerializable]
public sealed record CMUBasketballTargetScoreEvent(NetEntity User, int Score);

[Serializable, NetSerializable]
public enum CMUBasketballTeam : byte
{
    Left,
    Right,
}

[Serializable, NetSerializable]
public enum CMUBasketballScoreboardLayers : byte
{
    Base,
    LeftTens,
    LeftOnes,
    RightTens,
    RightOnes,
}

[Serializable, NetSerializable]
public enum CMUBasketballResetLayers : byte
{
    Base,
}
