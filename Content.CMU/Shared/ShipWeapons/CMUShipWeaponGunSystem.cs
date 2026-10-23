using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Tools.Systems;

namespace Content.Shared.CMU14.ShipWeapons;

/// <summary>
///     Repairs orbital batteries after they fire: each of the battery's repair steps needs its own tool, done in
///     order, and the battery can fire again once the last one is finished.
/// </summary>
public sealed partial class CMUShipWeaponGunSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedToolSystem _tool = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUShipWeaponGunComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<CMUShipWeaponGunComponent, CMUShipWeaponRepairDoAfterEvent>(OnRepairDoAfter);
        SubscribeLocalEvent<CMUShipWeaponGunComponent, ExaminedEvent>(OnExamined);
    }

    /// <summary>Marks the battery as spent: it needs the whole repair again before it can fire.</summary>
    public void Spend(Entity<CMUShipWeaponGunComponent> ent)
    {
        ent.Comp.Ready = false;
        ent.Comp.RepairStep = 0;
        Dirty(ent);
    }

    private void OnInteractUsing(Entity<CMUShipWeaponGunComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || ent.Comp.Ready || ent.Comp.RepairStep >= ent.Comp.RepairSteps.Count)
            return;

        var step = ent.Comp.RepairSteps[ent.Comp.RepairStep];
        if (!_tool.HasQuality(args.Used, step.Quality))
            return;

        args.Handled = _tool.UseTool(args.Used, args.User, ent, step.Delay, step.Quality,
            new CMUShipWeaponRepairDoAfterEvent(ent.Comp.RepairStep), step.Fuel);
    }

    private void OnRepairDoAfter(Entity<CMUShipWeaponGunComponent> ent, ref CMUShipWeaponRepairDoAfterEvent args)
    {
        // The step can only count once, and only if nobody else finished it first.
        if (args.Cancelled || args.Handled || ent.Comp.Ready || args.Step != ent.Comp.RepairStep ||
            args.Step >= ent.Comp.RepairSteps.Count)
        {
            return;
        }

        args.Handled = true;
        var step = ent.Comp.RepairSteps[args.Step];
        ent.Comp.RepairStep++;
        if (ent.Comp.RepairStep >= ent.Comp.RepairSteps.Count)
        {
            ent.Comp.Ready = true;
            ent.Comp.RepairStep = 0;
            Dirty(ent);
            _popup.PopupEntity(Loc.GetString("cmu-ship-weapons-repair-complete"), ent, args.User);
            return;
        }

        Dirty(ent);
        _popup.PopupEntity(Loc.GetString(step.Done), ent, args.User);
    }

    private void OnExamined(Entity<CMUShipWeaponGunComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Ready)
        {
            args.PushMarkup(Loc.GetString("cmu-ship-weapons-gun-ready"));
            return;
        }

        if (ent.Comp.RepairStep >= ent.Comp.RepairSteps.Count)
            return;

        args.PushMarkup(Loc.GetString("cmu-ship-weapons-gun-needs-repair",
            ("step", ent.Comp.RepairStep + 1),
            ("steps", ent.Comp.RepairSteps.Count),
            ("hint", Loc.GetString(ent.Comp.RepairSteps[ent.Comp.RepairStep].Hint))));
    }
}
