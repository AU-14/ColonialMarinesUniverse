using Content.Shared.Containers.ItemSlots;
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Vehicle.Jeep;

/// <summary>
/// An open vehicle's ignition: a key slot on the dash. The engine only runs, and the vehicle only
/// drives, with a key in it; taking the key out stops the engine. Only the vehicle's own key fits,
/// the one cut with its <see cref="KeyCode"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUVehicleIgnitionComponent : Component
{
    public const string SlotId = "jeep-ignition";

    [DataField(required: true)]
    public ItemSlot KeySlot = new();

    /// <summary>
    /// Whether the vehicle comes with its key in; a kit's chassis comes without, the key in the crate.
    /// </summary>
    [DataField]
    public bool StartWithKey = true;

    /// <summary>
    /// The code its keys are cut with; 0 until it has a key, when the first key put in it decides.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int KeyCode;
}

/// <summary>
/// A vehicle key, cut with the <see cref="Code"/> of the vehicle it starts; 0 is a blank, which only
/// goes into an ignition that has no key yet and becomes that vehicle's key.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUVehicleKeyComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Code;
}
