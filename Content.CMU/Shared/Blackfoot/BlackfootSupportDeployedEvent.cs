namespace Content.Shared.CMU14.Blackfoot;

/// <summary>Raised on the kit before it is removed, so deployed equipment can inherit its properties.</summary>
[ByRefEvent]
public readonly record struct BlackfootSupportDeployedEvent(EntityUid Deployed, EntityUid User);
