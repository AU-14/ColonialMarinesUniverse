using System.Linq;
using Content.Shared.Buckle.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Vehicle.Open;

/// <summary>
/// Which hand guns a rider can fire from their seat: the driver only a sidearm, held in one hand while
/// the other steers, and a gunner none, their hands on the mounted gun. Neither can wield anything in
/// both hands, and sitting down there lets go of a wielded one. Whatever they may fire still scatters
/// as a rider's does.
/// </summary>
public sealed class CMUVehicleSeatGunsSystem : EntitySystem
{
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private TagSystem _tags = default!;
    [Dependency] private SharedWieldableSystem _wieldable = default!;

    private static readonly ProtoId<TagPrototype> Sidearm = "Sidearm";
    private static readonly TimeSpan PopupDelay = TimeSpan.FromSeconds(1);

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUVehicleRiderGunComponent, ShotAttemptedEvent>(OnShotAttempted);
        SubscribeLocalEvent<CMUVehicleRiderComponent, WieldAttemptEvent>(OnWieldAttempt);
    }

    private void OnWieldAttempt(Entity<CMUVehicleRiderComponent> ent, ref WieldAttemptEvent args)
    {
        if (args.Cancelled || CanWield(ent) is not { } refusal)
            return;

        args.Cancel();
        args.Message = Loc.GetString(refusal);
    }

    /// <summary>
    /// Why a rider can't use both hands in their seat, or null if they can.
    /// </summary>
    public string? CanWield(EntityUid rider)
    {
        if (!TryComp(rider, out BuckleComponent? buckle) || !TryComp(buckle.BuckledTo, out CMUVehicleSeatComponent? seat))
            return null;

        return seat.Guns switch
        {
            CMUVehicleSeatGuns.None => "cmu-vehicle-seat-guns-none",
            CMUVehicleSeatGuns.Sidearms => "cmu-vehicle-seat-wield-driver",
            _ => null,
        };
    }

    /// <summary>
    /// Lets go of anything the rider wields with both hands, if their seat needs a hand.
    /// </summary>
    public void LetGoOfWielded(EntityUid rider)
    {
        if (CanWield(rider) == null)
            return;

        // Letting go takes the wielding's placeholder out of the other hand.
        foreach (var held in _hands.EnumerateHeld(rider).ToList())
        {
            if (TryComp(held, out WieldableComponent? wieldable) && wieldable.Wielded)
                _wieldable.TryUnwield((held, wieldable), rider, force: true);
        }
    }

    private void OnShotAttempted(Entity<CMUVehicleRiderGunComponent> ent, ref ShotAttemptedEvent args)
    {
        if (args.Cancelled || CanFire(args.User, ent) is not { } refusal)
            return;

        args.Cancel();
        if (!TryComp(args.User, out CMUVehicleRiderComponent? rider) || _timing.CurTime < rider.NextGunPopup)
            return;

        rider.NextGunPopup = _timing.CurTime + PopupDelay;
        _popup.PopupClient(Loc.GetString(refusal), args.User, args.User, PopupType.SmallCaution);
    }

    /// <summary>
    /// Why a rider can't fire this gun from their seat, or null if they can.
    /// </summary>
    public string? CanFire(EntityUid rider, EntityUid gun)
    {
        if (!TryComp(rider, out BuckleComponent? buckle) || !TryComp(buckle.BuckledTo, out CMUVehicleSeatComponent? seat))
            return null;

        return seat.Guns switch
        {
            CMUVehicleSeatGuns.None => "cmu-vehicle-seat-guns-none",
            CMUVehicleSeatGuns.Sidearms when !_tags.HasTag(gun, Sidearm) => "cmu-vehicle-seat-guns-sidearms",
            _ => null,
        };
    }
}
