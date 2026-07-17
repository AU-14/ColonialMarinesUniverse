using Content.Server.Emp;
using Content.Shared._CMU14.Yautja;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Robust.Shared.Player;

namespace Content.Server._CMU14.Yautja;

public sealed partial class YautjaBracerEmpSystem : EntitySystem
{
    private const string EmpObserverPopup = "You hear a hiss and crackle!";
    private const string EmpWearerPopup = "Your bracers hiss and spark!";

    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private YautjaCloakSystem _cloak = default!;
    [Dependency] private YautjaPowerSystem _power = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<InventoryComponent, EmpPulseEvent>(OnInventoryEmpPulse);
        SubscribeLocalEvent<YautjaBracerComponent, EmpPulseEvent>(OnBracerEmpPulse);
    }

    private void OnInventoryEmpPulse(Entity<InventoryComponent> ent, ref EmpPulseEvent args)
    {
        InventoryComponent? inventory = ent.Comp;
        if (!_inventory.TryGetInventoryEntity<YautjaBracerComponent>((ent.Owner, inventory), out var bracer) ||
            bracer.Comp == null)
        {
            return;
        }

        ApplyEmpDrain((bracer.Owner, bracer.Comp), ref args, ent.Owner);
        _cloak.ForceDecloak(ent.Owner);
    }

    private void OnBracerEmpPulse(Entity<YautjaBracerComponent> ent, ref EmpPulseEvent args)
    {
        if (_inventory.InSlotWithFlags((ent, null, null), ent.Comp.Slots))
            return;

        ApplyEmpDrain(ent, ref args, null);
    }

    private void ApplyEmpDrain(Entity<YautjaBracerComponent> bracer, ref EmpPulseEvent args, EntityUid? wearer)
    {
        var drain = GetCmss13EmpDrain(bracer.Comp, args.EnergyConsumption);
        if (drain <= FixedPoint2.Zero)
            return;

        args.Affected = true;
        _power.RemovePower(bracer, drain);
        PopupEmpAct(bracer.Owner, wearer);
    }

    private void PopupEmpAct(EntityUid bracer, EntityUid? wearer)
    {
        if (wearer is { } user)
        {
            _popup.PopupEntity(EmpWearerPopup, user, user, PopupType.LargeCaution);

            var filter = Filter.Pvs(user, entityManager: EntityManager)
                .RemoveWhereAttachedEntity(attached => attached == user);
            _popup.PopupEntity(EmpObserverPopup, user, filter, true, PopupType.LargeCaution);
            return;
        }

        _popup.PopupEntity(EmpObserverPopup, bracer, Filter.Pvs(bracer, entityManager: EntityManager), true, PopupType.LargeCaution);
    }

    private static FixedPoint2 GetCmss13EmpDrain(YautjaBracerComponent bracer, float energyConsumption)
    {
        if (bracer.EmpPowerDrain <= FixedPoint2.Zero)
            return FixedPoint2.Zero;

        if (bracer.EmpSeverityOneEnergy <= 0 || energyConsumption <= 0)
            return bracer.EmpPowerDrain;

        var sourceSeverity = MathF.Max(1f, bracer.EmpSeverityOneEnergy / energyConsumption);
        return FixedPoint2.New(bracer.EmpPowerDrain.Double() / sourceSeverity);
    }
}
