using Content.Shared.Charges.Components;
using Content.Shared.Charges.Systems;
using Content.Shared.CMU14.Vehicle.Open;
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
        _window.OnModePicked += mode => SendPredictedMessage(new CMUVehicleSprayPainterModeMessage(mode));
        _window.OnDesignPicked += (design, colors) =>
            SendPredictedMessage(new CMUVehicleSprayPainterDesignMessage(design, colors));

        // The painter is only read on opening: the server echoing a slider back would fight the drag.
        if (EntMan.TryGetComponent(Owner, out CMUVehicleSprayPainterComponent? painter))
            _window.SetState(painter.Mode, painter.Color, painter.Design, painter.DesignColors);

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
