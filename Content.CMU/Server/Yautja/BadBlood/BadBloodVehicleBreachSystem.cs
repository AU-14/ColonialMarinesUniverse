using Content.Shared._RMC14.Vehicle;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.DoAfter;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Player;

namespace Content.Server.CMU14.Yautja.BadBlood;

/// <summary>
/// A Bad Blood rips a locked vehicle's hatch open with their bare hands: the Force hatch verb runs the same lock-break
/// do-after a marine runs with a cutting tool, only shorter and with no tool, so the clan can get at a crew that
/// shuts itself in. Once the lock is broken anyone can climb in, and the crew can repair it as usual.
/// </summary>
public sealed partial class BadBloodVehicleBreachSystem : EntitySystem
{
    private static readonly TimeSpan ForceTime = TimeSpan.FromSeconds(15);

    [Dependency] private BadBloodSystem _badBlood = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<VehicleLockComponent, GetVerbsEvent<Verb>>(OnLockVerbs);
    }

    private void OnLockVerbs(Entity<VehicleLockComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !CanForce(args.User, ent))
            return;

        var user = args.User;
        var vehicle = ent.Owner;
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("cmu-bad-blood-vehicle-force-verb"),
            Message = Loc.GetString("cmu-bad-blood-vehicle-force-verb-message", ("seconds", (int) ForceTime.TotalSeconds)),
            Act = () => TryStartForce(user, vehicle),
        });
    }

    private bool CanForce(EntityUid user, Entity<VehicleLockComponent> vehicle)
    {
        return _badBlood.IsBadBlood(user) && vehicle.Comp.Locked && !vehicle.Comp.Broken;
    }

    /// <summary>
    /// Starts forcing the hatch. VehicleLockSystem finishes it through its own lock-break handler.
    /// </summary>
    public bool TryStartForce(EntityUid user, EntityUid vehicle)
    {
        if (!TryComp(vehicle, out VehicleLockComponent? lockComp) || !CanForce(user, (vehicle, lockComp)))
            return false;

        var doAfter = new DoAfterArgs(EntityManager, user, ForceTime, new VehicleLockBreakDoAfterEvent(), vehicle, vehicle)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
            RequireCanInteract = true,
            BlockDuplicate = true,
            CancelDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameTarget | DuplicateConditions.SameEvent,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return false;

        _popup.PopupEntity(Loc.GetString("cmu-bad-blood-vehicle-force-start"), user, user);
        _popup.PopupEntity(
            Loc.GetString("cmu-bad-blood-vehicle-force-start-others"),
            vehicle,
            Filter.PvsExcept(user),
            true,
            PopupType.MediumCaution);
        return true;
    }
}
