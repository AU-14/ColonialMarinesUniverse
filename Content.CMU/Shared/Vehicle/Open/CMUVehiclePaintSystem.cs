using System.Diagnostics.CodeAnalysis;
using Content.Shared.Administration.Logs;
using Content.Shared.Charges.Systems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.SprayPainter.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Vehicle.Open;

/// <summary>
/// Respraying a vehicle with a vehicle spray painter: the colour picked in its window becomes the
/// paint of the part it is used on (the body outside, the tub inside, the seats or the wheels) after a
/// do-after, for some of its charges. Switched to its rim designs, the painter sprays the design picked
/// on the wheels, its base colour as their paint; the plain factory design takes a design off. The
/// wheels' paint and design go on the fitted road wheel set, never the spare.
/// </summary>
public sealed class CMUVehiclePaintSystem : EntitySystem
{
    [Dependency] private ISharedAdminLogManager _adminLog = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedChargesSystem _charges = default!;
    [Dependency] private SharedCMUVehicleCrayonSystem _crayon = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUVehiclePaintComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<CMUVehiclePaintComponent, CMUVehiclePaintDoAfterEvent>(OnPaint);
        SubscribeLocalEvent<CMUVehiclePaintComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<CMUVehicleWheelPaintComponent, ExaminedEvent>(OnWheelsExamined);
    }

    private void OnExamined(Entity<CMUVehiclePaintComponent> ent, ref ExaminedEvent args)
    {
        if (TryGetWheelPaint(ent, ent.Comp.WheelSlot, out var wheels) && RimName(wheels.Value.Comp) is { } name)
            args.PushMarkup(Loc.GetString("cmu-vehicle-paint-rims-examine", ("design", name)));
    }

    private void OnWheelsExamined(Entity<CMUVehicleWheelPaintComponent> ent, ref ExaminedEvent args)
    {
        if (RimName(ent.Comp) is { } name)
            args.PushMarkup(Loc.GetString("cmu-vehicle-wheel-rims-examine", ("design", name)));
    }

    private string? RimName(CMUVehicleWheelPaintComponent wheels)
    {
        return wheels.RimDesign is { } id && _prototypes.TryIndex(id, out var design) ? Loc.GetString(design.Name) : null;
    }

    /// <summary>
    /// The paint of the wheel set fitted in one of a vehicle's slots.
    /// </summary>
    public bool TryGetWheelPaint(EntityUid vehicle, string slot, [NotNullWhen(true)] out Entity<CMUVehicleWheelPaintComponent>? wheels)
    {
        wheels = null;
        if (_itemSlots.GetItemOrNull(vehicle, slot) is not { } item || !TryComp(item, out CMUVehicleWheelPaintComponent? paint))
            return false;

        wheels = (item, paint);
        return true;
    }

    private void OnInteractUsing(Entity<CMUVehiclePaintComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !HasComp<CMUVehicleSprayPainterComponent>(args.Used))
            return;

        args.Handled = true;
        TryRespray(ent.AsNullable(), args.User, args.Used, BodyZone(ent, args.ClickLocation));
    }

    /// <summary>
    /// The tub inside where a click on the body lands on it, else the body outside.
    /// </summary>
    private CMUVehiclePaintZone BodyZone(Entity<CMUVehiclePaintComponent> ent, Robust.Shared.Map.EntityCoordinates click)
    {
        if (!TryComp(ent, out CMUVehicleCrayonComponent? crayon) ||
            !_crayon.TryGetClickedSurface((ent, crayon), click, out _, out var voxel, out _, out _))
        {
            return CMUVehiclePaintZone.Body;
        }

        var min = ent.Comp.InteriorMin;
        var max = ent.Comp.InteriorMax;
        return voxel.X >= min.X && voxel.X <= max.X && voxel.Y >= min.Y && voxel.Y <= max.Y && voxel.Z >= min.Z && voxel.Z <= max.Z
            ? CMUVehiclePaintZone.Interior
            : CMUVehiclePaintZone.Body;
    }

    private static int Cost(CMUVehiclePaintComponent paint, CMUVehiclePaintZone zone, bool design = false)
    {
        return zone switch
        {
            CMUVehiclePaintZone.Wheels when design => paint.DesignChargeCost,
            CMUVehiclePaintZone.Interior => paint.InteriorChargeCost,
            CMUVehiclePaintZone.Seats => paint.SeatChargeCost,
            CMUVehiclePaintZone.Wheels => paint.WheelChargeCost,
            _ => paint.ChargeCost,
        };
    }

    private static TimeSpan Delay(CMUVehiclePaintComponent paint, CMUVehiclePaintZone zone, bool design = false)
    {
        return zone switch
        {
            CMUVehiclePaintZone.Wheels when design => paint.DesignDelay,
            CMUVehiclePaintZone.Interior => paint.InteriorDelay,
            CMUVehiclePaintZone.Seats => paint.SeatDelay,
            CMUVehiclePaintZone.Wheels => paint.WheelDelay,
            _ => paint.Delay,
        };
    }

    /// <summary>
    /// Starts respraying one part of the vehicle in the painter's colour, or its wheels in its rim design.
    /// </summary>
    public bool TryRespray(Entity<CMUVehiclePaintComponent?> ent, EntityUid user, EntityUid used, CMUVehiclePaintZone zone)
    {
        if (!Resolve(ent, ref ent.Comp, false) || !TryComp(used, out CMUVehicleSprayPainterComponent? painter))
            return false;

        var design = painter.Mode == CMUVehicleSprayPainterMode.Design;
        if (design && zone != CMUVehiclePaintZone.Wheels)
        {
            _popup.PopupClient(Loc.GetString("cmu-vehicle-paint-design-wheels"), ent, user);
            return false;
        }

        if (zone == CMUVehiclePaintZone.Wheels && _itemSlots.GetItemOrNull(ent.Owner, ent.Comp.WheelSlot) == null)
        {
            _popup.PopupClient(Loc.GetString("cmu-vehicle-paint-no-wheels", ("vehicle", ent.Owner)), ent, user);
            return false;
        }

        if (!_charges.HasCharges(used, Cost(ent.Comp, zone, design)))
        {
            _popup.PopupClient(Loc.GetString("cmu-vehicle-paint-empty"), ent, user);
            return false;
        }

        CMUVehiclePaintDoAfterEvent ev;
        if (design)
        {
            if (!_prototypes.TryIndex(painter.Design, out var rim))
                return false;

            ev = new CMUVehiclePaintDoAfterEvent(painter.Design, CMUVehicleSprayPainterSystem.DesignColors(painter, rim));
        }
        else
        {
            ev = new CMUVehiclePaintDoAfterEvent(painter.Color.WithAlpha(1f), zone);
        }

        var doAfter = new DoAfterArgs(EntityManager, user, Delay(ent.Comp, zone, design), ev, ent, ent, used)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            BlockDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameTarget,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return false;

        _audio.PlayPredicted(painter.SpraySound, ent, user);
        return true;
    }

    private void OnPaint(Entity<CMUVehiclePaintComponent> ent, ref CMUVehiclePaintDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Used is not { } used)
            return;

        args.Handled = true;
        CMURimDesignPrototype? design = null;
        if (args.Design is { } id &&
            (!_prototypes.TryIndex(id, out design) || args.Colors.Count != CMUVehicleSprayPainterSystem.DesignColorCount))
        {
            return;
        }

        EntityUid? wheelSet = null;
        if (args.Zone == CMUVehiclePaintZone.Wheels && (wheelSet = _itemSlots.GetItemOrNull(ent.Owner, ent.Comp.WheelSlot)) == null)
            return;

        if (!_charges.TryUseCharges(used, Cost(ent.Comp, args.Zone, design != null)))
        {
            _popup.PopupClient(Loc.GetString("cmu-vehicle-paint-empty"), ent, args.User);
            return;
        }

        switch (args.Zone)
        {
            case CMUVehiclePaintZone.Wheels when design != null:
            {
                var wheels = EnsureComp<CMUVehicleWheelPaintComponent>(wheelSet!.Value);
                wheels.Color = args.Colors[0];
                wheels.RimDesign = design.Factory ? null : design.ID;
                wheels.RimColors = design.Factory ? new List<Color>() : args.Colors.GetRange(1, 3);
                Dirty(wheelSet.Value, wheels);
                break;
            }
            case CMUVehiclePaintZone.Interior:
                ent.Comp.InteriorColor = args.Color;
                break;
            case CMUVehiclePaintZone.Seats:
                ent.Comp.SeatColor = args.Color;
                break;
            case CMUVehiclePaintZone.Wheels:
            {
                var wheels = EnsureComp<CMUVehicleWheelPaintComponent>(wheelSet!.Value);
                wheels.Color = args.Color;
                Dirty(wheelSet.Value, wheels);
                break;
            }
            default:
                ent.Comp.Color = args.Color;
                break;
        }

        Dirty(ent);

        if (TryComp(used, out CMUVehicleSprayPainterComponent? painter))
        {
            var painted = EnsureComp<PaintedComponent>(ent);
            painted.DryTime = _timing.CurTime + painter.FreshPaintDuration;
            Dirty(ent, painted);
            _audio.PlayPredicted(painter.SpraySound, ent, args.User);
        }

        var done = args.Zone switch
        {
            CMUVehiclePaintZone.Wheels when design != null => "cmu-vehicle-paint-design-done",
            CMUVehiclePaintZone.Interior => "cmu-vehicle-paint-interior-done",
            CMUVehiclePaintZone.Seats => "cmu-vehicle-paint-seats-done",
            CMUVehiclePaintZone.Wheels => "cmu-vehicle-paint-wheels-done",
            _ => "cmu-vehicle-paint-done",
        };
        var designName = design == null ? string.Empty : Loc.GetString(design.Name);
        _popup.PopupClient(Loc.GetString(done, ("vehicle", ent.Owner), ("design", designName)), ent, args.User);
        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(args.User):user} repainted {ToPrettyString(ent):target} {args.Zone} {args.Color.ToHex()} {design?.ID ?? string.Empty}");
    }
}
