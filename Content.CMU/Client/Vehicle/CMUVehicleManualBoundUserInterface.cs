using Content.Shared.CMU14.Vehicle.Open;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client.CMU14.Vehicle;

/// <summary>
/// Opens a vehicle manual's window. A manual is read alone: nothing goes back to the server.
/// </summary>
[UsedImplicitly]
public sealed class CMUVehicleManualBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [Dependency] private IPrototypeManager _prototypes = default!;

    private CMUVehicleManualWindow? _window;

    protected override void Open()
    {
        base.Open();
        if (!EntMan.TryGetComponent(Owner, out CMUVehicleManualComponent? manual) ||
            !_prototypes.TryIndex(manual.Manual, out var proto))
        {
            Close();
            return;
        }

        _window = this.CreateWindow<CMUVehicleManualWindow>();
        _window.Load(proto);
    }
}
