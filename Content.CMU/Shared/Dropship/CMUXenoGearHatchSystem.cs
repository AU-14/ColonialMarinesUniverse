using Content.Shared._RMC14.Xenonids;
using Content.Shared.CMU14.Dropship.MultiDeck;
using Content.Shared.DoAfter;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Random;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Dropship;

/// <summary>
///     A landing gear hatch a xeno can climb up into while the dropship is on the ground. They come out of one of
///     the dropship's walkway panels (<see cref="CMUXenoGearHatchExitComponent"/>), which are on the deck above.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUXenoGearHatchComponent : Component
{
    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(6);
}

/// <summary>
///     A walkway panel a xeno climbing up a landing gear hatch can come out of.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUXenoGearHatchExitComponent : Component;

[Serializable, NetSerializable]
public sealed partial class CMUXenoGearHatchClimbDoAfterEvent : SimpleDoAfterEvent;

public sealed class CMUXenoGearHatchSystem : EntitySystem
{
    /// <summary>
    ///     How close a xeno has to be to the hatch or its landing gear. The gear is solid and the hatch sits inside
    ///     it, so they're reached through it.
    /// </summary>
    private const float ReachRange = 2.5f;

    /// <summary>How far from the landing gear its hatch can be.</summary>
    private const float GearHatchRange = 2f;

    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private readonly List<EntityUid> _exits = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUXenoGearHatchComponent, InteractHandEvent>(OnHatchInteractHand);
        SubscribeLocalEvent<MohawkLandingGearComponent, InteractHandEvent>(OnGearInteractHand);
        SubscribeLocalEvent<CMUXenoGearHatchComponent, CMUXenoGearHatchClimbDoAfterEvent>(OnClimbDoAfter);
        SubscribeLocalEvent<XenoComponent, InRangeOverrideEvent>(OnXenoInRangeOverride);
    }

    private void OnXenoInRangeOverride(Entity<XenoComponent> ent, ref InRangeOverrideEvent args)
    {
        if (args.Handled ||
            !HasComp<CMUXenoGearHatchComponent>(args.Target) && !HasComp<MohawkLandingGearComponent>(args.Target))
        {
            return;
        }

        args.Handled = true;
        args.InRange = Within(args.User, args.Target, ReachRange);
    }

    private void OnHatchInteractHand(Entity<CMUXenoGearHatchComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled || !HasComp<XenoComponent>(args.User))
            return;

        args.Handled = true;
        TryClimb(ent, args.User);
    }

    private void OnGearInteractHand(Entity<MohawkLandingGearComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled || !HasComp<XenoComponent>(args.User) || GearHatch(ent) is not { } hatch)
            return;

        args.Handled = true;
        TryClimb(hatch, args.User);
    }

    private void TryClimb(Entity<CMUXenoGearHatchComponent> hatch, EntityUid xeno)
    {
        if (InFlight(hatch))
        {
            _popup.PopupClient(Loc.GetString("cmu-xeno-gear-hatch-in-flight"), hatch, xeno);
            return;
        }

        if (PickExit(hatch) is not { } exit)
        {
            _popup.PopupClient(Loc.GetString("cmu-xeno-gear-hatch-no-exit"), hatch, xeno);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, xeno, hatch.Comp.Delay, new CMUXenoGearHatchClimbDoAfterEvent(), hatch, target: exit, used: hatch)
        {
            BreakOnMove = true,
            DistanceThreshold = null,
            NeedHand = false,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        _popup.PopupClient(Loc.GetString("cmu-xeno-gear-hatch-start"), hatch, xeno);

        // Whoever is aboard gets a warning before anything comes out.
        if (_net.IsServer)
            _popup.PopupEntity(Loc.GetString("cmu-xeno-gear-hatch-rustling", ("panel", exit)), exit, PopupType.MediumCaution);
    }

    private void OnClimbDoAfter(Entity<CMUXenoGearHatchComponent> ent, ref CMUXenoGearHatchClimbDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } exit || !Exists(exit))
            return;

        args.Handled = true;

        if (InFlight(ent))
            return;

        _transform.SetCoordinates(args.User, Transform(exit).Coordinates);

        if (_net.IsServer)
        {
            _popup.PopupEntity(Loc.GetString("cmu-xeno-gear-hatch-burst",
                    ("xeno", Identity.Entity(args.User, EntityManager)), ("panel", exit)),
                exit, PopupType.LargeCaution);
        }
    }

    private bool Within(EntityUid user, EntityUid target, float range)
    {
        var userPos = _transform.GetMapCoordinates(user);
        var targetPos = _transform.GetMapCoordinates(target);
        return userPos.MapId == targetPos.MapId &&
               (userPos.Position - targetPos.Position).LengthSquared() <= range * range;
    }

    /// <summary>The hatch inside a landing gear, if it has one.</summary>
    private Entity<CMUXenoGearHatchComponent>? GearHatch(EntityUid gear)
    {
        var grid = Transform(gear).GridUid;
        var query = EntityQueryEnumerator<CMUXenoGearHatchComponent, TransformComponent>();
        while (query.MoveNext(out var hatch, out var comp, out var xform))
        {
            if (xform.GridUid == grid && Within(gear, hatch, GearHatchRange))
                return (hatch, comp);
        }

        return null;
    }

    /// <summary>
    ///     The grid that flies: a dropship's decks are separate grids, one z-level apart, that all belong to it.
    /// </summary>
    private EntityUid? Ship(EntityUid uid)
    {
        if (Transform(uid).GridUid is not { } grid)
            return null;

        return TryComp(grid, out DropshipDeckComponent? deck) && deck.Ship.IsValid() ? deck.Ship : grid;
    }

    private bool InFlight(EntityUid hatch)
    {
        return Ship(hatch) is { } ship &&
               TryComp(ship, out FTLComponent? ftl) &&
               ftl.State is FTLState.Starting or FTLState.Travelling or FTLState.Arriving;
    }

    private EntityUid? PickExit(EntityUid hatch)
    {
        if (Ship(hatch) is not { } ship)
            return null;

        _exits.Clear();
        var query = EntityQueryEnumerator<CMUXenoGearHatchExitComponent>();
        while (query.MoveNext(out var exit, out _))
        {
            if (Ship(exit) == ship)
                _exits.Add(exit);
        }

        return _exits.Count == 0 ? null : _random.Pick(_exits);
    }
}
