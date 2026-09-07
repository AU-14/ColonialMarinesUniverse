using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Threats;

/// <summary>A called or scheduled force which has not deployed yet.</summary>
[Serializable, NetSerializable]
public sealed record ForceInterestInfo(
    uint Identifier,
    string Name,
    int TotalRoles,
    int InterestedPlayers,
    int RequiredPlayers,
    bool Ready,
    bool Interested,
    bool CanJoin);

[Serializable, NetSerializable]
public sealed class SetForceInterestMessage(uint identifier, bool interested) : EuiMessageBase
{
    public uint Identifier { get; } = identifier;
    public bool Interested { get; } = interested;
}

public static class ForceInterest
{
    /// <summary>Strictly more than sixty percent, rounded down, must volunteer.</summary>
    public static int RequiredPlayers(int totalRoles) => totalRoles <= 0 ? 0 : (int) (totalRoles * 3L / 5) + 1;
}
