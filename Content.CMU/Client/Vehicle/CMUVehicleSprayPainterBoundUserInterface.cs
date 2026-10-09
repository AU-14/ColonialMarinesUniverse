using Content.Shared.Charges.Components;
using Content.Shared.Charges.Systems;
using Content.Shared.CMU14.Vehicle.Jeep;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.Vehicle;

[UsedImplicitly]
public sealed class CMUVehicleSprayPainterBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private CMUVehicleSprayPainterWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<CMUVehicleSprayPainterWindow>();
        _window.OnColorPicked += color => SendPredictedMessage(new CMUVehicleSprayPainterColorMessage(color));

        // The colour is only read on opening: the server echoing a slider back would fight the drag.
        if (EntMan.TryGetComponent(Owner, out CMUVehicleSprayPainterComponent? painter))
            _window.SetColor(painter.Color);

        Update();
    }

    public override void Update()
    {
        if (_window == null || !EntMan.TryGetComponent(Owner, out LimitedChargesComponent? charges))
            return;

        var current = EntMan.System<SharedChargesSystem>().GetCurrentCharges((Owner, charges));
        _window.SetCharges(current, charges.MaxCharges);
    }
}
