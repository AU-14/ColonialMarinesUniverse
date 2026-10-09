using Content.Shared.Containers.ItemSlots;
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Vehicle.Jeep;

/// <summary>
/// An open vehicle's ignition: a key slot on the dash. The engine only runs, and the vehicle only
/// drives, with a key in it; taking the key out stops the engine. Any of the vehicle family's keys
/// fits.
/// </summary>
[RegisterComponent, NetworkedComponent]
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
}
