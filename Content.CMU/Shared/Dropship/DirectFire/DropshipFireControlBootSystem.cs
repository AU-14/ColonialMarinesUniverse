using System.Linq;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Dropship.AttachmentPoint;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Tools;
using Content.Shared.Administration.Logs;
using Content.Shared.CCVar;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
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
    [DataField, AutoNetworkedField] public bool LandingReboot;
    [DataField] public bool CrashLanding;
    [DataField] public TimeSpan Duration = TimeSpan.FromSeconds(4);
    [DataField] public SoundSpecifier BootSound = new SoundPathSpecifier("/Audio/_RMC14/Medical/reset_key_boot_on.ogg");
    [DataField] public SoundSpecifier ReadySound = new SoundPathSpecifier("/Audio/Machines/twobeep.ogg");
    [DataField] public SoundSpecifier FaultSound = new SoundPathSpecifier("/Audio/Machines/warning_buzzer.ogg");
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DropshipHardpointFaultComponent : Component
{
    [DataField, AutoNetworkedField] public bool Faulted;
    [DataField, AutoNetworkedField] public string? Reason;

    /// <summary>Firing engagements since the hardpoint was last serviced.</summary>
    [DataField] public int Uses;

    /// <summary>Landing reboots survived since the hardpoint was last serviced.</summary>
    [DataField] public int Landings;

    [DataField] public TimeSpan LastUse;

    /// <summary>Shots closer together than this count as one engagement.</summary>
    [DataField] public TimeSpan UseWindow = TimeSpan.FromSeconds(5);

    [DataField] public SoundSpecifier RepairSound = new SoundPathSpecifier("/Audio/Machines/quickbeep.ogg");
}

[Serializable, NetSerializable]
public sealed partial class DropshipHardpointResetDoAfterEvent : SimpleDoAfterEvent;

public sealed partial class DropshipFireControlBootSystem : EntitySystem
{
    [Dependency] private ISharedAdminLogManager _adminLog = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private SharedDropshipSystem _dropships = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SkillsSystem _skills = default!;
    [Dependency] private IGameTiming _timing = default!;

    public static readonly string[] FaultReasons =
    [
        "cmu-dropship-fault-reason-gyro",
        "cmu-dropship-fault-reason-feed",
        "cmu-dropship-fault-reason-circuit",
        "cmu-dropship-fault-reason-iff",
        "cmu-dropship-fault-reason-servo",
        "cmu-dropship-fault-reason-coolant",
    ];

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
            boot.ReadyAt = _timing.CurTime + boot.Duration;
            Dirty(ship, boot);
            PlayAtConsoles(ship, boot.BootSound);
            _adminLog.Add(LogType.RMCDropshipWeapon,
                $"Fire control on {ToPrettyString(ship)} started its first boot from {ToPrettyString(console)}.");
        }
        Sync(ship, boot);
    }

    /// <summary>
    /// Re-runs the boot test after the dropship touches down, rolling landing wear faults.
    /// Ships that never completed their first boot are left alone.
    /// </summary>
    public void OnLanded(EntityUid ship, bool crashed)
    {
        if (_net.IsClient || !_config.GetCVar(CCVars.DropshipLandingReboot) ||
            !TryComp(ship, out DropshipComponent? dropship) ||
            !TryComp(ship, out DropshipFireControlBootComponent? boot) || !boot.Complete)
            return;
        boot.Complete = false;
        boot.LandingReboot = true;
        boot.CrashLanding = crashed;
        boot.ReadyAt = _timing.CurTime + boot.Duration;
        Dirty(ship, boot);
        PlayAtConsoles((ship, dropship), boot.BootSound);
        _adminLog.Add(LogType.RMCDropshipWeapon,
            $"Fire control on {ToPrettyString(ship)} started a post-landing reboot (crash: {crashed}).");
        Sync((ship, dropship), boot);
    }

    /// <summary>
    /// Records firing on a hardpoint. Continuous fire within <see cref="DropshipHardpointFaultComponent.UseWindow"/>
    /// counts as a single engagement.
    /// </summary>
    public void RegisterUse(EntityUid point)
    {
        if (_net.IsClient || TerminatingOrDeleted(point) ||
            !HasComp<DropshipWeaponPointComponent>(point) && !HasComp<DropshipUtilityPointComponent>(point))
            return;
        var fault = EnsureComp<DropshipHardpointFaultComponent>(point);
        var now = _timing.CurTime;
        if (fault.LastUse == TimeSpan.Zero || now - fault.LastUse >= fault.UseWindow)
            fault.Uses++;
        fault.LastUse = now;
    }

    public float GetLandingFaultChance(DropshipHardpointFaultComponent fault, bool crashed)
    {
        var chance = _config.GetCVar(CCVars.DropshipLandingFaultChance) +
                     _config.GetCVar(CCVars.DropshipLandingFaultStep) * Math.Max(0, fault.Landings - 1);
        chance *= 1f + _config.GetCVar(CCVars.DropshipFaultUseMultiplier) * fault.Uses;
        if (crashed)
            chance *= _config.GetCVar(CCVars.DropshipCrashFaultMultiplier);
        return Math.Clamp(chance, 0f, Math.Clamp(_config.GetCVar(CCVars.DropshipFaultMaxChance), 0f, 1f));
    }

    public static bool CanRepair(SkillsSystem skills, EntityUid user)
    {
        return skills.HasSkill(user, "RMCSkillPilot", 2) || skills.HasSkill(user, "RMCSkillEngineer", 1);
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
            if (boot.ReadyAt != TimeSpan.Zero && _timing.CurTime >= boot.ReadyAt)
                FinishBoot((uid, ship), boot);
            Sync((uid, ship), boot);
        }
    }

    private void FinishBoot(Entity<DropshipComponent> ship, DropshipFireControlBootComponent boot)
    {
        var bootChance = Math.Clamp(_config.GetCVar(CCVars.DropshipBootFaultChance), 0f, 1f);
        var newFaults = 0;
        foreach (var point in ship.Comp.AttachmentPoints)
        {
            string? slot = TryComp(point, out DropshipWeaponPointComponent? weaponPoint)
                ? weaponPoint.WeaponContainerSlotId
                : TryComp(point, out DropshipUtilityPointComponent? utilityPoint) ? utilityPoint.UtilitySlotId : null;
            if (slot == null || !_containers.TryGetContainer(point, slot, out var container) ||
                !container.ContainedEntities.Any(weapon => HasComp<DropshipWeaponComponent>(weapon)))
                continue;
            var fault = EnsureComp<DropshipHardpointFaultComponent>(point);
            if (fault.Faulted)
                continue;
            float chance;
            if (boot.LandingReboot)
            {
                fault.Landings++;
                chance = GetLandingFaultChance(fault, boot.CrashLanding);
            }
            else
                chance = bootChance;
            if (!_random.Prob(chance))
                continue;
            fault.Faulted = true;
            fault.Reason = _random.Pick(FaultReasons);
            Dirty(point, fault);
            newFaults++;
            _adminLog.Add(LogType.RMCDropshipWeapon,
                $"Hardpoint {ToPrettyString(point)} on {ToPrettyString(ship)} faulted during {(boot.LandingReboot ? "a post-landing" : "the first")} boot ({Loc.GetString(fault.Reason)}, chance {chance:P0}).");
        }
        boot.Complete = true;
        boot.LandingReboot = false;
        boot.CrashLanding = false;
        Dirty(ship, boot);
        PlayAtConsoles(ship, newFaults > 0 ? boot.FaultSound : boot.ReadySound);
        _adminLog.Add(LogType.RMCDropshipWeapon,
            $"Fire control on {ToPrettyString(ship)} completed boot with {newFaults} new hardpoint fault(s).");
    }

    private void PlayAtConsoles(Entity<DropshipComponent> ship, SoundSpecifier sound)
    {
        var query = EntityQueryEnumerator<DropshipTerminalWeaponsComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (_dropships.TryGetGridDropship(uid, out var consoleShip) && consoleShip.Owner == ship.Owner)
                _audio.PlayPvs(sound, uid);
        }
    }

    private string FaultReason(DropshipHardpointFaultComponent fault)
    {
        return Loc.GetString(fault.Reason ?? "cmu-dropship-fault-reason-unknown");
    }

    private void Sync(Entity<DropshipComponent> ship, DropshipFireControlBootComponent boot)
    {
        var faults = ship.Comp.AttachmentPoints.Where(point =>
            TryComp(point, out DropshipHardpointFaultComponent? fault) && fault.Faulted)
            .Select(point => Loc.GetString("cmu-dropship-boot-fault-entry",
                ("part", Name(point)), ("reason", FaultReason(Comp<DropshipHardpointFaultComponent>(point)))))
            .ToArray();
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
            if (boot.LandingReboot)
                status = Loc.GetString("cmu-dropship-boot-landing", ("stage", status));
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
            args.PushMarkup(Loc.GetString("cmu-dropship-hardpoint-fault", ("reason", FaultReason(point.Comp))));
    }

    private void OnReset(Entity<DropshipHardpointFaultComponent> point, ref InteractUsingEvent args)
    {
        if (args.Handled || !point.Comp.Faulted || !HasComp<MultitoolComponent>(args.Used))
            return;
        args.Handled = true;
        if (_net.IsClient)
            return;
        if (!CanRepair(_skills, args.User))
        {
            _popup.PopupEntity(Loc.GetString("cmu-dropship-hardpoint-reset-unskilled"), point, args.User);
            return;
        }
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
            !HasComp<MultitoolComponent>(tool) || !point.Comp.Faulted || !CanRepair(_skills, args.User))
            return;
        args.Handled = true;
        var reason = FaultReason(point.Comp);
        point.Comp.Faulted = false;
        point.Comp.Reason = null;
        point.Comp.Uses = 0;
        point.Comp.Landings = 0;
        Dirty(point);
        if (_dropships.TryGetGridDropship(point, out var ship) &&
            TryComp(ship, out DropshipFireControlBootComponent? boot))
            Sync(ship, boot);
        _audio.PlayPvs(point.Comp.RepairSound, point);
        _popup.PopupEntity(Loc.GetString("cmu-dropship-hardpoint-reset"), point, args.User);
        _adminLog.Add(LogType.RMCDropshipWeapon,
            $"{ToPrettyString(args.User)} cleared the {reason} fault on hardpoint {ToPrettyString(point)} with {ToPrettyString(tool)}.");
    }
}
