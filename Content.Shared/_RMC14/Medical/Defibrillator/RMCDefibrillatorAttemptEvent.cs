namespace Content.Shared._RMC14.Medical.Defibrillator;

public sealed class RMCDefibrillatorAttemptEvent : CancellableEntityEventArgs
{
    public RMCDefibrillatorAttemptEvent(EntityUid target, bool allowBeatingHeart = false)
    {
        Target = target;
        AllowBeatingHeart = allowBeatingHeart;
    }

    public EntityUid Target { get; }

    /// <summary>
    /// Chemical pacing can begin during the short corpse transition where the body is dead but the
    /// periodic heart update has not marked the heart stopped yet. Physical defibrillators retain
    /// their normal rejection of a genuinely beating heart.
    /// </summary>
    public bool AllowBeatingHeart { get; }

    public string? CancelReason { get; private set; }

    public void Cancel(string reason)
    {
        Cancel();
        CancelReason ??= reason;
    }
}
