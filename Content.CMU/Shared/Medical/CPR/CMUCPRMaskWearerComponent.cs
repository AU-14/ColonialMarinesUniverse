namespace Content.Shared.CMU14.Medical.CPR;

[RegisterComponent]
public sealed partial class CMUCPRMaskWearerComponent : Component
{
    [DataField]
    public EntityUid? Mask;
}
