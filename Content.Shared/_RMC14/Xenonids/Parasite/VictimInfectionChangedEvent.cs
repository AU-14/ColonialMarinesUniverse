namespace Content.Shared._RMC14.Xenonids.Parasite;

/// <summary>Published by the infection lifecycle owner after membership changes.</summary>
[ByRefEvent]
public readonly record struct VictimInfectionChangedEvent(EntityUid Victim);
