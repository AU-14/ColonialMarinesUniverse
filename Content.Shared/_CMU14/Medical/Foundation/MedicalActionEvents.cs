namespace Content.Shared._CMU14.Medical.Foundation;

[ByRefEvent]
public record struct MedicalActionAttemptEvent
{
    public readonly MedicalActionRequest Request;
    public MedicalActionResult? Result;
    public bool Handled;
    public bool Cancelled;

    public MedicalActionAttemptEvent(MedicalActionRequest request)
    {
        Request = request;
        Result = null;
        Handled = false;
        Cancelled = false;
    }
}

[ByRefEvent]
public readonly record struct MedicalActionRoutedEvent(
    MedicalActionRequest Request,
    MedicalActionResult Result);
