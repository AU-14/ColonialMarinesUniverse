namespace Content.Server.CMU14.Ops.ThirdParty;

[RegisterComponent]
public sealed partial class ThirdPartyDropshipReturnDestinationComponent : Component
{
    [DataField(required: true)]
    public EntityUid Shuttle;
}

[RegisterComponent]
public sealed partial class ThirdPartyDropshipReturnedComponent : Component;

[RegisterComponent]
public sealed partial class ThirdPartyDropshipDeactivatedConsoleComponent : Component;

[RegisterComponent]
public sealed partial class ThirdPartyDropshipAutoReturnComponent : Component
{
    [DataField]
    public TimeSpan InactivityDelay = TimeSpan.FromSeconds(90);

    [DataField]
    public TimeSpan LastActivity;

    [DataField]
    public TimeSpan NextWarningAt;

    [DataField]
    public TimeSpan? ReturnAt;

    [DataField]
    public TimeSpan ReturnDelay = TimeSpan.FromSeconds(30);

    [DataField(required: true)]
    public EntityUid ReturnDestination;

    [DataField]
    public TimeSpan WarningInterval = TimeSpan.FromSeconds(30);
}

/// <summary>A third party shuttle called in by a faction (govfor or opfor), rather than one that arrived on its own.</summary>
[RegisterComponent]
public sealed partial class CMUSummonedThirdPartyShuttleComponent : Component
{
    [DataField]
    public string Faction = string.Empty;
}

/// <summary>A summoned shuttle sitting on its faction's ship pad, waiting to go back to deep space.</summary>
[RegisterComponent]
public sealed partial class CMUSummonedThirdPartyDepartureComponent : Component
{
    [DataField]
    public TimeSpan DepartAt;

    [DataField]
    public TimeSpan NextWarningAt;
}
