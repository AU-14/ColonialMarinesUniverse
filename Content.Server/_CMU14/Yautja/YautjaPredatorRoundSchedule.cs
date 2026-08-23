namespace Content.Server._CMU14.Yautja;

internal sealed class YautjaPredatorRoundSchedule
{
    public bool Due => RoundsRemaining == 0;
    public int RoundsRemaining { get; private set; }

    private int _lastCountedRoundId;

    public YautjaPredatorRoundSchedule(int interval)
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
