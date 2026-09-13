namespace Content.Shared._RMC14.Medical.Defibrillator;

/// <summary>Raised on a physical device only after its target has successfully revived.</summary>
[ByRefEvent]
public readonly record struct RMCDefibrillatorRevivedEvent(EntityUid Target);
