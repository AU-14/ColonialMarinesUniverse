using Content.Shared.Administration.Logs;
using Content.Shared.Charges.Systems;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.SprayPainter.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Vehicle.Jeep;

/// <summary>
/// Respraying a vehicle with a vehicle spray painter: the colour picked in its window becomes the
/// paint of the part it is used on (the body outside, the tub inside, the seats or the wheels) after a
/// do-after, for some of its charges.
/// </summary>
public sealed class CMUVehiclePaintSystem : EntitySystem
{
    [Dependency] private ISharedAdminLogManager _adminLog = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedChargesSystem _charges = default!;
    [Dependency] private SharedCMUVehicleCrayonSystem _crayon = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUVehiclePaintComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<CMUVehiclePaintComponent, CMUVehiclePaintDoAfterEvent>(OnPaint);
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

    private static int Cost(CMUVehiclePaintComponent paint, CMUVehiclePaintZone zone)
    {
        return zone switch
        {
            CMUVehiclePaintZone.Interior => paint.InteriorChargeCost,
            CMUVehiclePaintZone.Seats => paint.SeatChargeCost,
            CMUVehiclePaintZone.Wheels => paint.WheelChargeCost,
            _ => paint.ChargeCost,
        };
    }

    private static TimeSpan Delay(CMUVehiclePaintComponent paint, CMUVehiclePaintZone zone)
    {
        return zone switch
        {
            CMUVehiclePaintZone.Interior => paint.InteriorDelay,
            CMUVehiclePaintZone.Seats => paint.SeatDelay,
            CMUVehiclePaintZone.Wheels => paint.WheelDelay,
            _ => paint.Delay,
        };
    }

    /// <summary>
    /// Starts respraying one part of the vehicle in the painter's colour.
    /// </summary>
    public bool TryRespray(Entity<CMUVehiclePaintComponent?> ent, EntityUid user, EntityUid used, CMUVehiclePaintZone zone)
    {
        if (!Resolve(ent, ref ent.Comp, false) || !TryComp(used, out CMUVehicleSprayPainterComponent? painter))
            return false;

        if (!_charges.HasCharges(used, Cost(ent.Comp, zone)))
        {
            _popup.PopupClient(Loc.GetString("cmu-vehicle-paint-empty"), ent, user);
            return false;
        }

        var color = painter.Color.WithAlpha(1f);
        var doAfter = new DoAfterArgs(EntityManager, user, Delay(ent.Comp, zone), new CMUVehiclePaintDoAfterEvent(color, zone), ent, ent, used)
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
        if (!_charges.TryUseCharges(used, Cost(ent.Comp, args.Zone)))
        {
            _popup.PopupClient(Loc.GetString("cmu-vehicle-paint-empty"), ent, args.User);
            return;
        }

        switch (args.Zone)
        {
            case CMUVehiclePaintZone.Interior:
                ent.Comp.InteriorColor = args.Color;
                break;
            case CMUVehiclePaintZone.Seats:
                ent.Comp.SeatColor = args.Color;
                break;
            case CMUVehiclePaintZone.Wheels:
                ent.Comp.WheelColor = args.Color;
                break;
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
            CMUVehiclePaintZone.Interior => "cmu-vehicle-paint-interior-done",
            CMUVehiclePaintZone.Seats => "cmu-vehicle-paint-seats-done",
            CMUVehiclePaintZone.Wheels => "cmu-vehicle-paint-wheels-done",
            _ => "cmu-vehicle-paint-done",
        };
        _popup.PopupClient(Loc.GetString(done, ("vehicle", ent.Owner)), ent, args.User);
        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(args.User):user} repainted {ToPrettyString(ent):target} {args.Zone} {args.Color.ToHex()}");
    }
}
