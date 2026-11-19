namespace Content.Server.CMU14.Camera;

/// <summary>
/// Moves a camera up a camera console's list. Lower numbers list first; cameras without this component list after
/// every camera that has one, at <see cref="Default"/>. Cameras of equal priority are listed by name.
/// </summary>
[RegisterComponent]
public sealed partial class CMUCameraListPriorityComponent : Component
{
    public const int Default = 100;

    [DataField]
    public int Priority;
}
