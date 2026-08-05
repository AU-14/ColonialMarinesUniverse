using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared.GameTicking;

[Serializable, NetSerializable]
public sealed partial class CMURoundStatusEvent : EntityEventArgs
{
    public string MapName { get; }
    public string ShipMapName { get; }
    public int RoundId { get; }
    public int PlayerCount { get; }
    public string GamemodeTitle { get; }
    public TimeSpan RoundStartTimeSpan { get; }
    public TimeSpan RoundElapsedTime { get; }
    public bool IsRoundStarted { get; }

    public CMURoundStatusEvent(
        string mapName,
        string shipMapName,
        int roundId,
        int playerCount,
        string gamemodeTitle,
        TimeSpan roundStartTimeSpan,
        TimeSpan roundElapsedTime,
        bool isRoundStarted)
    {
        MapName = mapName;
        ShipMapName = shipMapName;
        RoundId = roundId;
        PlayerCount = playerCount;
        GamemodeTitle = gamemodeTitle;
        RoundStartTimeSpan = roundStartTimeSpan;
        RoundElapsedTime = roundElapsedTime;
        IsRoundStarted = isRoundStarted;
    }
}
