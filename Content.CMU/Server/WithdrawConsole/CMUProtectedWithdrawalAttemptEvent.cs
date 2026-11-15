namespace Content.Server.CMU14.WithdrawConsole;

/// <summary>
/// Allows a round policy to exclude protected withdrawal without changing ordinary dropship flight.
/// Includes withdrawal locks, return flights and round finalization (including stalemate).
/// </summary>
[ByRefEvent]
public record struct CMUProtectedWithdrawalAttemptEvent
{
    public bool Cancelled;
}
