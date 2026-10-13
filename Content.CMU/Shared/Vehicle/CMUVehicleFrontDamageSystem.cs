using Content.Shared._RMC14.Vehicle;
using Content.Shared.CMU14.Vehicle.Open;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Tools.Systems;
using Robust.Shared.Map;

namespace Content.Shared.CMU14.Vehicle;

/// <summary>
/// Crumples a vehicle's front in head on crashes, and straightens it under a welder used on the front.
/// </summary>
public sealed class CMUVehicleFrontDamageSystem : EntitySystem
{
    [Dependency] private SharedCMUVehicleCrayonSystem _crayon = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedToolSystem _tool = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUVehicleFrontDamageComponent, CMUVehicleCrashedEvent>(OnCrashed);
        SubscribeLocalEvent<CMUVehicleFrontDamageComponent, InteractUsingEvent>(OnInteractUsing,
            before: [typeof(HardpointSystem)]);
        SubscribeLocalEvent<CMUVehicleFrontDamageComponent, CMUVehicleFrontRepairDoAfterEvent>(OnRepair);
        SubscribeLocalEvent<CMUVehicleFrontDamageComponent, ExaminedEvent>(OnExamined);
    }

    /// <summary>
    /// How crumpled the front is: 0 straight, then one more for each threshold its damage has passed.
    /// </summary>
    public int GetLevel(Entity<CMUVehicleFrontDamageComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return 0;

        var level = 0;
        foreach (var threshold in ent.Comp.Thresholds)
        {
            if (ent.Comp.Damage >= threshold)
                level++;
        }

        return level;
    }

    /// <summary>
    /// Adds damage to the front, up to its most.
    /// </summary>
    public void Damage(Entity<CMUVehicleFrontDamageComponent> ent, float amount)
    {
        var damage = Math.Clamp(ent.Comp.Damage + amount, 0f, ent.Comp.MaxDamage);
        if (MathHelper.CloseTo(damage, ent.Comp.Damage))
            return;

        ent.Comp.Damage = damage;
        Dirty(ent);
    }

    private void OnCrashed(Entity<CMUVehicleFrontDamageComponent> ent, ref CMUVehicleCrashedEvent args)
    {
        if (args.Frontal)
            Damage(ent, args.HullDamage * ent.Comp.CrashShare);
    }

    private void OnInteractUsing(Entity<CMUVehicleFrontDamageComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled ||
            ent.Comp.Damage <= 0f ||
            !_tool.HasQuality(args.Used, ent.Comp.RepairQuality) ||
            !IsFront(ent, args.ClickLocation))
        {
            return;
        }

        args.Handled = true;
        _tool.UseTool(args.Used, args.User, ent, (float) ent.Comp.RepairDelay.TotalSeconds,
            ent.Comp.RepairQuality, new CMUVehicleFrontRepairDoAfterEvent(), fuel: ent.Comp.RepairFuel);
    }

    /// <summary>
    /// Whether a click lands on the vehicle's front: far enough forward on its body, read from its crayon
    /// map. A vehicle without one has no front to work.
    /// </summary>
    private bool IsFront(Entity<CMUVehicleFrontDamageComponent> ent, EntityCoordinates click)
    {
        return TryComp(ent, out CMUVehicleCrayonComponent? crayon) &&
               _crayon.TryGetClickedSurface((ent, crayon), click, out _, out var voxel, out _, out _) &&
               voxel.X >= ent.Comp.FrontVoxel;
    }

    // Repairs repeat through the tool's wrapper do-after, which raises this same event instance on every
    // pass: Repeat has to go on the wrapper, and Handled is still set from the last pass, so it isn't checked.
    private void OnRepair(Entity<CMUVehicleFrontDamageComponent> ent, ref CMUVehicleFrontRepairDoAfterEvent args)
    {
        if (args.Cancelled)
            return;

        args.Handled = true;
        Damage(ent, -ent.Comp.RepairAmount);
        args.Repeat = ent.Comp.Damage > 0f;
        args.Args.Event.Repeat = args.Repeat;
        if (!args.Repeat)
            _popup.PopupClient(Loc.GetString("cmu-vehicle-front-straightened", ("vehicle", ent.Owner)), ent, args.User);
    }

    private void OnExamined(Entity<CMUVehicleFrontDamageComponent> ent, ref ExaminedEvent args)
    {
        if (GetLevel(ent.AsNullable()) is var level and > 0)
            args.PushMarkup(Loc.GetString($"cmu-vehicle-front-damage-{Math.Min(level, 3)}"));
    }
}
