namespace Content.Shared._CMU14.Round.Objectives;

public sealed class CaptureHoistFlagStartedEvent : EntityEventArgs
{
    public EntityUid User;
    public string Faction;

    public CaptureHoistFlagStartedEvent(EntityUid user, string faction)
    {
        User = user;
        Faction = faction;
    }
}
