using System.Linq;
using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.Dropship.AttachmentPoint;
using Content.Shared._RMC14.Dropship.Utility.Components;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared._RMC14.Evacuation;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Marines.Announce;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Rules;
using Content.Shared._RMC14.Thunderdome;
using Content.Shared._RMC14.Tracker;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Maturing;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Administration.Logs;
using Content.Shared.AU14;
using Content.Shared.AU14.Round;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.GameTicking;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Content.Shared.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Dropship;

public abstract class SharedDropshipSystem : EntitySystem
{
    [Dependency] private readonly ISharedAdminLogManager _adminLog = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedGameTicker _gameTicker = default!;
    [Dependency] private readonly SharedMarineAnnounceSystem _marineAnnounce = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SkillsSystem _skills = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;

    private TimeSpan _dropshipInitialDelay;
    private TimeSpan _hijackInitialDelay;

    public override void Initialize()
    {
        SubscribeLocalEvent<DropshipComponent, MapInitEvent>(OnDropshipMapInit);

        SubscribeLocalEvent<DropshipNavigationComputerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<DropshipNavigationComputerComponent, ActivatableUIOpenAttemptEvent>(OnUIOpenAttempt);
        SubscribeLocalEvent<DropshipNavigationComputerComponent, AfterActivatableUIOpenEvent>(OnNavigationOpen);
        SubscribeLocalEvent<DropshipNavigationComputerComponent, DropshipLockoutOverrideDoAfterEvent>(OnNavigationLockoutOverride);
        SubscribeLocalEvent<DropshipNavigationComputerComponent, DropshipHumanHijackDoAfterEvent>(OnHumanHijackDoAfter);

        SubscribeLocalEvent<DropshipTerminalComponent, ActivateInWorldEvent>(OnDropshipTerminalActivateInWorld, before: [typeof(ActivatableUISystem), typeof(ActivatableUIRequiresAccessSystem)]);
        SubscribeLocalEvent<DropshipTerminalComponent, ActivatableUIOpenAttemptEvent>(OnTerminalOpenAttempt);
        SubscribeLocalEvent<DropshipTerminalComponent, AfterActivatableUIOpenEvent>(OnTerminalOpen);

        SubscribeLocalEvent<DropshipWeaponPointComponent, MapInitEvent>(OnAttachmentPointMapInit);
        SubscribeLocalEvent<DropshipWeaponPointComponent, EntityTerminatingEvent>(OnAttachmentPointRemove);
        SubscribeLocalEvent<DropshipWeaponPointComponent, ExaminedEvent>(OnAttachmentExamined);
        SubscribeLocalEvent<DropshipWeaponPointComponent, InteractHandEvent>(OnInteract);

        SubscribeLocalEvent<DropshipUtilityPointComponent, MapInitEvent>(OnAttachmentPointMapInit);
        SubscribeLocalEvent<DropshipUtilityPointComponent, EntityTerminatingEvent>(OnAttachmentPointRemove);

        SubscribeLocalEvent<DropshipEnginePointComponent, MapInitEvent>(OnAttachmentPointMapInit);
        SubscribeLocalEvent<DropshipEnginePointComponent, EntityTerminatingEvent>(OnAttachmentPointRemove);
        SubscribeLocalEvent<DropshipEnginePointComponent, ExaminedEvent>(OnEngineExamined);

        SubscribeLocalEvent<DropshipElectronicSystemPointComponent, MapInitEvent>(OnAttachmentPointMapInit);
        SubscribeLocalEvent<DropshipElectronicSystemPointComponent, EntityTerminatingEvent>(OnAttachmentPointRemove);
        SubscribeLocalEvent<DropshipElectronicSystemPointComponent, ExaminedEvent>(OnElectronicSystemExamined);
        SubscribeLocalEvent<DropshipElectronicSystemPointComponent, InteractHandEvent>(OnInteract);

        Subs.BuiEvents<DropshipNavigationComputerComponent>(DropshipNavigationUiKey.Key,
            subs =>
            {
                subs.Event<DropshipNavigationLaunchMsg>(OnDropshipNavigationLaunchMsg);
                subs.Event<DropshipNavigationCancelMsg>(OnDropshipNavigationCancelMsg);
            });

        Subs.BuiEvents<DropshipNavigationComputerComponent>(DropshipHijackerUiKey.Key,
            subs =>
            {
                subs.Event<DropshipHijackerDestinationChosenBuiMsg>(OnHijackerDestinationChosenMsg);
            });

        Subs.BuiEvents<DropshipTerminalComponent>(DropshipTerminalUiKey.Key,
            subs =>
            {
                subs.Event<DropshipTerminalSummonDropshipMsg>(OnTerminalSummon);
            });

        Subs.CVar(_config, RMCCVars.RMCDropshipInitialDelayMinutes, v => _dropshipInitialDelay = TimeSpan.FromMinutes(v), true);
        Subs.CVar(_config, RMCCVars.RMCDropshipHijackInitialDelayMinutes, v => _hijackInitialDelay = TimeSpan.FromMinutes(v), true);
    }

    private void OnDropshipMapInit(Entity<DropshipComponent> ent, ref MapInitEvent args)
    {
        var children = Transform(ent).ChildEnumerator;
        while (children.MoveNext(out var uid))
        {
            if (TerminatingOrDeleted(uid))
                continue;

            if (HasComp<DropshipWeaponPointComponent>(uid) ||
                HasComp<DropshipEnginePointComponent>(uid) ||
                HasComp<DropshipUtilityPointComponent>(uid) ||
                HasComp<DropshipElectronicSystemPointComponent>(uid))
            {
                ent.Comp.AttachmentPoints.Add(uid);
            }
        }

        var ev = new DropshipMapInitEvent();
        RaiseLocalEvent(ent, ref ev);
    }

    private void OnMapInit(Entity<DropshipNavigationComputerComponent> ent, ref MapInitEvent args)
    {
        if (Transform(ent).ParentUid is { Valid: true } parent &&
            IsShuttle(parent))
        {
            EnsureComp<DropshipComponent>(parent);
        }
    }

    private void OnUIOpenAttempt(Entity<DropshipNavigationComputerComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        var isXeno = HasComp<XenoComponent>(args.User);
        var isHijacker = TryComp<DropshipHijackerComponent>(args.User, out var hijackerComp);

        // Xenos without hijacker comp can't use the console at all
        if (isXeno && !isHijacker)
        {
            args.Cancel();
            return;
        }

        var xform = Transform(ent);
        if (TryComp(xform.ParentUid, out DropshipComponent? dropship) &&
            dropship.Crashed)
        {
            args.Cancel();
            return;
        }

        // Block hijacking third-party dropships entirely
        if (isHijacker &&
            TryComp<WhitelistedShuttleComponent>(ent.Owner, out var wsComp) &&
            string.Equals(wsComp.Faction, "thirdparty", StringComparison.OrdinalIgnoreCase))
        {
            args.Cancel();
            _popup.PopupClient(Loc.GetString("rmc-dropship-hijack-thirdparty"), ent, args.User, PopupType.MediumCaution);
            return;
        }

        if (!TryDropshipLaunchPopup(ent, args.User, true))
        {
            args.Cancel();
            return;
        }

        var lockedOutRemaining = ent.Comp.LockedOutUntil - _timing.CurTime;
        if (lockedOutRemaining > TimeSpan.Zero && !isHijacker)
        {
            args.Cancel();
            _popup.PopupClient(Loc.GetString("rmc-dropship-locked-out", ("minutes", (int)lockedOutRemaining.TotalMinutes)), ent, args.User, PopupType.MediumCaution);

            if (_skills.HasSkill(args.User, ent.Comp.Skill, ent.Comp.FlyBySkillLevel))
            {
                var ev = new DropshipLockoutOverrideDoAfterEvent();
                var doAfter = new DoAfterArgs(EntityManager, args.User, TimeSpan.FromSeconds(20), ev, ent, ent)
                {
                    BreakOnMove = true,
                    BreakOnDamage = true,
                    BreakOnRest = true,
                    DuplicateCondition = DuplicateConditions.SameEvent,
                    CancelDuplicate = true
                };
                _doAfter.TryStartDoAfter(doAfter);
            }
            return;
        }

        // Xeno hijacker (queen) lockout behavior - short 3s do-after
        if (lockedOutRemaining <= TimeSpan.Zero && isHijacker && !hijackerComp!.IsHumanHijacker)
        {
            args.Cancel();

            var ev = new DropshipLockoutDoAfterEvent();
            var doAfter = new DoAfterArgs(EntityManager, args.User, TimeSpan.FromSeconds(3), ev, ent, ent)
            {
                BreakOnMove = true,
                BreakOnDamage = true,
                BreakOnRest = true,
                DuplicateCondition = DuplicateConditions.SameEvent,
                CancelDuplicate = true
            };
            _doAfter.TryStartDoAfter(doAfter);
            return;
        }

        // Hijacker (xeno or human) from here on.
        if (!isHijacker)
            return;

        args.Cancel();

        if (!TryDropshipHijackPopup(ent, args.User, false))
            return;

        // Human hijacker: 60s do-after before opening the hijack destination menu
        if (hijackerComp!.IsHumanHijacker)
        {
            _popup.PopupEntity(Loc.GetString("rmc-dropship-hijack-human-hacking"), ent, args.User, PopupType.LargeCaution);

            var ev = new DropshipHumanHijackDoAfterEvent();
            var doAfter = new DoAfterArgs(EntityManager, args.User, TimeSpan.FromSeconds(hijackerComp.HijackDoAfterSeconds), ev, ent, ent)
            {
                BreakOnMove = true,
                BreakOnDamage = true,
                BreakOnRest = true,
                DuplicateCondition = DuplicateConditions.SameEvent,
                CancelDuplicate = true
            };
            _doAfter.TryStartDoAfter(doAfter);
            return;
        }

        // Xeno hijacker: open destination menu immediately
        OpenHijackDestinationMenu(ent, args.User);
    }

    /// <summary>
    ///     Opens the hijack destination selection UI for a user.
    ///     For xeno hijackers: shows DropshipHijackDestination entities on the hijackee's ship only.
    ///     For human hijackers: shows enemy primary LZs only.
    /// </summary>
    private void OpenHijackDestinationMenu(EntityUid computer, EntityUid user)
    {
        var destinations = new List<(NetEntity Id, string Name)>();

        var isHumanHijacker = TryComp<DropshipHijackerComponent>(user, out var hijacker) && hijacker.IsHumanHijacker;

        if (isHumanHijacker)
        {
            // Resolve the hijacker's own faction
            string? userFaction = null;
            if (TryComp<MarineComponent>(user, out var marine) && !string.IsNullOrEmpty(marine.Faction))
                userFaction = marine.Faction.ToLowerInvariant();

            // Show only ENEMY primary LZs (primary LZs whose faction doesn't match the hijacker's)
            var primaryQuery = EntityQueryEnumerator<PrimaryLandingZoneComponent>();
            while (primaryQuery.MoveNext(out var uid, out var primary))
            {
                var lzFaction = string.IsNullOrEmpty(primary.Faction) ? null : primary.Faction.ToLowerInvariant();

                // Skip own faction's primary LZs
                if (lzFaction != null && string.Equals(lzFaction, userFaction, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Skip null-faction LZs if the hijacker has no faction (shouldn't happen, but safety)
                if (lzFaction == null && userFaction == null)
                    continue;

                destinations.Add((GetNetEntity(uid), Name(uid)));
            }

            if (destinations.Count == 0)
            {
                _popup.PopupEntity(Loc.GetString("rmc-dropship-hijack-no-enemy-lz"), computer, user, PopupType.LargeCaution);
                return;
            }
        }
        else
        {
            // Xeno/threat hijacker: show crash landing destinations on ANY ship
            var shipMaps = GetAllShipMaps();
            var query = EntityQueryEnumerator<DropshipHijackDestinationComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out _, out var xform))
            {
                if (xform.MapUid is { } mapUid && shipMaps.Contains(mapUid))
                    destinations.Add((GetNetEntity(uid), Name(uid)));
            }
        }

        _ui.OpenUi(computer, DropshipHijackerUiKey.Key, user);
        _ui.SetUiState(computer, DropshipHijackerUiKey.Key, new DropshipHijackerBuiState(destinations));
    }

    /// <summary>
    ///     Gets the map UIDs of ALL ships in the game.
    ///     Used to filter xeno/threat hijack destinations to any ship.
    ///     Includes AlmayerComponent maps (default marine) and all ShipFactionComponent maps.
    /// </summary>
    private HashSet<EntityUid> GetAllShipMaps()
    {
        var shipMaps = new HashSet<EntityUid>();

        var almayerQuery = EntityQueryEnumerator<AlmayerComponent, TransformComponent>();
        while (almayerQuery.MoveNext(out _, out _, out var xform))
        {
            if (xform.MapUid is { } mapUid)
                shipMaps.Add(mapUid);
        }

        var shipQuery = EntityQueryEnumerator<ShipFactionComponent, TransformComponent>();
        while (shipQuery.MoveNext(out _, out _, out var xform2))
        {
            if (xform2.MapUid is { } mapUid)
                shipMaps.Add(mapUid);
        }

        return shipMaps;
    }

    private void OnNavigationOpen(Entity<DropshipNavigationComputerComponent> ent, ref AfterActivatableUIOpenEvent args)
    {
        RefreshUI(ent);
        AfterNavigationOpen(ent, ref args);
    }

    protected virtual void AfterNavigationOpen(Entity<DropshipNavigationComputerComponent> ent, ref AfterActivatableUIOpenEvent args)
    {
    }

    private void OnNavigationLockoutOverride(Entity<DropshipNavigationComputerComponent> ent, ref DropshipLockoutOverrideDoAfterEvent args)
    {
        var lockedOutRemaining = ent.Comp.LockedOutUntil - _timing.CurTime;
        var reduction = lockedOutRemaining / 10 + TimeSpan.FromSeconds(20);
        ent.Comp.LockedOutUntil -= reduction;
        Dirty(ent);

        if (ent.Comp.LockedOutUntil < _timing.CurTime)
        {
            _ui.CloseUis(ent.Owner);
            _popup.PopupClient(Loc.GetString("rmc-dropship-locked-out-bypass-complete"), ent, args.User, PopupType.Medium);
            return;
        }

        _popup.PopupClient(Loc.GetString("rmc-dropship-locked-out-bypass"), ent, args.User, PopupType.Medium);
    }

    private void OnHumanHijackDoAfter(Entity<DropshipNavigationComputerComponent> ent, ref DropshipHumanHijackDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        if (!TryComp<DropshipHijackerComponent>(args.User, out var hijacker) || !hijacker.IsHumanHijacker)
            return;

        // Check and spend intel points
        if (!TrySpendHijackIntel(args.User, hijacker.IntelCost))
        {
            _popup.PopupEntity(Loc.GetString("rmc-dropship-hijack-no-intel"), ent, args.User, PopupType.LargeCaution);
            return;
        }

        // Open the hijack destination menu
        OpenHijackDestinationMenu(ent, args.User);
    }

    /// <summary>
    ///     Attempts to spend intel points for a human hijacker. Override on server side.
    /// </summary>
    protected virtual bool TrySpendHijackIntel(EntityUid user, double cost)
    {
        // Only server can spend intel points, default to false
        return false;
    }

    private void OnDropshipTerminalActivateInWorld(Entity<DropshipTerminalComponent> ent, ref ActivateInWorldEvent args)
    {
        var user = args.User;
        var isXeno = HasComp<XenoComponent>(user);
        var isHijacker = HasComp<DropshipHijackerComponent>(user);
        var isHumanHijacker = TryComp<DropshipHijackerComponent>(user, out var hijackerComp) && hijackerComp.IsHumanHijacker;

        // Non-xeno non-hijacker: fall through to normal UI
        if (!isXeno && !isHijacker)
            return;

        args.Handled = true;
        if (_net.IsClient)
            return;

        if (!isHijacker)
        {
            _popup.PopupEntity($"You stare cluelessly at the {Name(ent.Owner)}", user, user);
            return;
        }

        if (!TryDropshipLaunchPopup(ent, user, false))
            return;

        if (!TryDropshipHijackPopup(ent, user, false))
            return;

        var userTransform = Transform(user);
        var closestDestination = FindClosestLZ(userTransform);
        if (closestDestination == null)
        {
            _popup.PopupEntity("There are no dropship destinations near you!", user, user, PopupType.MediumCaution);
            return;
        }

        if (closestDestination.Value.Comp1.Ship != null)
        {
            _popup.PopupEntity("There's already a dropship coming here!", user, user, PopupType.MediumCaution);
            return;
        }

        // Use the planetside terminal's faction to determine which faction's dropship to call.
        // The terminal belongs to the hijackees, so we call a dropship of that same faction.
        string? terminalFaction = string.IsNullOrWhiteSpace(ent.Comp.Faction)
            ? null
            : ent.Comp.Faction.ToLowerInvariant();

        // If a global primary exists OR a primary exists for the terminal's faction, and the closest destination isn't primary for that same faction, block.
        if ((PrimaryExistsForFaction(null) || PrimaryExistsForFaction(terminalFaction)) &&
            !IsPrimaryForFaction(closestDestination.Value.Owner, terminalFaction))
        {
            _popup.PopupEntity("The shuttle isn't responding to prompts, it looks like this isn't the primary shuttle.", user, user, PopupType.MediumCaution);
            return;
        }

        var dropships = EntityQueryEnumerator<DropshipComponent, TransformComponent>();
        while (dropships.MoveNext(out var uid, out var dropship, out var xform))
        {
            if (dropship.Crashed || IsInFTL(uid))
                continue;

            if (HasComp<ThunderdomeMapComponent>(xform.MapUid))
                continue;

            var computerQuery = EntityQueryEnumerator<DropshipNavigationComputerComponent>();
            while (computerQuery.MoveNext(out var computerId, out var computer))
            {
                if (!computer.Hijackable)
                    continue;

                if (Transform(computerId).GridUid != uid)
                    continue;

                // Resolve shuttle faction once for multiple checks
                string? shuttleFaction = null;
                if (TryComp<WhitelistedShuttleComponent>(computerId, out var wsComp) &&
                    !string.IsNullOrEmpty(wsComp.Faction))
                {
                    shuttleFaction = wsComp.Faction.ToLowerInvariant();
                }

                // Never hijack third-party dropships
                if (string.Equals(shuttleFaction, "thirdparty", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Only call dropships belonging to the terminal's faction (the hijackees' faction)
                if (terminalFaction != null &&
                    !string.Equals(shuttleFaction, terminalFaction, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (FlyTo((computerId, computer), closestDestination.Value, user))
                {
                    _popup.PopupEntity("You call down one of the dropships to your location", user, user, PopupType.LargeCaution);
                    return;
                }
            }
        }

        _popup.PopupEntity("There are no available dropships! Wait a moment.", user, user, PopupType.LargeCaution);
    }

    private void OnTerminalOpenAttempt(Entity<DropshipTerminalComponent> terminal, ref ActivatableUIOpenAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        // Block xenos and human hijackers from the normal terminal UI
        // (they use the hijack flow via ActivateInWorld instead)
        if (HasComp<XenoComponent>(args.User) || HasComp<DropshipHijackerComponent>(args.User))
        {
            args.Cancel();
            return;
        }

        // Faction whitelisting: if the terminal has a faction set, only users of that faction can use it
        if (!string.IsNullOrEmpty(terminal.Comp.Faction))
        {
            string? userFaction = null;
            if (TryComp<MarineComponent>(args.User, out var marine) && !string.IsNullOrEmpty(marine.Faction))
                userFaction = marine.Faction.ToLowerInvariant();

            if (!string.Equals(terminal.Comp.Faction, userFaction, StringComparison.OrdinalIgnoreCase))
            {
                _popup.PopupClient(Loc.GetString("rmc-dropship-terminal-wrong-faction"), terminal, args.User, PopupType.MediumCaution);
                args.Cancel();
                return;
            }
        }
    }

    private void OnTerminalOpen(Entity<DropshipTerminalComponent> terminal, ref AfterActivatableUIOpenEvent args)
    {
        if (!_ui.IsUiOpen(terminal.Owner, DropshipTerminalUiKey.Key, args.Actor))
            return;

        var closestLZ = FindClosestLZ(terminal);
        if (closestLZ is not { } lz)
        {
            var failedState = new DropshipTerminalBuiState("???", []);
            _ui.SetUiState(terminal.Owner, DropshipTerminalUiKey.Key, failedState);
            return;
        }

        // Determine the terminal's faction for filtering dropships
        var terminalFaction = terminal.Comp.Faction;

        var dropships = new List<DropshipEntry>();
        var dropshipQuery = EntityQueryEnumerator<DropshipComponent>();
        while (dropshipQuery.MoveNext(out var uid, out var _))
        {
            var computerQuery = EntityQueryEnumerator<DropshipNavigationComputerComponent>();
            while (computerQuery.MoveNext(out var computerId, out var computer))
            {
                // ERT-Ships can't be hijacked, so we can use this to filter them out.
                if (!computer.Hijackable)
                    continue;

                // On a different grid => not the associated computer.
                if (Transform(computerId).GridUid != uid)
                    continue;

                // Faction filter: if the terminal has a faction, only show dropships
                // whose WhitelistedShuttleComponent faction matches (or dropships with no faction)
                if (!string.IsNullOrEmpty(terminalFaction))
                {
                    if (TryComp<WhitelistedShuttleComponent>(computerId, out var shuttle) &&
                        !string.IsNullOrEmpty(shuttle.Faction) &&
                        !string.Equals(shuttle.Faction, terminalFaction, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                }

                dropships.Add(new DropshipEntry(GetNetEntity(computerId), Name(uid)));
            }
        }

        var state = new DropshipTerminalBuiState(Name(lz), dropships);
        _ui.SetUiState(terminal.Owner, DropshipTerminalUiKey.Key, state);
    }

    private void OnTerminalSummon(Entity<DropshipTerminalComponent> terminal, ref DropshipTerminalSummonDropshipMsg args)
    {
        if (_net.IsClient)
            return;

        if (!_ui.IsUiOpen(terminal.Owner, DropshipTerminalUiKey.Key, args.Actor))
            return;

        if (!TryGetEntity(args.Id, out var computerId) ||
            !TryComp<DropshipNavigationComputerComponent>(computerId, out var computer) ||
            !computer.Hijackable)
        {
            Log.Warning($"{ToPrettyString(args.Actor)} tried to remotely pilot a invalid dropship");
            return;
        }

        var closestDestination = FindClosestLZ(terminal);
        if (closestDestination == null)
        {
            _popup.PopupEntity("There are no dropship destinations near you!", terminal, args.Actor, PopupType.MediumCaution);
            return;
        }

        if (closestDestination.Value.Comp1.Ship is { } ship)
        {
            if (HasComp<FTLComponent>(ship))
            {
                _popup.PopupEntity("There is already a dropship coming here!", terminal, args.Actor, PopupType.MediumCaution);
            }
            else
            {
                _popup.PopupEntity("There is already a dropship here!", terminal, args.Actor, PopupType.MediumCaution);
            }
            return;
        }

        if (!computer.RemoteControl)
        {
            _popup.PopupEntity("This dropship does not have remote-control enabled.", terminal, args.Actor, PopupType.MediumCaution);
            return;
        }

        if (!TryDropshipLaunchPopup(terminal, args.Actor, false))
            return;

        if (!FlyTo((computerId.Value, computer), closestDestination.Value, args.Actor))
        {
            _popup.PopupEntity("This dropship is currently busy. Please try again later.", terminal, args.Actor, PopupType.MediumCaution);
            return;
        }

        _ui.CloseUi(terminal.Owner, DropshipTerminalUiKey.Key, args.Actor);
        _popup.PopupEntity("This dropship is now on its way.", terminal, args.Actor, PopupType.Medium);
    }

    private void OnAttachmentPointMapInit<TComp, TEvent>(Entity<TComp> ent, ref TEvent args) where TComp : IComponent?
    {
        if (_net.IsClient)
            return;

        if (TryGetGridDropship(ent, out var dropship))
        {
            dropship.Comp.AttachmentPoints.Add(ent);
            Dirty(dropship);
        }
    }

    private void OnAttachmentPointRemove<TComp, TEvent>(Entity<TComp> ent, ref TEvent args) where TComp : IComponent?
    {
        if (TryGetGridDropship(ent, out var dropship))
        {
            dropship.Comp.AttachmentPoints.Remove(ent);
            Dirty(dropship);
        }
    }

    private void OnAttachmentExamined(Entity<DropshipWeaponPointComponent> ent, ref ExaminedEvent args)
    {
        using (args.PushGroup(nameof(DropshipWeaponPointComponent)))
        {
            if (TryGetAttachmentContained(ent, ent.Comp.WeaponContainerSlotId, out var weapon))
                args.PushText(Loc.GetString("rmc-dropship-attached", ("attachment", weapon)));

            if (TryGetAttachmentContained(ent, ent.Comp.AmmoContainerSlotId, out var ammo))
            {
                args.PushText(Loc.GetString("rmc-dropship-weapons-point-ammo", ("ammo", ammo)));

                if (TryComp(ammo, out DropshipAmmoComponent? ammoComp))
                {
                    args.PushText(Loc.GetString("rmc-dropship-weapons-rounds-left",
                        ("current", ammoComp.Rounds),
                        ("max", (ammoComp.MaxRounds))));
                }
            }
        }
    }

    private void OnEngineExamined(Entity<DropshipEnginePointComponent> ent, ref ExaminedEvent args)
    {
        using (args.PushGroup(nameof(DropshipWeaponPointComponent)))
        {
            if (TryGetAttachmentContained(ent, ent.Comp.ContainerId, out var attachment))
                args.PushText(Loc.GetString("rmc-dropship-attached", ("attachment", attachment)));
        }
    }

    private void OnElectronicSystemExamined(Entity<DropshipElectronicSystemPointComponent> ent, ref ExaminedEvent args)
    {
        using (args.PushGroup(nameof(DropshipWeaponPointComponent)))
        {
            if (TryGetAttachmentContained(ent, ent.Comp.ContainerId, out var attachment))
                args.PushText(Loc.GetString("rmc-dropship-attached", ("attachment", attachment)));
        }
    }

    private void OnDropshipNavigationLaunchMsg(Entity<DropshipNavigationComputerComponent> ent,
        ref DropshipNavigationLaunchMsg args)
    {
        var user = args.Actor;

        if (!TryGetEntity(args.Target, out var destination))
        {
            Log.Warning($"{ToPrettyString(user)} tried to launch to invalid dropship destination {args.Target}");
            return;
        }

        if (!HasComp<DropshipDestinationComponent>(destination))
        {
            Log.Warning(
                $"{ToPrettyString(args.Actor)} tried to launch to invalid dropship destination {ToPrettyString(destination)}");
            return;
        }

        FlyTo(ent, destination.Value, user);
    }

    private void OnDropshipNavigationCancelMsg(Entity<DropshipNavigationComputerComponent> ent,
        ref DropshipNavigationCancelMsg args)
    {
        var grid = _transform.GetGrid((ent.Owner, Transform(ent.Owner)));
        if (!TryComp(grid, out FTLComponent? ftl) || !TryComp(grid, out DropshipComponent? dropship))
            return;

        if (dropship.Destination != dropship.DepartureLocation ||
            _timing.CurTime + dropship.CancelFlightTime >= ftl.StateTime.End)
            return;

        ftl.StateTime.End = _timing.CurTime + dropship.CancelFlightTime;
        Dirty(grid.Value, dropship);
        RefreshUI();
    }

    private void OnHijackerDestinationChosenMsg(Entity<DropshipNavigationComputerComponent> ent,
        ref DropshipHijackerDestinationChosenBuiMsg args)
    {
        _ui.CloseUi(ent.Owner, DropshipHijackerUiKey.Key, args.Actor);

        if (!TryGetEntity(args.Destination, out var destination))
        {
            Log.Warning($"{ToPrettyString(args.Actor)} tried to hijack to invalid destination");
            return;
        }

        var isHumanHijacker = TryComp<DropshipHijackerComponent>(args.Actor, out var hijackerComp) && hijackerComp.IsHumanHijacker;

        if (isHumanHijacker)
        {
            // Human hijackers target enemy primary LZs
            if (!HasComp<PrimaryLandingZoneComponent>(destination))
            {
                Log.Warning(
                    $"{ToPrettyString(args.Actor)} tried to human-hijack to non-primary-LZ {ToPrettyString(destination)}");
                return;
            }

            // Fly to the enemy LZ (as a hijack, but not a crash)
            if (FlyTo(ent, destination.Value, args.Actor, true) &&
                TryComp(ent, out TransformComponent? xform) &&
                xform.ParentUid.Valid)
            {
                var dropship = EnsureComp<DropshipComponent>(xform.ParentUid);
                // Human hijack does NOT set Crashed - the dropship lands normally at the enemy LZ
                Dirty(xform.ParentUid, dropship);

                // Remove access restrictions from ALL navigation consoles on this dropship
                // so anyone can use them after hijack
                var gridChildren = Transform(xform.ParentUid).ChildEnumerator;
                while (gridChildren.MoveNext(out var child))
                {
                    if (HasComp<DropshipNavigationComputerComponent>(child))
                    {
                        RemCompDeferred<AccessReaderComponent>(child);
                        RemCompDeferred<ActivatableUIRequiresAccessComponent>(child);
                    }
                }

                // Determine hijacker faction for downstream systems
                string? hijackerFaction = null;
                if (TryComp<MarineComponent>(args.Actor, out var marine) && !string.IsNullOrEmpty(marine.Faction))
                    hijackerFaction = marine.Faction.ToLowerInvariant();

                var ev = new DropshipHijackStartEvent(xform.ParentUid, hijackerFaction, true);
                RaiseLocalEvent(ref ev);
            }
        }
        else
        {
            // Xeno hijacker: crash-land on the hijackee's ship
            if (!HasComp<DropshipHijackDestinationComponent>(destination))
            {
                Log.Warning(
                    $"{ToPrettyString(args.Actor)} tried to hijack to invalid destination {ToPrettyString(destination)}");
                return;
            }

            // Validate destination is on a valid ship
            var shipMaps = GetAllShipMaps();
            if (TryComp<TransformComponent>(destination, out var destXform) &&
                destXform.MapUid is { } destMap &&
                !shipMaps.Contains(destMap))
            {
                Log.Warning(
                    $"{ToPrettyString(args.Actor)} tried to hijack to destination on wrong ship {ToPrettyString(destination)}");
                return;
            }

            if (FlyTo(ent, destination.Value, args.Actor, true) &&
                TryComp(ent, out TransformComponent? xform) &&
                xform.ParentUid.Valid)
            {
                var dropship = EnsureComp<DropshipComponent>(xform.ParentUid);
                dropship.Crashed = true;
                Dirty(xform.ParentUid, dropship);

                var ev = new DropshipHijackStartEvent(xform.ParentUid);
                RaiseLocalEvent(ref ev);
            }
        }
    }

    /// <summary>
    ///     Relay interaction events to the entity stored within the Weapon Point.
    /// </summary>
    private void OnInteract(Entity<DropshipWeaponPointComponent> ent, ref InteractHandEvent args)
    {
        var slot = _container.EnsureContainer<ContainerSlot>(ent, ent.Comp.WeaponContainerSlotId);
        RelayInteractToContained(slot, ref args);
    }

    /// <summary>
    ///     Relay interaction events to the entity stored within the Electronic Point.
    /// </summary>
    private void OnInteract(Entity<DropshipElectronicSystemPointComponent> ent, ref InteractHandEvent args)
    {
        var slot = _container.EnsureContainer<ContainerSlot>(ent, ent.Comp.ContainerId);
        RelayInteractToContained(slot, ref args);
    }

    private void RelayInteractToContained(ContainerSlot slot, ref InteractHandEvent args)
    {
        var deployer= slot.ContainedEntity;
        if (!HasComp<RMCEquipmentDeployerComponent>(deployer))
            return;

        var ev = new InteractHandEvent(args.User, args.Target);
        RaiseLocalEvent(deployer.Value, ev);
        args.Handled = ev.Handled;
    }

    public virtual bool FlyTo(Entity<DropshipNavigationComputerComponent> computer,
        EntityUid destination,
        EntityUid? user,
        bool hijack = false,
        float? startupTime = null,
        float? hyperspaceTime = null,
        bool offset = false)
    {
        return false;
    }

    protected virtual void RefreshUI()
    {
    }

    protected virtual void RefreshUI(Entity<DropshipNavigationComputerComponent> computer)
    {
    }

    protected virtual bool IsShuttle(EntityUid dropship)
    {
        return false;
    }

    protected virtual bool IsInFTL(EntityUid dropship)
    {
        return false;
    }

    // Helper: check if a primary landing zone exists for a given faction (null/empty = global)
    protected bool PrimaryExistsForFaction(string? faction)
    {
        var normalized = string.IsNullOrWhiteSpace(faction) ? null : faction.ToLowerInvariant();
        var query = EntityQueryEnumerator<PrimaryLandingZoneComponent>();
        while (query.MoveNext(out _, out var comp))
        {
            var compFaction = string.IsNullOrWhiteSpace(comp.Faction) ? null : comp.Faction.ToLowerInvariant();
            if (compFaction == normalized)
                return true;
        }
        return false;
    }

    protected bool IsPrimaryForFaction(EntityUid destination, string? faction)
    {
        if (!EntityManager.TryGetComponent<PrimaryLandingZoneComponent>(destination, out var comp))
            return false;
        var compFaction = string.IsNullOrWhiteSpace(comp.Faction) ? null : comp.Faction.ToLowerInvariant();
        var normalized = string.IsNullOrWhiteSpace(faction) ? null : faction.ToLowerInvariant();
        return compFaction == normalized;
    }

    private bool TryDropshipLaunchPopup(EntityUid computer, EntityUid user, bool predicted)
    {
        var roundDuration = _gameTicker.RoundDuration();
        if (roundDuration < _dropshipInitialDelay)
        {
            var minutesLeft = Math.Max(1, (int)(_dropshipInitialDelay - roundDuration).TotalMinutes);
            var msg = Loc.GetString("rmc-dropship-pre-flight-fueling", ("minutes", minutesLeft));

            if (predicted)
                _popup.PopupClient(msg, computer, user, PopupType.MediumCaution);
            else
                _popup.PopupEntity(msg, computer, user, PopupType.MediumCaution);

            return false;
        }

        return true;
    }

    protected bool TryDropshipHijackPopup(EntityUid computer, Entity<DropshipHijackerComponent?> user, bool predicted)
    {
        var roundDuration = _gameTicker.RoundDuration();
        if (HasComp<DropshipHijackerComponent>(user) && roundDuration < _hijackInitialDelay)
        {
            var minutesLeft = Math.Max(1, (int)(_hijackInitialDelay - roundDuration).TotalMinutes);
            var msg = Loc.GetString("rmc-dropship-pre-hijack", ("minutes", minutesLeft));

            if (predicted)
                _popup.PopupClient(msg, computer, user, PopupType.MediumCaution);
            else
                _popup.PopupEntity(msg, computer, user, PopupType.MediumCaution);

            return false;
        }

        var map = _transform.GetMap(user.Owner);

        // Prevent double hijack.
        if (TryComp(map, out EvacuationProgressComponent? evacuation) &&
            evacuation.DropShipCrashed)
        {
            var msg = Loc.GetString("rmc-dropship-invalid-hijack");

            if (predicted)
                _popup.PopupClient(msg, computer, user, PopupType.MediumCaution);
            else
                _popup.PopupEntity(msg, computer, user, PopupType.MediumCaution);

            return false;
        }

        // Prevent shipside hijacks by immature xeno queens (xeno-specific check).
        if (HasComp<XenoMaturingComponent>(user) &&
            !HasComp<RMCPlanetComponent>(map))
        {
            var msg = Loc.GetString("rmc-dropship-invalid-hijack");

            if (predicted)
                _popup.PopupClient(msg, computer, user, PopupType.MediumCaution);
            else
                _popup.PopupEntity(msg, computer, user, PopupType.MediumCaution);

            return false;
        }

        return true;
    }

    public bool TryDesignatePrimaryLZ(
        EntityUid actor,
        EntityUid lz)
    {
        if (!HasComp<DropshipDestinationComponent>(lz))
        {
            Log.Warning($"{ToPrettyString(actor)} tried to designate as primary LZ entity {ToPrettyString(lz)} with no {nameof(DropshipDestinationComponent)}!");
            return false;
        }

        // Determine desired faction from the DropshipDestination's FactionController (if any).
        string? desiredFaction = null;
        if (EntityManager.TryGetComponent<DropshipDestinationComponent>(lz, out var lzComp) && !string.IsNullOrWhiteSpace(lzComp.FactionController))
            desiredFaction = lzComp.FactionController.ToLowerInvariant();

        // Prevent duplicate primary for the same faction
        if (PrimaryExistsForFaction(desiredFaction))
        {
            Log.Warning($"{ToPrettyString(actor)} tried to designate as primary LZ entity {ToPrettyString(lz)} when a primary already exists for that faction!");
            return false;
        }

        if (!HasComp<RMCPlanetComponent>(_transform.GetGrid(lz)) &&
            !HasComp<RMCPlanetComponent>(_transform.GetMap(lz)))
        {
            Log.Warning($"{ToPrettyString(actor)} tried to designate entity {ToPrettyString(lz)} on the warship as primary LZ!");
            return false;
        }

        if (GetPrimaryLZCandidates().All(candidate => candidate.Owner != lz))
        {
            Log.Warning($"{ToPrettyString(actor)} tried to designate invalid primary LZ entity {ToPrettyString(lz)}!");
            return false;
        }

        _adminLog.Add(LogType.RMCPrimaryLZ, $"{ToPrettyString(actor):player} designated {ToPrettyString(lz):lz} as primary landing zone");

        var primary = EnsureComp<PrimaryLandingZoneComponent>(lz);
        primary.Faction = desiredFaction;
        EntityManager.Dirty(lz, primary);
        EnsureComp<RMCTrackableComponent>(lz);

        // Auto-set faction on all DropshipTerminal entities on the same map as the LZ
        if (TryComp<TransformComponent>(lz, out var lzXform))
        {
            var terminalQuery = EntityQueryEnumerator<DropshipTerminalComponent, TransformComponent>();
            while (terminalQuery.MoveNext(out var termUid, out var termComp, out var termXform))
            {
                if (termXform.MapID != lzXform.MapID)
                    continue;

                termComp.Faction = desiredFaction;
                Dirty(termUid, termComp);
            }
        }

        RefreshUI();

        var message = Loc.GetString("rmc-announcement-ares-lz-designated", ("name", Name(lz)));
        _marineAnnounce.AnnounceARESStaging(actor, message, null, null, desiredFaction);

        return true;
    }

    public IEnumerable<Entity<MetaDataComponent>> GetPrimaryLZCandidates(string? faction = null)
    {
        // If a global primary exists no candidates should be shown.
        if (PrimaryExistsForFaction(null))
            yield break;

        // If a primary exists for the caller's faction, don't show candidates to that faction either.
        if (!string.IsNullOrWhiteSpace(faction) && PrimaryExistsForFaction(faction))
            yield break;

        var landingZoneQuery = EntityQueryEnumerator<DropshipDestinationComponent, MetaDataComponent, TransformComponent>();
        while (landingZoneQuery.MoveNext(out var uid, out _, out var metaData, out var xform))
        {
            if (!HasComp<RMCPlanetComponent>(xform.ParentUid) &&
                !HasComp<RMCPlanetComponent>(xform.MapUid))
            {
                continue;
            }

            yield return (uid, metaData);
        }
    }

    public bool TryGetGridDropship(EntityUid ent, out Entity<DropshipComponent> dropship)
    {
        if (TryComp(ent, out TransformComponent? xform) &&
            xform.GridUid is { } grid &&
            !TerminatingOrDeleted(grid) &&
            TryComp(xform.GridUid, out DropshipComponent? dropshipComp))
        {
            dropship = (grid, dropshipComp);
            return true;
        }

        dropship = default;
        return false;
    }

    public bool IsWeaponAttached(Entity<DropshipWeaponComponent?> weapon)
    {
        if (!Resolve(weapon, ref weapon.Comp, false) ||
            !TryGetGridDropship(weapon, out var dropship))
        {
            return false;
        }

        if (!_container.TryGetContainingContainer((weapon, null), out var container) ||
            !dropship.Comp.AttachmentPoints.Contains(container.Owner))
        {
            return false;
        }

        return true;
    }
    // wtf why was it private
    public bool TryGetAttachmentContained(
        EntityUid point,
        string containerId,
        out EntityUid contained)
    {
        contained = default;
        if (!_container.TryGetContainer(point, containerId, out var container) ||
            container.ContainedEntities.Count == 0)
        {
            return false;
        }

        contained = container.ContainedEntities[0];
        return true;
    }

    public bool IsInFlight(Entity<DropshipComponent?> dropship)
    {
        if (!Resolve(dropship, ref dropship.Comp, false))
            return false;

        return dropship.Comp.State == FTLState.Travelling || dropship.Comp.State == FTLState.Arriving;
    }

    public bool IsOnDropship(EntityUid entity)
    {
        var grid = _transform.GetGrid(entity);
        return HasComp<DropshipComponent>(grid);
    }

    public bool IsOnDropship(EntityCoordinates coordinates)
    {
        var grid = _transform.GetGrid(coordinates);
        return HasComp<DropshipComponent>(grid);
    }

    public Entity<DropshipDestinationComponent, TransformComponent>? FindClosestLZ(TransformComponent userTransform)
    {
        Entity<DropshipDestinationComponent, TransformComponent>? closestDestination = null;
        var destinations = EntityQueryEnumerator<DropshipDestinationComponent, TransformComponent>();
        while (destinations.MoveNext(out var uid, out var destination, out var xform))
        {
            if (xform.MapID != userTransform.MapID)
                continue;

            if (closestDestination == null)
            {
                closestDestination = (uid, destination, xform);
                continue;
            }

            if (userTransform.Coordinates.TryDistance(EntityManager, xform.Coordinates, out var distance) &&
                userTransform.Coordinates.TryDistance(EntityManager,
                    closestDestination.Value.Comp2.Coordinates,
                    out var oldDistance) &&
                distance < oldDistance)
            {
                closestDestination = (uid, destination, xform);
            }
        }
        return closestDestination;
    }

    public Entity<DropshipDestinationComponent, TransformComponent>? FindClosestLZ(EntityUid entity)
    {
        if (TryComp(entity, out TransformComponent? transform))
            return FindClosestLZ(transform);

        return null;
    }

    /// <summary>
    /// Safely sets the FactionController for a DropshipDestinationComponent and networks the change.
    /// </summary>
    public void SetFactionController(EntityUid uid, string faction)
    {
        if (EntityManager.TryGetComponent<DropshipDestinationComponent>(uid, out var comp))
        {
            comp.FactionController = faction;
            EntityManager.Dirty(uid, comp);
        }
    }


    public void SetDestinationType(EntityUid uid, string destinationtype)
    {
        if (EntityManager.TryGetComponent<DropshipDestinationComponent>(uid, out var comp))
        {
            if (Enum.TryParse<DropshipDestinationComponent.DestinationType>(destinationtype, out var parsed))
                comp.Destinationtype = parsed;
            EntityManager.Dirty(uid, comp);
        }
    }

    public void SetDestinationShip(EntityUid uid, EntityUid? ship)
    {
        if (EntityManager.TryGetComponent<DropshipDestinationComponent>(uid, out var comp))
        {
            comp.Ship = ship;
            EntityManager.Dirty(uid, comp);
        }
    }

    public void SetDestinationHome(EntityUid uid, bool home)
    {
        if (EntityManager.TryGetComponent<DropshipDestinationComponent>(uid, out var comp))
        {
            comp.Home = home;
            EntityManager.Dirty(uid, comp);
        }
    }

    public void SetDropshipDestination(EntityUid uid, EntityUid? destination)
    {
        if (EntityManager.TryGetComponent<DropshipComponent>(uid, out var comp))
        {
            comp.Destination = destination;
            EntityManager.Dirty(uid, comp);
        }
    }
}
