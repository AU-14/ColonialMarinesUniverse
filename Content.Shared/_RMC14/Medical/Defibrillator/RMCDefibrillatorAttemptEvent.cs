namespace Content.Shared._RMC14.Medical.Defibrillator;

public sealed class RMCDefibrillatorAttemptEvent : CancellableEntityEventArgs
{
    public RMCDefibrillatorAttemptEvent(EntityUid target)
    {
        Target = target;
    }

    public EntityUid Target { get; }

    public string? CancelReason { get; private set; }

    public void Cancel(string reason)
    {
        Cancel();
        CancelReason ??= reason;
    }
}
