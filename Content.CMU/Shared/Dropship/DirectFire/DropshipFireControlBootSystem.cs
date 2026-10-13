using System.Linq;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Dropship.AttachmentPoint;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared._RMC14.Tools;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Random;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Dropship.DirectFire;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DropshipFireControlBootComponent : Component
{
    [DataField, AutoNetworkedField] public bool Complete;
    [DataField, AutoNetworkedField] public TimeSpan ReadyAt;
    [DataField] public float FaultChance = 0.1f;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DropshipHardpointFaultComponent : Component
{
    [DataField, AutoNetworkedField] public bool Faulted;
}

[Serializable, NetSerializable]
public sealed partial class DropshipHardpointResetDoAfterEvent : SimpleDoAfterEvent;

public sealed partial class DropshipFireControlBootSystem : EntitySystem
{
    [Dependency] private SharedDropshipSystem _dropships = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<DropshipHardpointFaultComponent, InteractUsingEvent>(OnReset);
        SubscribeLocalEvent<DropshipHardpointFaultComponent, DropshipHardpointResetDoAfterEvent>(OnResetFinished);
        SubscribeLocalEvent<DropshipHardpointFaultComponent, ExaminedEvent>(OnExamine);
    }

    public void OnConsoleOpened(Entity<DropshipTerminalWeaponsComponent> console)
    {
        if (_net.IsClient || !_dropships.TryGetGridDropship(console, out var ship))
            return;
        if (!console.Comp.Gunnery && !HasComp<DropshipFireControlBootComponent>(ship))
            return;
        var boot = EnsureComp<DropshipFireControlBootComponent>(ship);
        if (!boot.Complete && boot.ReadyAt == TimeSpan.Zero && console.Comp.Gunnery)
        {
            boot.ReadyAt = _timing.CurTime + TimeSpan.FromSeconds(4);
            Dirty(ship, boot);
        }
        Sync(ship, boot);
    }

    public bool IsWeaponReady(EntityUid weapon)
    {
        if (_containers.TryGetContainingContainer(weapon, out var container) &&
            TryComp(container.Owner, out DropshipHardpointFaultComponent? fault) && fault.Faulted)
            return false;
        return !_dropships.TryGetGridDropship(weapon, out var ship) ||
               !TryComp(ship, out DropshipFireControlBootComponent? boot) || boot.Complete;
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;
        var query = EntityQueryEnumerator<DropshipFireControlBootComponent, DropshipComponent>();
        while (query.MoveNext(out var uid, out var boot, out var ship))
        {
            if (boot.Complete)
                continue;
            if (!boot.Complete && boot.ReadyAt != TimeSpan.Zero && _timing.CurTime >= boot.ReadyAt)
            {
                foreach (var point in ship.AttachmentPoints)
                {
                    string? slot = TryComp(point, out DropshipWeaponPointComponent? weaponPoint)
                        ? weaponPoint.WeaponContainerSlotId
                        : TryComp(point, out DropshipUtilityPointComponent? utilityPoint) ? utilityPoint.UtilitySlotId : null;
                    if (slot == null || !_containers.TryGetContainer(point, slot, out var container) ||
                        !container.ContainedEntities.Any(weapon => HasComp<DropshipWeaponComponent>(weapon)))
                        continue;
                    var fault = EnsureComp<DropshipHardpointFaultComponent>(point);
                    fault.Faulted = _random.Prob(boot.FaultChance);
                    Dirty(point, fault);
                }
                boot.Complete = true;
                Dirty(uid, boot);
            }
            Sync((uid, ship), boot);
        }
    }

    private void Sync(Entity<DropshipComponent> ship, DropshipFireControlBootComponent boot)
    {
        var faults = ship.Comp.AttachmentPoints.Where(point =>
            TryComp(point, out DropshipHardpointFaultComponent? fault) && fault.Faulted)
            .Select(point => Name(point)).ToArray();
        string status;
        if (!boot.Complete)
        {
            var remaining = (boot.ReadyAt - _timing.CurTime).TotalSeconds;
            if (remaining > 3.0)
                status = Loc.GetString("cmu-dropship-boot-stage-1");
            else if (remaining > 2.0)
                status = Loc.GetString("cmu-dropship-boot-stage-2");
            else if (remaining > 1.0)
                status = Loc.GetString("cmu-dropship-boot-stage-3");
            else
                status = Loc.GetString("cmu-dropship-boot-stage-4");
        }
        else
        {
            status = faults.Length == 0 ? Loc.GetString("cmu-dropship-boot-ready")
                : Loc.GetString("cmu-dropship-boot-faults", ("parts", string.Join(", ", faults)));
        }
        var query = EntityQueryEnumerator<DropshipTerminalWeaponsComponent>();
        while (query.MoveNext(out var uid, out var console))
        {
            if (!_dropships.TryGetGridDropship(uid, out var consoleShip) || consoleShip.Owner != ship.Owner ||
                console.FireControlBooting == !boot.Complete && console.FireControlStatus == status)
                continue;
            console.FireControlBooting = !boot.Complete;
            console.FireControlStatus = status;
            Dirty(uid, console);
        }
    }

    private void OnExamine(Entity<DropshipHardpointFaultComponent> point, ref ExaminedEvent args)
    {
        if (point.Comp.Faulted && args.IsInDetailsRange)
            args.PushMarkup(Loc.GetString("cmu-dropship-hardpoint-fault"));
    }

    private void OnReset(Entity<DropshipHardpointFaultComponent> point, ref InteractUsingEvent args)
    {
        if (args.Handled || !point.Comp.Faulted || !HasComp<MultitoolComponent>(args.Used))
            return;
        args.Handled = true;
        if (_net.IsClient)
            return;
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, TimeSpan.FromSeconds(3),
            new DropshipHardpointResetDoAfterEvent(), point, point, args.Used)
        {
            NeedHand = true, BreakOnMove = true, BreakOnDamage = true,
            BlockDuplicate = true,
        });
    }

    private void OnResetFinished(Entity<DropshipHardpointFaultComponent> point, ref DropshipHardpointResetDoAfterEvent args)
    {
        if (_net.IsClient || args.Handled || args.Cancelled || args.Used is not { } tool ||
            !HasComp<MultitoolComponent>(tool) || !point.Comp.Faulted)
            return;
        args.Handled = true;
        point.Comp.Faulted = false;
        Dirty(point);
        if (_dropships.TryGetGridDropship(point, out var ship) &&
            TryComp(ship, out DropshipFireControlBootComponent? boot))
            Sync(ship, boot);
        _popup.PopupEntity(Loc.GetString("cmu-dropship-hardpoint-reset"), point, args.User);
    }
}
