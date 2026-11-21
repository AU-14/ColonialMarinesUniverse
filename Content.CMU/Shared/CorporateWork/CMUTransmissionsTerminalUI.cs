using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.CorporateWork;

[Serializable, NetSerializable]
public enum CMUTransmissionsTerminalUi : byte
{
    Key,
}

/// <param name="Exact">Right symbol in the right place.</param>
/// <param name="Misplaced">Right symbol in the wrong place.</param>
[Serializable, NetSerializable]
public readonly record struct CMUTransmissionsAttempt(int[] Guess, int Exact, int Misplaced);

[Serializable, NetSerializable]
public readonly record struct CMUTransmissionsTierInfo(string Name, int Length, int Symbols, int Attempts, int Reward);

[Serializable, NetSerializable]
public readonly record struct CMUTransmissionsResult(bool Solved, int AttemptsUsed, int Payout, int[] Key);

[Serializable, NetSerializable]
public sealed class CMUTransmissionsTerminalBuiState(
    List<CMUTransmissionsTierInfo> tiers,
    int? activeTier,
    List<CMUTransmissionsAttempt> attempts,
    TimeSpan nextCaseAt,
    CMUTransmissionsResult? lastResult) : BoundUserInterfaceState
{
    public readonly List<CMUTransmissionsTierInfo> Tiers = tiers;
    public readonly int? ActiveTier = activeTier;
    public readonly List<CMUTransmissionsAttempt> Attempts = attempts;
    public readonly TimeSpan NextCaseAt = nextCaseAt;
    public readonly CMUTransmissionsResult? LastResult = lastResult;
}

[Serializable, NetSerializable]
public sealed class CMUTransmissionsStartBuiMsg(int tier) : BoundUserInterfaceMessage
{
    public readonly int Tier = tier;
}

[Serializable, NetSerializable]
public sealed class CMUTransmissionsGuessBuiMsg(int[] guess) : BoundUserInterfaceMessage
{
    public readonly int[] Guess = guess;
}
