using Content.Shared._RMC14.PowerLoader;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Containers;

namespace Content.Shared.CMU14.PowerLoader;

/// <summary>
/// Power-loader style lifting for <see cref="CMULargeObjectGrabberComponent"/> mobs: clicking a
/// <see cref="PowerLoaderGrabbableComponent"/> with an empty hand lifts it into a free hand after the grabbable's delay.
/// The object is then dropped and placed like any held item.
/// </summary>
public sealed partial class CMULargeObjectGrabberSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMULargeObjectGrabberComponent, UserInteractHandEvent>(OnUserInteractHand);
        SubscribeLocalEvent<CMULargeObjectGrabberComponent, CMULargeObjectGrabDoAfterEvent>(OnGrabDoAfter);
    }

    private void OnUserInteractHand(Entity<CMULargeObjectGrabberComponent> ent, ref UserInteractHandEvent args)
    {
        if (args.Handled || !HasComp<PowerLoaderGrabbableComponent>(args.Target))
            return;

        args.Handled = TryStartGrab(ent, args.Target);
    }

    /// <summary>
    /// Starts the lift do-after. Returns true if the interaction was consumed (including a refusal popup).
    /// </summary>
    public bool TryStartGrab(EntityUid user, EntityUid target)
    {
        if (!HasComp<CMULargeObjectGrabberComponent>(user) ||
            !TryComp(target, out PowerLoaderGrabbableComponent? grabbable))
        {
            return false;
        }

        if (!CanGrab(user, target, true))
            return true;

        var ev = new CMULargeObjectGrabDoAfterEvent();
        var doAfter = new DoAfterArgs(EntityManager, user, grabbable.Delay, ev, user, target)
        {
            BreakOnMove = true,
            DuplicateCondition = DuplicateConditions.SameEvent,
            DistanceThreshold = 1.5f,
        };

        // zero-delay do-afters finish inside TryStartDoAfter, so there is nothing to announce
        if (!_doAfter.TryStartDoAfter(doAfter) || grabbable.Delay <= TimeSpan.Zero)
            return true;

        _popup.PopupEntity(
            Loc.GetString("cmu-large-object-grab-start-self", ("target", target)),
            Loc.GetString("cmu-large-object-grab-start-others", ("user", user), ("target", target)),
            user,
            user);

        return true;
    }

    private void OnGrabDoAfter(Entity<CMULargeObjectGrabberComponent> ent, ref CMULargeObjectGrabDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target)
            return;

        args.Handled = true;
        if (!CanGrab(ent, target, true) ||
            !_hands.TryGetEmptyHand(ent.Owner, out var hand))
        {
            return;
        }

        // Crates have no ItemComponent and WhitelistPickup would refuse them, so insert directly like the power loader.
        _hands.DoPickup(ent, hand, target);
    }

    private bool CanGrab(EntityUid user, EntityUid target, bool popup)
    {
        if (TerminatingOrDeleted(target) || _container.IsEntityInContainer(target))
            return false;

        if (Transform(target).Anchored)
        {
            if (popup)
                _popup.PopupEntity(Loc.GetString("cmu-large-object-grab-anchored", ("target", target)), target, user, PopupType.SmallCaution);

            return false;
        }

        if (!_hands.TryGetEmptyHand(user, out _))
        {
            if (popup)
                _popup.PopupEntity(Loc.GetString("cmu-large-object-grab-hands-full", ("target", target)), target, user, PopupType.SmallCaution);

            return false;
        }

        return true;
    }
}
