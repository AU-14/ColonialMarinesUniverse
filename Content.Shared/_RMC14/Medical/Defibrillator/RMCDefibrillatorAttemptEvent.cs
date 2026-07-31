namespace Content.Shared._RMC14.Medical.Defibrillator;

public sealed class RMCDefibrillatorAttemptEvent(EntityUid target) : CancellableEntityEventArgs
{
    public EntityUid Target { get; } = target;
    public string? CancelReason { get; private set; }

    public void Cancel(string reason)
    {
        Cancel();
        CancelReason ??= reason;
    }
}
