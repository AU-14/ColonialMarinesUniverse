using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Vehicle;

/// <summary>
/// Restricts entry and operation to the access groups configured by the vehicle's AccessReader.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ColonyDepartmentVehicleComponent : Component
{
    [AutoNetworkedField]
    public EntityUid? AuthorizedDriver;

    public EntityUid? LastCheckedDriver;
    public TimeSpan NextAccessCheck;
}
