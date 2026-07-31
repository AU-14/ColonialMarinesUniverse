namespace Content.Server._CMU14.BalanceRating;

internal sealed class CMUBalanceRatingSchedule
{
    public bool Due => RoundsRemaining == 0;
    public int RoundsRemaining { get; private set; }

    private int _lastCountedRoundId;

    public CMUBalanceRatingSchedule(int interval)
    {
        Reset(interval);
    }

    public bool CountRound(int roundId)
    {
        if (roundId <= _lastCountedRoundId)
            return false;

        _lastCountedRoundId = roundId;

        if (Due)
            return false;

        RoundsRemaining--;
        return Due;
    }

    public void Reset(int interval)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(interval, 1);
        RoundsRemaining = interval;
    }
}
