namespace Content.Shared._RMC14.Communications;

[ByRefEvent] // CMU14
public sealed record CommunicationsTowerStateChangedEvent(Entity<CommunicationsTowerComponent> Tower);
