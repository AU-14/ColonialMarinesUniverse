using Content.Shared._RMC14.Medical.Defibrillator;
using Content.Shared.Medical;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Content.Shared.PowerCell.Components;
using Content.Shared.Rounding;

namespace Content.Server._RMC14.Medical.Defibrillator;

public sealed partial class RMCDefibrillatorSystem : SharedRMCDefibrillatorSystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private PowerCellSystem _powerCell = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DefibrillatorComponent, PowerCellChangedEvent>(OnChangeCharge);
    }

    private void OnChangeCharge(Entity<DefibrillatorComponent> entity, ref PowerCellChangedEvent args)
    {
        if (!_powerCell.TryGetBatteryFromSlot(entity.Owner, out var battery) || !TryComp<PowerCellDrawComponent>(entity, out var draw))
            return;

        var charge = _battery.GetCharge(battery.Value.AsNullable());
        var frac = charge / battery.Value.Comp.MaxCharge;
        var level = (byte)ContentHelpers.RoundToLevels(frac, 1, (int)DefibrillatorChargeVisuals.Full);
        level = (byte)Math.Ceiling(frac * (int)DefibrillatorChargeVisuals.Full);
        if (charge < draw.UseCharge)
            level = 0;

        if (!TryComp<AppearanceComponent>(entity, out var appearance))
            return;
        _appearance.SetData(entity, DefibrillatorVisuals.DefibrillatorCharge, (DefibrillatorChargeVisuals)level, appearance);
    }
}
