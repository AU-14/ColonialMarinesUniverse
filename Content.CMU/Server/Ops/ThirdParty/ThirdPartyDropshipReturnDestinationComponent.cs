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
