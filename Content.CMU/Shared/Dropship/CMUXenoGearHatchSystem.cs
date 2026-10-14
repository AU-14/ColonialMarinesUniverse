using Content.Shared._RMC14.Xenonids;
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
///     the dropship's walkway panels (<see cref="CMUXenoGearHatchExitComponent"/>).
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
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private readonly List<EntityUid> _exits = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUXenoGearHatchComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<CMUXenoGearHatchComponent, CMUXenoGearHatchClimbDoAfterEvent>(OnClimbDoAfter);
    }

    private void OnInteractHand(Entity<CMUXenoGearHatchComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled || !HasComp<XenoComponent>(args.User))
            return;

        args.Handled = true;

        if (InFlight(ent))
        {
            _popup.PopupClient(Loc.GetString("cmu-xeno-gear-hatch-in-flight"), ent, args.User);
            return;
        }

        if (PickExit(ent) is not { } exit)
        {
            _popup.PopupClient(Loc.GetString("cmu-xeno-gear-hatch-no-exit"), ent, args.User);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, args.User, ent.Comp.Delay, new CMUXenoGearHatchClimbDoAfterEvent(), ent, target: exit, used: ent)
        {
            BreakOnMove = true,
            DistanceThreshold = null,
            NeedHand = false,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        _popup.PopupClient(Loc.GetString("cmu-xeno-gear-hatch-start"), ent, args.User);

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

    private bool InFlight(EntityUid hatch)
    {
        return Transform(hatch).GridUid is { } grid &&
               TryComp(grid, out FTLComponent? ftl) &&
               ftl.State is FTLState.Starting or FTLState.Travelling or FTLState.Arriving;
    }

    private EntityUid? PickExit(EntityUid hatch)
    {
        if (Transform(hatch).GridUid is not { } grid)
            return null;

        _exits.Clear();
        var query = EntityQueryEnumerator<CMUXenoGearHatchExitComponent, TransformComponent>();
        while (query.MoveNext(out var exit, out _, out var xform))
        {
            if (xform.GridUid == grid)
                _exits.Add(exit);
        }

        return _exits.Count == 0 ? null : _random.Pick(_exits);
    }
}
