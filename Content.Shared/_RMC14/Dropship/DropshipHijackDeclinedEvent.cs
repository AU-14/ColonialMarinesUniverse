namespace Content.Shared._RMC14.Dropship;

[ByRefEvent]
public record struct DropshipHijackDeclinedEvent(EntityUid Queen)
{
    public bool Handled;
}
