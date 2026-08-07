using Content.Server._CMU14.Round.Objectives;
using Content.Shared._RMC14.Vendors;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Content.Shared.PowerCell.Components;
using Robust.Server.GameObjects;

namespace Content.Server._RMC14.Vendors;

public sealed partial class CMAutomatedVendorSystem : SharedCMAutomatedVendorSystem
{
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private ObjectiveControlSystem _objectiveSystem = default!;
    [Dependency] private PowerCellSystem _powerCell = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMAutomatedVendorComponent, ComponentStartup>(OnVendorStartup);
    }

    private void OnVendorStartup(EntityUid uid, CMAutomatedVendorComponent vendor, ComponentStartup args)
    {
        if (!vendor.UseObjectivePoints)
            return;

        vendor.CachedFactionWinPoints = _objectiveSystem.GetWinPoints(vendor.Faction).current;
        Dirty(uid, vendor);
    }

    protected override void OnVendBui(Entity<CMAutomatedVendorComponent> vendor, ref CMVendorVendBuiMsg args)
    {
        base.OnVendBui(vendor, ref args);

        var msg = new CMVendorRefreshBuiMsg();
        _ui.ServerSendUiMessage(vendor.Owner, args.UiKey, msg, args.Actor);
    }

    protected override (float currentCharge, float maxCharge) GetBatteryCharge(EntityUid item, PowerCellSlotComponent powerCellSlot)
    {
        return _powerCell.TryGetBatteryFromSlot((item, powerCellSlot), out var battery)
            ? (_battery.GetCharge(battery.Value.AsNullable()), battery.Value.Comp.MaxCharge)
            : (0, 0);
    }
}
