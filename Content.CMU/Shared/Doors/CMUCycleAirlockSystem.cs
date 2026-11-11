using Content.Shared.Doors;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Popups;
using Content.Shared.Prying.Components;
using Robust.Shared.Network;

namespace Content.Shared.CMU14.Doors;

/// <summary>
///     Runs cycling airlocks (<see cref="CMUCycleAirlockComponent"/>): a door won't open while another door in its cycle
///     is open, and opening one bolts the others until every door in the cycle is shut again.
/// </summary>
public sealed partial class CMUCycleAirlockSystem : EntitySystem
{
    [Dependency] private SharedDoorSystem _door = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private readonly List<Entity<CMUCycleAirlockComponent>> _linked = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUCycleAirlockComponent, BeforeDoorOpenedEvent>(OnBeforeOpened);
        SubscribeLocalEvent<CMUCycleAirlockComponent, BeforePryEvent>(OnBeforePry);
        SubscribeLocalEvent<CMUCycleAirlockComponent, DoorStateChangedEvent>(OnStateChanged);
    }

    private void OnBeforeOpened(EntityUid uid, CMUCycleAirlockComponent comp, BeforeDoorOpenedEvent args)
    {
        if (args.Cancelled || !OtherDoorOpen((uid, comp)))
            return;

        args.Cancel();
        if (args.User is { } user)
            _popup.PopupEntity(Loc.GetString("cmu-cycle-airlock-other-open"), uid, user, PopupType.SmallCaution);
    }

    private void OnBeforePry(Entity<CMUCycleAirlockComponent> ent, ref BeforePryEvent args)
    {
        // Forcing one open while the other end is open would vent the whole cycle.
        if (!args.Cancelled && OtherDoorOpen(ent))
            args.Cancelled = true;
    }

    private void OnStateChanged(EntityUid uid, CMUCycleAirlockComponent comp, DoorStateChangedEvent args)
    {
        // Bolting is the server's call; the client just sees the result.
        if (_net.IsClient)
            return;

        GetLinked((uid, comp));
        switch (args.State)
        {
            case DoorState.Opening:
                foreach (var other in _linked)
                {
                    if (!TryComp(other, out DoorBoltComponent? bolts) || bolts.BoltsDown)
                        continue;

                    if (_door.TrySetBoltDown((other, bolts), true))
                    {
                        other.Comp.BoltedByCycle = true;
                        Dirty(other);
                    }
                }

                break;
            case DoorState.Closed:
                foreach (var other in _linked)
                {
                    if (IsOpen(other))
                        return;
                }

                // Every door in the cycle is shut, so the ones we bolted can open again.
                foreach (var other in _linked)
                {
                    if (!other.Comp.BoltedByCycle)
                        continue;

                    other.Comp.BoltedByCycle = false;
                    Dirty(other);
                    if (TryComp(other, out DoorBoltComponent? bolts))
                        _door.SetBoltsDown((other, bolts), false);
                }

                break;
        }
    }

    private bool OtherDoorOpen(Entity<CMUCycleAirlockComponent> ent)
    {
        GetLinked(ent);
        foreach (var other in _linked)
        {
            if (IsOpen(other))
                return true;
        }

        return false;
    }

    private bool IsOpen(EntityUid uid)
    {
        return TryComp(uid, out DoorComponent? door) &&
               door.State is DoorState.Opening or DoorState.Open or DoorState.Closing or DoorState.Emagging;
    }

    /// <summary>Fills <see cref="_linked"/> with every other door in this door's cycle.</summary>
    private void GetLinked(Entity<CMUCycleAirlockComponent> ent)
    {
        _linked.Clear();
        var xform = Transform(ent);
        if (xform.GridUid is not { } grid)
            return;

        var position = _transform.GetWorldPosition(xform);
        var query = EntityQueryEnumerator<CMUCycleAirlockComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var other, out var otherXform))
        {
            if (uid == ent.Owner || otherXform.GridUid != grid)
                continue;

            if (ent.Comp.Group != null)
            {
                if (other.Group == ent.Comp.Group)
                    _linked.Add((uid, other));

                continue;
            }

            if (other.Group == null &&
                (_transform.GetWorldPosition(otherXform) - position).Length() <= ent.Comp.AutoLinkRange)
            {
                _linked.Add((uid, other));
            }
        }
    }
}
