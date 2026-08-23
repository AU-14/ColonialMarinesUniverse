namespace Content.Server._CMU14.Yautja;

[RegisterComponent, Access(typeof(YautjaYoungbloodSystem), typeof(YautjaHuntConsoleSystem))]
public sealed partial class YautjaYoungbloodGhostRoleComponent : Component
{
    [DataField(required: true)]
    public string CallId = string.Empty;

    [DataField]
    public bool BypassEligibility;

    public bool SetupComplete;
}
