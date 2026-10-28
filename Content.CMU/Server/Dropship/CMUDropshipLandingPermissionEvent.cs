namespace Content.Server.CMU14.Dropship;

/// <summary>Allows an owned transport service to authorize a specific landing across faction boundaries.</summary>
[ByRefEvent]
public record struct CMUDropshipLandingPermissionEvent(EntityUid Destination)
{
    public bool Allowed;
}
