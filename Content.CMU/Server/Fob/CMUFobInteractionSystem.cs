using System.Numerics;
using Content.Shared._RMC14.Construction;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Marines.Squads;
using Content.Shared.ActionBlocker;
using Content.Shared.CMU14.Fob;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.GameTicking;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Physics;
using Content.Shared.Tools;
using Content.Shared.Tools.Components;
using Content.Shared.Tools.Systems;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Fob;

/// <summary>Physical access and repair authority for the optional FOB reserve equipment.</summary>
public sealed partial class CMUFobInteractionSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private ActionBlockerSystem _blocker = default!;
    [Dependency] private RMCConstructionSystem _construction = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private CMUFobDefenseSystem _defense = default!;
    [Dependency] private CMUFobEffectsSystem _effects = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private CMUFobMapSystem _maps = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private CMUFobReserveSystem _reserve = default!;
    [Dependency] private SkillsSystem _skills = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedToolSystem _tools = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private static readonly EntProtoId<SkillDefinitionComponent> Construction = "RMCSkillConstruction";
    private static readonly EntProtoId<SkillDefinitionComponent> Engineering = "RMCSkillEngineer";
    private static readonly ProtoId<ToolQualityPrototype> Screwdriver = "Screwing";
    private static readonly ProtoId<ToolQualityPrototype> Wrench = "Anchoring";
    private static readonly ProtoId<ToolQualityPrototype> Welding = "Welding";
    private readonly Dictionary<EntityUid, EntityUid> _controls = new();
    private TimeSpan _nextUiUpdate;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUFobRepairableComponent, InteractUsingEvent>(OnRepairInteract);
        SubscribeLocalEvent<CMUFobRepairableComponent, CMUFobRepairDoAfterEvent>(OnRepairFinished);
        SubscribeLocalEvent<CMUFobRepairableComponent, DoAfterAttemptEvent<CMUFobRepairDoAfterEvent>>(OnRepairAttempt);
        SubscribeLocalEvent<CMUFobRepairableComponent, DamageChangedEvent>(OnDeviceDamage);
        SubscribeLocalEvent<CMUFobRepairableComponent, ComponentShutdown>(OnRepairShutdown);
        SubscribeLocalEvent<CMUFobRepairableComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<CMUFobRelayComponent, CMUFobRelayChangedEvent>(OnRelayChanged);
        SubscribeLocalEvent<CMUFobControlComponent, ActivatableUIOpenAttemptEvent>(OnOpenAttempt);
        SubscribeLocalEvent<CMUFobControlComponent, BoundUIOpenedEvent>(OnOpened);
        SubscribeLocalEvent<CMUFobControlComponent, ComponentShutdown>(OnControlShutdown);
        SubscribeLocalEvent<CMUFobControlComponent, MoveEvent>(OnControlMoved);
        SubscribeLocalEvent<CMUFobControlComponent, AnchorStateChangedEvent>(OnControlAnchorChanged);
        SubscribeLocalEvent<CMUFobNetworkComponent, CMUFobReserveChangedEvent>(OnReserveChanged);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _controls.Clear());
        Subs.BuiEvents<CMUFobControlComponent>(CMUFobControlUiKey.Key, subs =>
            subs.Event<CMUFobControlMessage>(OnControlMessage));
    }

    private bool CanInteract(EntityUid target, EntityUid user, string faction)
    {
        return _reserve.Enabled && Exists(user) && !TerminatingOrDeleted(user) && !TerminatingOrDeleted(target)
            && !MetaData(user).EntityPaused && !MetaData(target).EntityPaused
            && Transform(user).MapUid == Transform(target).MapUid
            && TryComp<MarineComponent>(user, out var marine)
            && string.Equals(marine.Faction, faction, StringComparison.OrdinalIgnoreCase)
            && TryComp<MobStateComponent>(user, out var mob) && mob.CurrentState == MobState.Alive
            && _blocker.CanInteract(user, target) && _interaction.InRangeUnobstructed(user, target, range: 1.5f);
    }

    private bool IsOperator(EntityUid user)
    {
        return _skills.GetSkill(user, Construction) >= 3 || _skills.GetSkill(user, Engineering) >= 2
            || HasComp<SquadLeaderComponent>(user);
    }

    public bool CanOperate(EntityUid device, EntityUid user, string faction)
        => CanInteract(device, user, faction) && IsOperator(user);

    public bool HasOperationalControl(EntityUid network)
    {
        return _controls.TryGetValue(network, out var uid)
            && TryComp<CMUFobControlComponent>(uid, out var control) && !control.Broken
            && TryBindControl((uid, control), out var bound) && bound.Owner == network;
    }

    private bool HasTool(EntityUid user, EntityUid tool)
    {
        if (!_hands.TryGetActiveItem(user, out var held) || held != tool) return false;
        if (_tools.HasQuality(tool, Screwdriver)) return true;
        if (!_tools.HasQuality(tool, Welding)) return false;
        // Keep welders subject to their normal lit/fuel checks throughout the repair. Lit tools burn fuel normally.
        var attempt = new ToolUseAttemptEvent(user, 0);
        RaiseLocalEvent(tool, attempt);
        return !attempt.Cancelled;
    }

    private bool AtSite(EntityUid control, CMUFobControlComponent comp, Entity<CMUFobNetworkComponent> network)
    {
        return Transform(control).Anchored && Transform(control).MapUid == network.Comp.Map
            && Transform(network).MapUid == network.Comp.Map
            && Vector2.DistanceSquared(_transform.GetWorldPosition(control), _transform.GetWorldPosition(network))
                <= network.Comp.DeploymentRange * network.Comp.DeploymentRange
            && _maps.CanPlaceUtility(network, _transform.GetWorldPosition(control));
    }

    /// <summary>Reconnect a replacement at the original site; never creates a bank or replaces a live control.</summary>
    public bool TryBindControl(Entity<CMUFobControlComponent> control, out Entity<CMUFobNetworkComponent> network)
    {
        network = default;
        if (!_reserve.Enabled || TerminatingOrDeleted(control) || MetaData(control).EntityPaused
            || !_reserve.TryGetNetwork(control.Comp.Faction, out network)
            || network.Comp.State == CMUFobReserveState.Closed || !AtSite(control, control.Comp, network))
            return false;

        if (_controls.TryGetValue(network, out var other) && other != control.Owner
            && HasComp<CMUFobControlComponent>(other) && !TerminatingOrDeleted(other))
            return false;

        _controls[network] = control;
        control.Comp.Network = network;
        EnsureComp<CMUFobInstructionsComponent>(control);
        return true;
    }

    private void OnOpenAttempt(Entity<CMUFobControlComponent> control, ref ActivatableUIOpenAttemptEvent args)
    {
        if (TryBindControl(control, out _) && !control.Comp.Broken
            && CanInteract(control, args.User, control.Comp.Faction) && IsOperator(args.User))
            return;

        args.Cancel();
        _popup.PopupEntity(Loc.GetString("cmu-fob-control-denied"), control, args.User);
    }

    private void OnOpened(Entity<CMUFobControlComponent> control, ref BoundUIOpenedEvent args) => UpdateControl(control);

    private void OnControlMessage(Entity<CMUFobControlComponent> control, ref CMUFobControlMessage args)
    {
        // Revalidate every message: an already-open UI is not continuing permission to operate the device.
        if (control.Comp.Broken || !CanInteract(control, args.Actor, control.Comp.Faction) || !IsOperator(args.Actor)
            || !TryBindControl(control, out var network))
        {
            _ui.CloseUi(control.Owner, CMUFobControlUiKey.Key, args.Actor);
            return;
        }

        var changed = args.Activate ? _reserve.TryActivate(network) : _reserve.TryDeactivate(network);
        if (!changed)
            _popup.PopupEntity(Loc.GetString("cmu-fob-control-wait"), control, args.Actor);
        UpdateControl(control);
    }

    private void OnRepairInteract(Entity<CMUFobRepairableComponent> device, ref InteractUsingEvent args)
    {
        if (args.Handled || !_reserve.Enabled)
            return;

        args.Handled = true;
        if (_tools.HasQuality(args.Used, Wrench) && TryComp<CMUFobControlComponent>(device, out var control))
        {
            if (control.Broken || !_reserve.TryGetNetwork(control.Faction, out var bank)
                || bank.Comp.State is CMUFobReserveState.Active or CMUFobReserveState.Starting or CMUFobReserveState.Closed
                || !CanOperate(device, args.User, control.Faction)
                || Transform(device).MapUid != bank.Comp.Map
                || Vector2.DistanceSquared(_transform.GetWorldPosition(device), _transform.GetWorldPosition(bank)) > bank.Comp.DeploymentRange * bank.Comp.DeploymentRange) return;
            if (Transform(device).Anchored) _transform.Unanchor(device);
            else
            {
                if (Transform(device).GridUid is not { } grid || !TryComp<MapGridComponent>(grid, out var mapGrid)) return;
                var tile = _map.GetTileRef(grid, mapGrid, Transform(device).Coordinates);
                var center = _map.GridTileToWorld(grid, mapGrid, tile.GridIndices).Position;
                if (tile.Tile.IsEmpty || Vector2.DistanceSquared(center, _transform.GetWorldPosition(bank)) > bank.Comp.DeploymentRange * bank.Comp.DeploymentRange
                    || !_maps.CanPlaceUtility(bank, center)
                    || !_construction.CanBuildAt(Transform(device).Coordinates, Name(device), out _, anchoring: true,
                        collision: CollisionGroup.Impassable | CollisionGroup.MidImpassable)
                    || !_transform.AnchorEntity(device)) return;
            }
            _appearance.SetData(device, CMUFobVisuals.Stowed, !Transform(device).Anchored);
            _effects.Play(device, Transform(device).Anchored ? CMUFobPresentationCue.Deploy : CMUFobPresentationCue.Pack);
            return;
        }
        TryStartRepair(device, args.User, args.Used);
    }

    /// <summary>Starts interruptible work against the current relay job or broken control.</summary>
    public bool TryStartRepair(Entity<CMUFobRepairableComponent> device, EntityUid user, EntityUid tool)
    {
        var controlRepair = TryComp<CMUFobControlComponent>(device, out var control);
        CMUFobRelayJob job;
        if (controlRepair)
        {
            if (!control!.Broken || !TryBindControl((device, control), out var network) || !IsOperator(user))
                return false;
            job = new CMUFobRelayJob(network, 0);
        }
        else if (!_reserve.TryGetRelayJob(device, out job, out _))
        {
            _popup.PopupEntity(Loc.GetString("cmu-fob-relay-unavailable"), device, user);
            return false;
        }

        var faction = Comp<CMUFobNetworkComponent>(job.Network).Faction;
        if (!CanInteract(device, user, faction) || !HasTool(user, tool))
        {
            _popup.PopupEntity(Loc.GetString("cmu-fob-repair-denied"), device, user);
            return false;
        }

        if (device.Comp.User != null && _doAfter.IsRunning(device.Comp.DoAfter))
        {
            _popup.PopupEntity(Loc.GetString("cmu-fob-repair-busy"), device, user);
            return false;
        }

        var attempt = ++device.Comp.Attempt;
        var ev = new CMUFobRepairDoAfterEvent
        {
            Network = GetNetEntity(job.Network), Generation = job.Generation, Attempt = attempt, Control = controlRepair,
        };
        var duration = _skills.GetSkill(user, Construction) >= 3
            ? device.Comp.TrainedDuration : device.Comp.UntrainedDuration;
        var work = new DoAfterArgs(EntityManager, user, duration, ev, device, device, tool)
        {
            NeedHand = true,
            BreakOnHandChange = true,
            BreakOnMove = true,
            BreakOnDamage = true,
            DamageThreshold = FixedPoint2.Zero,
            AttemptFrequency = AttemptFrequency.EveryTick,
            CancelDuplicate = false,
        };

        // Register before starting: instant-admin DoAfters can deliver completion synchronously.
        device.Comp.User = user;
        device.Comp.Tool = tool;
        device.Comp.DoAfter = null;
        if (!_doAfter.TryStartDoAfter(work, out var id))
        {
            ClearRepair(device);
            return false;
        }

        if (device.Comp.User == user && device.Comp.Attempt == attempt)
        {
            device.Comp.DoAfter = id;
            _effects.SetStatus(device, CMUFobPresentationStatus.Working);
        }
        return true;
    }

    private bool ValidateRepair(Entity<CMUFobRepairableComponent> device, CMUFobRepairDoAfterEvent ev,
        Content.Shared.DoAfter.DoAfter work)
    {
        var user = work.Args.User;
        if (device.Comp.Attempt != ev.Attempt || device.Comp.User != user || device.Comp.Tool != work.Args.Used
            || work.Args.Used is not { } tool || !HasTool(user, tool)
            || !TryGetEntity(ev.Network, out var uid) || !TryComp<CMUFobNetworkComponent>(uid, out var network)
            || !CanInteract(device, user, network.Faction))
            return false;

        if (ev.Control)
            return TryComp<CMUFobControlComponent>(device, out var control) && control.Broken
                && IsOperator(user) && TryBindControl((device, control), out var bound) && bound.Owner == uid;

        return _reserve.TryGetRelayJob(device, out var job, out _) && job.Network == uid
            && job.Generation == ev.Generation;
    }

    private void OnRepairAttempt(Entity<CMUFobRepairableComponent> device,
        ref DoAfterAttemptEvent<CMUFobRepairDoAfterEvent> args)
    {
        if (!ValidateRepair(device, args.Event, args.DoAfter))
            args.Cancel();
    }

    private void OnRepairFinished(Entity<CMUFobRepairableComponent> device, ref CMUFobRepairDoAfterEvent args)
    {
        if (args.Handled || device.Comp.Attempt != args.Attempt || device.Comp.User != args.User)
            return;

        var valid = !args.Cancelled && ValidateRepair(device, args, args.DoAfter);
        ClearRepair(device);
        args.Handled = true;
        if (!valid)
            return;

        if (args.Control)
        {
            var control = Comp<CMUFobControlComponent>(device);
            control.Broken = false;
            _appearance.SetData(device, CMUFobVisuals.Broken, false);
            _damage.SetAllDamage(device.Owner, FixedPoint2.Zero);
            _popup.PopupEntity(Loc.GetString("cmu-fob-control-repaired"), device, args.User);
            _effects.Play(device, CMUFobPresentationCue.Repair);
            UpdateControl((device, control));
        }
        else if (_reserve.TryCompleteRelay(device,
                     new CMUFobRelayJob(GetEntity(args.Network), args.Generation), out var awarded))
        {
            _damage.SetAllDamage(device.Owner, FixedPoint2.Zero);
            _effects.Play(device, CMUFobPresentationCue.Repair);
            _popup.PopupEntity(Loc.GetString("cmu-fob-relay-repaired", ("seconds", (int) awarded.TotalSeconds)),
                device, args.User);
        }
    }

    private void ClearRepair(Entity<CMUFobRepairableComponent> device)
    {
        var comp = device.Comp;
        comp.User = null;
        comp.Tool = null;
        comp.DoAfter = null;
        _effects.SetStatus(device, TryComp<CMUFobControlComponent>(device, out var control) && control.Broken
            ? CMUFobPresentationStatus.Broken
            : TryComp<CMUFobRelayComponent>(device, out var relay) && relay.Network != null
                ? CMUFobPresentationStatus.Ready : CMUFobPresentationStatus.Off);
    }

    private void CancelRepair(Entity<CMUFobRepairableComponent> device)
    {
        var id = device.Comp.DoAfter;
        ClearRepair(device);
        _doAfter.Cancel(id);
    }

    private void OnRepairShutdown(Entity<CMUFobRepairableComponent> device, ref ComponentShutdown args)
        => CancelRepair(device);

    private void OnDeviceDamage(Entity<CMUFobRepairableComponent> device, ref DamageChangedEvent args)
    {
        if (!_reserve.Enabled)
            return;

        if (args.DamageIncreased)
            CancelRepair(device);

        if (!TryComp<CMUFobControlComponent>(device, out var control) || control.Broken
            || _damage.GetTotalDamage((device, args.Damageable)) < control.BreakDamage)
            return;

        control.Broken = true;
        _effects.SetStatus(device, CMUFobPresentationStatus.Broken);
        _effects.Play(device, CMUFobPresentationCue.Break);
        _appearance.SetData(device, CMUFobVisuals.Broken, true);
        if (TryBindControl((device, control), out var network))
            _reserve.DisableControl(network);
        _ui.CloseUi(device.Owner, CMUFobControlUiKey.Key);
    }

    private void OnRelayChanged(Entity<CMUFobRelayComponent> relay, ref CMUFobRelayChangedEvent args)
    {
        if (TryComp<CMUFobRepairableComponent>(relay, out var repair))
            CancelRepair((relay, repair));
        _appearance.SetData(relay, CMUFobVisuals.Available, relay.Comp.Network != null);
        _effects.SetStatus(relay, relay.Comp.Network != null ? CMUFobPresentationStatus.Ready : CMUFobPresentationStatus.Off);
    }

    private void OnControlShutdown(Entity<CMUFobControlComponent> control, ref ComponentShutdown args)
    {
        if (control.Comp.Network is not { } network || !_controls.TryGetValue(network, out var bound)
            || bound != control.Owner)
            return;
        _controls.Remove(network);
        if (TryComp<CMUFobNetworkComponent>(network, out var reserve))
            _reserve.DisableControl((network, reserve));
    }

    private void OnControlMoved(Entity<CMUFobControlComponent> control, ref MoveEvent args)
        => CheckControlSite(control);

    private void OnControlAnchorChanged(Entity<CMUFobControlComponent> control, ref AnchorStateChangedEvent args)
        => CheckControlSite(control);

    private void CheckControlSite(Entity<CMUFobControlComponent> control)
    {
        if (control.Comp.Network is { } uid && TryComp<CMUFobNetworkComponent>(uid, out var network)
            && !AtSite(control, control.Comp, (uid, network)))
        {
            _reserve.DisableControl((uid, network));
            _ui.CloseUi(control.Owner, CMUFobControlUiKey.Key);
        }
    }

    private void OnReserveChanged(Entity<CMUFobNetworkComponent> network, ref CMUFobReserveChangedEvent args)
    {
        if (_controls.TryGetValue(network, out var uid) && TryComp<CMUFobControlComponent>(uid, out var control))
            UpdateControl((uid, control));
    }

    private void OnExamined(Entity<CMUFobRepairableComponent> device, ref ExaminedEvent args)
    {
        if (!_reserve.Enabled)
        {
            args.PushMarkup(Loc.GetString("cmu-fob-disabled"));
            return;
        }

        if (TryComp<CMUFobControlComponent>(device, out var control))
        {
            args.PushMarkup(Loc.GetString(control.Broken ? "cmu-fob-control-broken" : "cmu-fob-control-examine"));
            if (TryBindControl((device, control), out var network))
                args.PushMarkup(_reserve.GetPublicState(network));
        }
        else
            args.PushMarkup(Loc.GetString(_reserve.TryGetRelayAssignment(device, out _)
                ? "cmu-fob-relay-examine-active" : "cmu-fob-relay-examine-inactive"));
    }

    private static int Seconds(TimeSpan time) => Math.Max(0, (int) Math.Ceiling(time.TotalSeconds));

    private void UpdateControl(Entity<CMUFobControlComponent> control)
    {
        _appearance.SetData(control, CMUFobVisuals.Stowed, !Transform(control).Anchored);
        CheckControlSite(control);
        if (!TryBindControl(control, out var network) || control.Comp.Broken)
        {
            _effects.SetStatus(control, TryComp<CMUFobRepairableComponent>(control, out var repair) && _doAfter.IsRunning(repair.DoAfter)
                ? CMUFobPresentationStatus.Working
                : control.Comp.Broken ? CMUFobPresentationStatus.Broken : CMUFobPresentationStatus.Off);
            _appearance.SetData(control, CMUFobVisuals.Available, false);
            _ui.CloseUi(control.Owner, CMUFobControlUiKey.Key);
            return;
        }

        var comp = network.Comp;
        _effects.SetStatus(control, comp.State switch
        {
            CMUFobReserveState.Starting => CMUFobPresentationStatus.Starting,
            CMUFobReserveState.Active => CMUFobPresentationStatus.Active,
            CMUFobReserveState.Empty => CMUFobPresentationStatus.Empty,
            _ => CMUFobPresentationStatus.Off,
        });
        _appearance.SetData(control, CMUFobVisuals.Available,
            comp.State is CMUFobReserveState.Starting or CMUFobReserveState.Active);
        if (!_ui.IsUiOpen(control.Owner, CMUFobControlUiKey.Key))
            return;
        var remaining = _reserve.GetRemaining(network);
        var now = _timing.CurTime;
        var noRelayPair = _maps.TryGetRelayFailure(network, out var retryAt);
        _ui.SetUiState(control.Owner, CMUFobControlUiKey.Key, new CMUFobControlState(comp.State,
            Seconds(remaining), Seconds(comp.RestartAvailableAt - now),
            Seconds((comp.MinimumActiveUntil ?? now) - now),
            Seconds((comp.StartupEndsAt ?? now) - now),
            Seconds((noRelayPair ? retryAt : comp.JobExpiresAt ?? comp.NextJobAt) - now),
            Seconds(TimeSpan.FromTicks(Math.Min(comp.RelayReward.Ticks, (comp.Capacity - remaining).Ticks))),
            _reserve.GetRelayLocation(comp.FirstRelay), _reserve.GetRelayLocation(comp.SecondRelay), _defense.GetGunStatus(network),
            comp.State == CMUFobReserveState.Standby && remaining > TimeSpan.Zero && now >= comp.RestartAvailableAt,
            comp.DepletesAt != null && now >= comp.MinimumActiveUntil, noRelayPair));
    }

    public override void Update(float frameTime)
    {
        if (!_reserve.Enabled || _timing.CurTime < _nextUiUpdate)
            return;
        _nextUiUpdate = _timing.CurTime + TimeSpan.FromSeconds(1);
        var query = EntityQueryEnumerator<CMUFobControlComponent>();
        while (query.MoveNext(out var uid, out var control))
            UpdateControl((uid, control));
    }
}
