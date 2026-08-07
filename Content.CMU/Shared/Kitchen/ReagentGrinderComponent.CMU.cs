namespace Content.Shared.Kitchen.Components;

public sealed partial class ReagentGrinderComponent
{
    [DataField]
    public float LinkDistance = 8;

    [DataField]
    public float LinkLimit = 16;

    [AutoNetworkedField]
    public EntityUid? SmartFridge;
}
