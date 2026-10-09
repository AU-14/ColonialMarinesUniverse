using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Vehicle.Jeep;

/// <summary>
/// Anyone riding an open vehicle, driver included: guns in their hands scatter more, less so the
/// better they shoot, and more again the faster the vehicle goes.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUVehicleRiderComponent : Component
{
    /// <summary>
    /// Degrees added to the spread of guns this rider holds while the vehicle stands still.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ExtraSpread;

    /// <summary>
    /// The vehicle's speed, in fifths of its top speed, the rider's guns were last widened for.
    /// </summary>
    [ViewVariables]
    public int SpeedStep;
}

/// <summary>
/// A gun held by a <see cref="CMUVehicleRiderComponent"/>, widened by their riding spread. It kicks
/// no camera: the view jumping back looks like the vehicle being shoved.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUVehicleRiderGunComponent : Component
{
    [DataField, AutoNetworkedField]
    public float ExtraSpread;
}

/// <summary>
/// Anyone sitting in an open vehicle's seat, driver included. Their hard contacts are off and the
/// physics step leaves them on the strap.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUVehicleSeatedComponent : Component;
