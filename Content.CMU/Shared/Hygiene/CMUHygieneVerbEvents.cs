namespace Content.Shared.CMU14.Hygiene;

public sealed class CMUSinkWashRequestEvent(EntityUid user) : EntityEventArgs
{
    public readonly EntityUid User = user;
}

public sealed class CMUShowerToggleRequestEvent(EntityUid user) : EntityEventArgs
{
    public readonly EntityUid User = user;
}
