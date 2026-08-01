using Robust.Shared.Serialization;

namespace Content.Shared.AU14.WithdrawConsole;

[Serializable, NetSerializable]
public enum WithdrawConsoleUiKey
{
    Key,
}

[Serializable, NetSerializable]
public sealed class WithdrawConsoleBuiState : BoundUserInterfaceState
{
    public readonly string Faction;
    public readonly bool IsUnlocked;
    public readonly int SwipedIdCount;
    public readonly bool WithdrawActive;
    public readonly bool StalemateToggled;
    /// <summary>True while still within the 15-minute cancellation window.</summary>
    public readonly bool CanCancel;
    /// <summary>Seconds remaining in the withdrawal countdown, or null when inactive.</summary>
    public readonly double? SecondsRemaining;
    public readonly bool RoundEndTriggered;

    public WithdrawConsoleBuiState(
        string faction,
        bool isUnlocked,
        int swipedIdCount,
        bool withdrawActive,
        bool stalemateToggled,
        bool canCancel,
        double? secondsRemaining,
        bool roundEndTriggered)
    {
        Faction = faction;
        IsUnlocked = isUnlocked;
        SwipedIdCount = swipedIdCount;
        WithdrawActive = withdrawActive;
        StalemateToggled = stalemateToggled;
        CanCancel = canCancel;
        SecondsRemaining = secondsRemaining;
        RoundEndTriggered = roundEndTriggered;
    }
}

[Serializable, NetSerializable]
public sealed class WithdrawConsoleToggleWithdrawMsg : BoundUserInterfaceMessage { }

[Serializable, NetSerializable]
public sealed class WithdrawConsoleCancelMsg : BoundUserInterfaceMessage { }

[Serializable, NetSerializable]
public sealed class WithdrawConsoleToggleStalemateMsg : BoundUserInterfaceMessage { }
