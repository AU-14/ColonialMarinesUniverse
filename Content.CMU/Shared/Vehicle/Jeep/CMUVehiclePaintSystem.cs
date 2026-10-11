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
/// vehicle's paint, or its wheels' when sprayed on them, after a do-after, for some of its charges.
/// </summary>
public sealed class CMUVehiclePaintSystem : EntitySystem
{
    [Dependency] private ISharedAdminLogManager _adminLog = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedChargesSystem _charges = default!;
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
        TryRespray(ent.AsNullable(), args.User, args.Used, false);
    }

    /// <summary>
    /// Starts respraying the vehicle, or only its wheels, in the painter's colour.
    /// </summary>
    public bool TryRespray(Entity<CMUVehiclePaintComponent?> ent, EntityUid user, EntityUid used, bool wheels)
    {
        if (!Resolve(ent, ref ent.Comp, false) || !TryComp(used, out CMUVehicleSprayPainterComponent? painter))
            return false;

        if (!_charges.HasCharges(used, wheels ? ent.Comp.WheelChargeCost : ent.Comp.ChargeCost))
        {
            _popup.PopupClient(Loc.GetString("cmu-vehicle-paint-empty"), ent, user);
            return false;
        }

        var color = painter.Color.WithAlpha(1f);
        var delay = wheels ? ent.Comp.WheelDelay : ent.Comp.Delay;
        var doAfter = new DoAfterArgs(EntityManager, user, delay, new CMUVehiclePaintDoAfterEvent(color, wheels), ent, ent, used)
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
        if (!_charges.TryUseCharges(used, args.Wheels ? ent.Comp.WheelChargeCost : ent.Comp.ChargeCost))
        {
            _popup.PopupClient(Loc.GetString("cmu-vehicle-paint-empty"), ent, args.User);
            return;
        }

        if (args.Wheels)
            ent.Comp.WheelColor = args.Color;
        else
            ent.Comp.Color = args.Color;

        Dirty(ent);

        if (TryComp(used, out CMUVehicleSprayPainterComponent? painter))
        {
            var painted = EnsureComp<PaintedComponent>(ent);
            painted.DryTime = _timing.CurTime + painter.FreshPaintDuration;
            Dirty(ent, painted);
            _audio.PlayPredicted(painter.SpraySound, ent, args.User);
        }

        _popup.PopupClient(Loc.GetString(args.Wheels ? "cmu-vehicle-paint-wheels-done" : "cmu-vehicle-paint-done", ("vehicle", ent.Owner)), ent, args.User);
        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(args.User):user} repainted {ToPrettyString(ent):target}{(args.Wheels ? " wheels" : "")} {args.Color.ToHex()}");
    }
}
