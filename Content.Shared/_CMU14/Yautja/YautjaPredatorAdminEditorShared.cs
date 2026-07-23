using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Yautja;

[Serializable, NetSerializable]
public sealed class YautjaPredatorAdminEditorEuiState : EuiStateBase
{
    public YautjaPredatorAdminEditorEuiState(
        int roundId,
        bool roundActive,
        bool huntInitialized,
        int activeHunterSlots,
        int hunterSlots,
        bool randomEnabled,
        int randomMinimumRounds,
        int randomMaximumRounds,
        int randomRoundsRemaining,
        string statusMessage)
    {
        RoundId = roundId;
        RoundActive = roundActive;
        HuntInitialized = huntInitialized;
        ActiveHunterSlots = activeHunterSlots;
        HunterSlots = hunterSlots;
        RandomEnabled = randomEnabled;
        RandomMinimumRounds = randomMinimumRounds;
        RandomMaximumRounds = randomMaximumRounds;
        RandomRoundsRemaining = randomRoundsRemaining;
        StatusMessage = statusMessage;
    }

    public int RoundId { get; }
    public bool RoundActive { get; }
    public bool HuntInitialized { get; }
    public int ActiveHunterSlots { get; }
    public int HunterSlots { get; }
    public bool RandomEnabled { get; }
    public int RandomMinimumRounds { get; }
    public int RandomMaximumRounds { get; }
    public int RandomRoundsRemaining { get; }
    public string StatusMessage { get; }
}

[Serializable, NetSerializable]
public sealed class YautjaPredatorAdminEditorInitializeMessage : EuiMessageBase;

[Serializable, NetSerializable]
public sealed class YautjaPredatorAdminEditorSetHunterSlotsMessage(int slots) : EuiMessageBase
{
    public int Slots { get; } = slots;
}

[Serializable, NetSerializable]
public sealed class YautjaPredatorAdminEditorSetRandomMessage(
    bool enabled,
    int minimumRounds,
    int maximumRounds) : EuiMessageBase
{
    public bool Enabled { get; } = enabled;
    public int MinimumRounds { get; } = minimumRounds;
    public int MaximumRounds { get; } = maximumRounds;
}

[Serializable, NetSerializable]
public sealed class YautjaPredatorAdminEditorRefreshMessage : EuiMessageBase;
