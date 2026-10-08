using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Vehicle.Jeep;

/// <summary>
/// A passenger of an open vehicle: guns in their hands scatter more, less so the better they shoot.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUVehicleRiderComponent : Component
{
    /// <summary>
    /// Degrees added to the spread of guns this rider holds.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ExtraSpread;
}

/// <summary>
/// A gun held by a <see cref="CMUVehicleRiderComponent"/>, widened by their riding spread.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUVehicleRiderGunComponent : Component
{
    [DataField, AutoNetworkedField]
    public float ExtraSpread;
}
