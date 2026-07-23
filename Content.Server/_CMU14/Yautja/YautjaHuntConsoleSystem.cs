using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Ghost.Roles.Raffles;
using Content.Server.Humanoid.Systems;
using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Dialog;
using Content.Shared.Coordinates;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Ghost.Roles.Raffles;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._CMU14.Yautja;

public sealed partial class YautjaHuntConsoleSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private DialogSystem _dialog = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedDoorSystem _door = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private MapLoaderSystem _mapLoader = default!;
    [Dependency] private RandomHumanoidSystem _randomHumanoid = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IRobustRandom _random = default!;

    private bool _huntingGroundActivated;
    private TimeSpan _nextHuntAt;
    private TimeSpan _nextBloodingAt;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaHuntConsoleComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<YautjaHuntConsoleComponent, YautjaHuntingGroundSelectedEvent>(OnHuntingGroundSelected);
        SubscribeLocalEvent<YautjaHuntConsoleComponent, YautjaHuntCallSelectedEvent>(OnHuntCallSelected);
        SubscribeLocalEvent<YautjaHuntConsoleComponent, YautjaHuntConsoleDialogCancelledEvent>(OnHuntDialogCancelled);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);

        SubscribeLocalEvent<YautjaHuntEscapeConsoleComponent, InteractHandEvent>(OnEscapeConsoleInteractHand);
        SubscribeLocalEvent<YautjaHuntEscapeConsoleComponent, InteractUsingEvent>(OnEscapeConsoleInteractUsing);
        SubscribeLocalEvent<YautjaHuntEscapeConsoleComponent, YautjaHuntEscapeActionSelectedEvent>(OnEscapeActionSelected);
        SubscribeLocalEvent<YautjaHuntEscapeConsoleComponent, YautjaHuntEscapeScanDoAfterEvent>(OnEscapeScanDoAfter);
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _huntingGroundActivated = false;
        _nextHuntAt = TimeSpan.Zero;
        _nextBloodingAt = TimeSpan.Zero;
    }

    private void OnInteractHand(Entity<YautjaHuntConsoleComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        switch (ent.Comp.Kind)
        {
            case YautjaHuntConsoleKind.HuntingGroundSelection:
                OpenHuntingGroundSelection(ent, args.User);
                break;
            case YautjaHuntConsoleKind.HuntGround:
                OpenHuntCallSelection(ent, args.User, false);
                break;
            case YautjaHuntConsoleKind.Blooding:
                OpenHuntCallSelection(ent, args.User, true);
                break;
        }
    }

    private void OpenHuntingGroundSelection(Entity<YautjaHuntConsoleComponent> ent, EntityUid user)
    {
        if (!CanUseSelectionConsole(user))
        {
            PopupDenied(ent.Owner, user);
            return;
        }

        if (_huntingGroundActivated || TryGetDestination(YautjaHuntTeleporterKind.Ship, ent.Comp.DestinationId, out _))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-hunt-console-selection-already"), ent.Owner, user, PopupType.SmallCaution);
            return;
        }

        if (ent.Comp.AvailableDestinations.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-hunt-console-selection-unavailable"), ent.Owner, user, PopupType.SmallCaution);
            return;
        }

        var options = new List<DialogOption>();
        foreach (var destination in ent.Comp.AvailableDestinations)
        {
            options.Add(new DialogOption(
                destination.DisplayName,
                new YautjaHuntingGroundSelectedEvent(GetNetEntity(user), destination.Id)));
        }

        _dialog.OpenOptions(
            ent.Owner,
            user,
            Loc.GetString("cmu-yautja-hunt-console-selection-title"),
            options,
            Loc.GetString("cmu-yautja-hunt-console-selection-message"),
            new YautjaHuntConsoleDialogCancelledEvent(GetNetEntity(user)));
    }

    private void OnHuntingGroundSelected(Entity<YautjaHuntConsoleComponent> ent, ref YautjaHuntingGroundSelectedEvent args)
    {
        if (!TryGetEntity(args.User, out var user))
            return;

        if (!CanUseSelectionConsole(user.Value))
        {
            PopupDenied(ent.Owner, user.Value);
            return;
        }

        if (_huntingGroundActivated || TryGetDestination(YautjaHuntTeleporterKind.Ship, ent.Comp.DestinationId, out _))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-hunt-console-selection-already"), ent.Owner, user.Value, PopupType.SmallCaution);
            return;
        }

        var destinationId = args.Id;
        var destination = ent.Comp.AvailableDestinations
            .FirstOrDefault(candidate => string.Equals(candidate.Id, destinationId, StringComparison.OrdinalIgnoreCase));
        var options = DeserializationOptions.Default with { InitializeMaps = true };
        if (destination == null || !_mapLoader.TryLoadMap(new ResPath(destination.MapPath), out var loadedMap, out var loadedGrids, options))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-hunt-console-selection-unavailable"), ent.Owner, user.Value, PopupType.SmallCaution);
            return;
        }

        MarkHuntingGrounds(loadedMap.Value, loadedGrids);

        ent.Comp.DestinationId = destination.Id;
        _huntingGroundActivated = true;

        var message = Loc.GetString(
            "cmu-yautja-hunt-console-selection-broadcast",
            ("hunter", Name(user.Value)),
            ("ground", destination.DisplayName));
        AnnounceToYautja(message);
        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(user.Value):player} spawned {destination.DisplayName} (hunting grounds)");
    }

    private void MarkHuntingGrounds(Entity<MapComponent> map, HashSet<Entity<MapGridComponent>>? grids)
    {
        EnsureComp<YautjaHuntingGroundComponent>(map.Owner);

        if (grids == null)
            return;

        foreach (var grid in grids)
        {
            EnsureComp<YautjaHuntingGroundComponent>(grid.Owner);
        }
    }

    private void OpenHuntCallSelection(Entity<YautjaHuntConsoleComponent> ent, EntityUid user, bool blooding)
    {
        if (!CanUseHuntConsole(user))
        {
            PopupHuntConsoleDenied(ent.Owner, user, blooding);
            return;
        }

        var callOptions = blooding ? ent.Comp.BloodingCallOptions : ent.Comp.HuntCallOptions;
        if (callOptions.Count == 0)
        {
            var unavailable = blooding
                ? "cmu-yautja-hunt-console-blooding-unavailable"
                : "cmu-yautja-hunt-console-hunt-ground-unavailable";
            _popup.PopupEntity(Loc.GetString(unavailable), ent.Owner, user, PopupType.SmallCaution);
            return;
        }

        if (TryGetCooldownRemaining(blooding, out var remaining))
        {
            _popup.PopupEntity(
                Loc.GetString("cmu-yautja-hunt-console-cooldown", ("time", FormatRemaining(remaining))),
                ent.Owner,
                user,
                PopupType.SmallCaution);
            return;
        }

        var options = new List<DialogOption>();
        foreach (var call in callOptions)
        {
            options.Add(new DialogOption(
                call.DisplayName,
                new YautjaHuntCallSelectedEvent(GetNetEntity(user), call.Id)));
        }

        _dialog.OpenOptions(
            ent.Owner,
            user,
            blooding
                ? Loc.GetString("cmu-yautja-hunt-console-blooding-title")
                : Loc.GetString("cmu-yautja-hunt-console-hunt-ground-title"),
            options,
            blooding
                ? Loc.GetString("cmu-yautja-hunt-console-blooding-message")
                : Loc.GetString("cmu-yautja-hunt-console-hunt-ground-message"),
            new YautjaHuntConsoleDialogCancelledEvent(GetNetEntity(user)));
    }

    private void OnHuntDialogCancelled(Entity<YautjaHuntConsoleComponent> ent, ref YautjaHuntConsoleDialogCancelledEvent args)
    {
        if (!TryGetEntity(args.User, out var user))
            return;

        var popup = ent.Comp.Kind switch
        {
            YautjaHuntConsoleKind.HuntingGroundSelection => "cmu-yautja-hunt-console-selection-cancelled",
            YautjaHuntConsoleKind.Blooding => "cmu-yautja-hunt-console-blooding-cancelled",
            _ => "cmu-yautja-hunt-console-hunt-ground-cancelled",
        };

        _popup.PopupEntity(Loc.GetString(popup), ent.Owner, user.Value, PopupType.SmallCaution);
    }

    private void OnHuntCallSelected(Entity<YautjaHuntConsoleComponent> ent, ref YautjaHuntCallSelectedEvent args)
    {
        if (!TryGetEntity(args.User, out var user))
            return;

        if (!CanUseHuntConsole(user.Value))
        {
            PopupHuntConsoleDenied(ent.Owner, user.Value, ent.Comp.Kind == YautjaHuntConsoleKind.Blooding);
            return;
        }

        var blooding = ent.Comp.Kind == YautjaHuntConsoleKind.Blooding;
        var callId = args.Id;
        var option = (blooding ? ent.Comp.BloodingCallOptions : ent.Comp.HuntCallOptions)
            .FirstOrDefault(candidate => string.Equals(candidate.Id, callId, StringComparison.OrdinalIgnoreCase));
        if (option == null)
        {
            var unavailable = blooding
                ? "cmu-yautja-hunt-console-blooding-unavailable"
                : "cmu-yautja-hunt-console-hunt-ground-unavailable";
            _popup.PopupEntity(Loc.GetString(unavailable), ent.Owner, user.Value, PopupType.SmallCaution);
            return;
        }

        if (TryGetCooldownRemaining(blooding, out var remaining))
        {
            _popup.PopupEntity(
                Loc.GetString("cmu-yautja-hunt-console-cooldown", ("time", FormatRemaining(remaining))),
                ent.Owner,
                user.Value,
                PopupType.SmallCaution);
            return;
        }

        var created = blooding
            ? TryCreateYoungbloodCall(ent, user.Value, option, bypassEligibility: false)
            : TryCreateHuntCall(ent, user.Value, option, blooding: false);
        if (!created)
            return;

        StartCooldown(ent.Comp, option, blooding);
    }

    public bool TryCreateYoungbloodCall(
        Entity<YautjaHuntConsoleComponent> ent,
        EntityUid requester,
        YautjaHuntCallOption option,
        bool bypassEligibility)
    {
        return TryCreateHuntCall(ent, requester, option, blooding: true, bypassEligibility);
    }

    private bool TryCreateHuntCall(
        Entity<YautjaHuntConsoleComponent> ent,
        EntityUid requester,
        YautjaHuntCallOption option,
        bool blooding,
        bool bypassEligibility = false)
    {
        var destinationKind = blooding
            ? YautjaHuntTeleporterKind.Young
            : YautjaHuntTeleporterKind.Ship;
        var spawnKind = blooding
            ? YautjaHuntSpawnKind.Youngblood
            : YautjaHuntSpawnKind.Prey;

        MapCoordinates coordinates;
        EntityUid? destination;
        if (blooding)
        {
            if (!TryGetDestination(destinationKind, ent.Comp.DestinationId, out var deployDestination) ||
                !TryGetSpawnCoordinates(spawnKind, destinationKind, ent.Comp.DestinationId, out coordinates, out _, false))
            {
                var unavailable = blooding
                    ? "cmu-yautja-hunt-console-blooding-unavailable"
                    : "cmu-yautja-hunt-console-hunt-ground-unavailable";
                _popup.PopupEntity(Loc.GetString(unavailable), ent.Owner, requester, PopupType.SmallCaution);
                return false;
            }

            destination = deployDestination;
        }
        else if (!TryGetSpawnCoordinates(spawnKind, destinationKind, ent.Comp.DestinationId, out coordinates, out destination))
        {
            var unavailable = blooding
                ? "cmu-yautja-hunt-console-blooding-unavailable"
                : "cmu-yautja-hunt-console-hunt-ground-unavailable";
            _popup.PopupEntity(Loc.GetString(unavailable), ent.Owner, requester, PopupType.SmallCaution);
            return false;
        }

        var request = Spawn(null, ent.Owner.ToCoordinates());
        var requestComp = EnsureComp<YautjaHuntCallComponent>(request);
        requestComp.Kind = ent.Comp.Kind;
        requestComp.Requester = requester;
        requestComp.Destination = destination;
        requestComp.DestinationId = ent.Comp.DestinationId;
        requestComp.CallId = option.Id;
        requestComp.CallName = option.DisplayName;

        var maxCount = Math.Max(1, option.SpawnCount > 0 ? option.SpawnCount : ent.Comp.SpawnCount);
        var minCount = Math.Clamp(option.MinSpawnCount, 1, maxCount);
        var count = blooding
            ? _random.Next(minCount, maxCount + 1)
            : maxCount;
        var spawnedCount = 0;
        for (var i = 0; i < count; i++)
        {
            var spawned = TrySpawnPrey(ent.Comp, option, coordinates, blooding);
            if (spawned == null)
                continue;

            spawnedCount++;
            _transform.AttachToGridOrMap(spawned.Value);
            if (blooding)
            {
                EnsureComp<YautjaYoungbloodComponent>(spawned.Value);
                var metadata = EnsureComp<YautjaYoungbloodGhostRoleComponent>(spawned.Value);
                metadata.CallId = option.Id;
                metadata.BypassEligibility = bypassEligibility;
            }

            var ghostRole = EnsureHuntGhostRole(spawned.Value, ent.Comp.Kind);
            if (blooding)
            {
                ghostRole.JobProto = "CMUYautjaYoungblood";
                ghostRole.ReregisterOnGhost = false;
                // Apply the raffle after the Youngblood job metadata is finalized.
                // This preserves the existing registration flow while using the same
                // 30/10/90 guest-role window as regular prey.
                ghostRole.RaffleConfig = CreateYautjaGuestRoleRaffleConfig();
            }
        }

        if (spawnedCount == 0)
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-hunt-console-spawn-failed"), ent.Owner, requester, PopupType.SmallCaution);
            QueueDel(request);
            return false;
        }

        var started = blooding
            ? "cmu-yautja-hunt-console-blooding-started"
            : "cmu-yautja-hunt-console-hunt-ground-started";
        _popup.PopupEntity(Loc.GetString(started), ent.Owner, requester);

        var broadcast = Loc.GetString(
            blooding
                ? "cmu-yautja-hunt-console-blooding-broadcast"
                : "cmu-yautja-hunt-console-hunt-ground-broadcast",
            ("hunter", Name(requester)),
            ("hunt", option.DisplayName));
        AnnounceToYautja(broadcast);

        if (blooding)
        {
            _adminLog.Add(LogType.Action, LogImpact.Medium,
                $"{ToPrettyString(requester):player} has called {option.DisplayName} (Youngblood ERT)");
        }
        else
        {
            _adminLog.Add(LogType.Action, LogImpact.Medium,
                $"{ToPrettyString(requester):player} triggered {option.DisplayName} inside the hunting grounds");
        }
        return true;
    }

    private EntityUid? TrySpawnPrey(
        YautjaHuntConsoleComponent console,
        YautjaHuntCallOption option,
        MapCoordinates coordinates,
        bool blooding)
    {
        var entry = PickSpawnEntry(option.Spawns);
        if (entry == null)
        {
            entry = new YautjaHuntSpawnEntry
            {
                EntityPrototype = blooding ? console.BloodingPrototype : console.HuntPreyPrototype,
            };
        }

        if (entry.RandomHumanoidSettings is { } settings)
        {
            if (!_prototype.HasIndex<RandomHumanoidSettingsPrototype>(settings))
                return null;

            return _randomHumanoid.SpawnRandomHumanoid(settings, _transform.ToCoordinates(coordinates), string.Empty);
        }

        if (entry.EntityPrototype is { } prototype)
        {
            if (!_prototype.HasIndex<EntityPrototype>(prototype))
                return null;

            return Spawn(prototype, coordinates);
        }

        return null;
    }

    private YautjaHuntSpawnEntry? PickSpawnEntry(List<YautjaHuntSpawnEntry> entries)
    {
        if (entries.Count == 0)
            return null;

        var totalWeight = 0;
        foreach (var entry in entries)
        {
            totalWeight += Math.Max(1, entry.Weight);
        }

        var picked = _random.Next(totalWeight);
        foreach (var entry in entries)
        {
            picked -= Math.Max(1, entry.Weight);
            if (picked < 0)
                return entry;
        }

        return entries[^1];
    }

    private GhostRoleComponent EnsureHuntGhostRole(EntityUid spawned, YautjaHuntConsoleKind kind)
    {
        if (TryComp<GhostRoleComponent>(spawned, out var existing))
        {
            EnsureComp<GhostTakeoverAvailableComponent>(spawned);
            existing.RaffleConfig = CreateYautjaGuestRoleRaffleConfig();
            return existing;
        }

        var ghostRole = EnsureComp<GhostRoleComponent>(spawned);
        EnsureComp<GhostTakeoverAvailableComponent>(spawned);
        ghostRole.RaffleConfig = CreateYautjaGuestRoleRaffleConfig();

        if (kind == YautjaHuntConsoleKind.Blooding)
        {
            ghostRole.RoleName = "cmu-yautja-youngblood-ghost-name";
            ghostRole.RoleDescription = "cmu-yautja-youngblood-ghost-description";
            ghostRole.RoleRules = "cmu-yautja-youngblood-ghost-rules";
            return ghostRole;
        }

        ghostRole.RoleName = "cmu-yautja-hunt-prey-ghost-name";
        ghostRole.RoleDescription = "cmu-yautja-hunt-prey-ghost-description";
        ghostRole.RoleRules = "cmu-yautja-hunt-prey-ghost-rules";
        return ghostRole;
    }

    private static GhostRoleRaffleConfig CreateYautjaGuestRoleRaffleConfig()
    {
        return new GhostRoleRaffleConfig(new GhostRoleRaffleSettings
        {
            InitialDuration = 30,
            JoinExtendsDurationBy = 10,
            MaxDuration = 90,
        });
    }

    private void OnEscapeConsoleInteractHand(Entity<YautjaHuntEscapeConsoleComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (!HasComp<YautjaComponent>(args.User))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-hunt-escape-console-nonyautja"), ent.Owner, args.User, PopupType.SmallCaution);
            return;
        }

        var options = new List<DialogOption>
        {
            new(
                "Open",
                new YautjaHuntEscapeActionSelectedEvent(GetNetEntity(args.User), YautjaHuntEscapeAction.Open)),
            new(
                "Close",
                new YautjaHuntEscapeActionSelectedEvent(GetNetEntity(args.User), YautjaHuntEscapeAction.Close)),
        };

        _dialog.OpenOptions(
            ent.Owner,
            args.User,
            Name(ent.Owner),
            options,
            "Do you wish to close or open the shutter?",
            timeout: ent.Comp.DialogTimeout);
    }

    private void OnEscapeActionSelected(Entity<YautjaHuntEscapeConsoleComponent> ent, ref YautjaHuntEscapeActionSelectedEvent args)
    {
        if (!TryGetEntity(args.User, out var user) || !HasComp<YautjaComponent>(user.Value))
            return;

        SetPreserveOpen(ent, user.Value, args.Action == YautjaHuntEscapeAction.Open, true);
    }

    private void OnEscapeConsoleInteractUsing(Entity<YautjaHuntEscapeConsoleComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (ent.Comp.Opened)
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-hunt-escape-console-already-open"), ent.Owner, args.User, PopupType.SmallCaution);
            return;
        }

        if (!_hands.IsHolding(args.User, args.Used))
        {
            _popup.PopupEntity(
                Loc.GetString("cmu-yautja-hunt-escape-console-mask-not-held", ("item", Name(args.Used))),
                ent.Owner,
                args.User,
                PopupType.SmallCaution);
            return;
        }

        if (HasActiveDoAfter(args.User))
            return;

        if (!HasComp<YautjaMaskComponent>(args.Used))
        {
            _popup.PopupEntity(
                Loc.GetString("cmu-yautja-hunt-escape-console-mask-refused", ("item", Name(args.Used))),
                ent.Owner,
                args.User,
                PopupType.SmallCaution);
            return;
        }

        var doAfter = new DoAfterArgs(
            EntityManager,
            args.User,
            ent.Comp.MaskScanDelay,
            new YautjaHuntEscapeScanDoAfterEvent(),
            ent.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            BreakOnHandChange = true,
            BlockDuplicate = true,
            CancelDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameEvent,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        _popup.PopupEntity(
            Loc.GetString("cmu-yautja-hunt-escape-console-scan-start", ("item", Name(args.Used))),
            ent.Owner,
            args.User);
        AnnounceToYautja(Loc.GetString("cmu-yautja-hunt-escape-console-scan-broadcast", ("area", "the hunting grounds")));
    }

    private void OnEscapeScanDoAfter(Entity<YautjaHuntEscapeConsoleComponent> ent, ref YautjaHuntEscapeScanDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (args.Cancelled)
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-hunt-escape-console-scan-cancelled"), ent.Owner, args.User, PopupType.SmallCaution);
            return;
        }

        _popup.PopupEntity(Loc.GetString("cmu-yautja-hunt-escape-console-scan-success"), ent.Owner, args.User);
        SetPreserveOpen(ent, args.User, true, false);
    }

    private void SetPreserveOpen(Entity<YautjaHuntEscapeConsoleComponent> ent, EntityUid user, bool open, bool yautjaCommand)
    {
        if (ent.Comp.Opened == open)
        {
            var already = open
                ? "cmu-yautja-hunt-escape-console-already-open"
                : "cmu-yautja-hunt-escape-console-already-closed";
            _popup.PopupEntity(Loc.GetString(already), ent.Owner, user, PopupType.SmallCaution);
            return;
        }

        ent.Comp.Opened = open;

        var shutterQuery = EntityQueryEnumerator<YautjaPreserveShutterComponent, DoorComponent>();
        while (shutterQuery.MoveNext(out var shutter, out _, out var door))
        {
            if (open)
                _door.TryOpen(shutter, user: user);
            else
                _door.TryClose(shutter, user: user);
        }

        var message = open && yautjaCommand
            ? Loc.GetString("cmu-yautja-hunt-escape-console-opened-by-yautja-broadcast", ("hunter", Name(user)))
            : open
                ? Loc.GetString("cmu-yautja-hunt-escape-console-opened-broadcast")
                : Loc.GetString("cmu-yautja-hunt-escape-console-closed-broadcast");
        AnnounceToYautja(message);
    }

    private bool HasActiveDoAfter(EntityUid user)
    {
        return TryComp(user, out DoAfterComponent? component) &&
               component.DoAfters.Values.Any(active => !active.Cancelled && !active.Completed);
    }

    private bool TryGetSpawnCoordinates(
        YautjaHuntSpawnKind spawnKind,
        YautjaHuntTeleporterKind destinationKind,
        string? destinationId,
        out MapCoordinates coordinates,
        out EntityUid? destination,
        bool allowDestinationFallback = true)
    {
        var query = EntityQueryEnumerator<YautjaHuntSpawnPointComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (Deleted(uid) ||
                component.Kind != spawnKind ||
                !SpawnDestinationMatches(component.DestinationId, destinationId))
            {
                continue;
            }

            coordinates = _transform.GetMapCoordinates(uid);
            destination = null;
            return true;
        }

        if (allowDestinationFallback && TryGetDestination(destinationKind, destinationId, out var destinationUid))
        {
            coordinates = _transform.GetMapCoordinates(destinationUid);
            destination = destinationUid;
            return true;
        }

        coordinates = default;
        destination = null;
        return false;
    }

    private bool TryGetDestination(YautjaHuntTeleporterKind kind, string? destinationId, out EntityUid destination)
    {
        destination = default;
        var query = EntityQueryEnumerator<YautjaHuntTeleportDestinationComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (Deleted(uid) ||
                component.Kind != kind ||
                !DestinationMatches(component.Id, destinationId))
            {
                continue;
            }

            destination = uid;
            return true;
        }

        return false;
    }

    private bool TryGetCooldownRemaining(bool blooding, out TimeSpan remaining)
    {
        var nextAt = blooding ? _nextBloodingAt : _nextHuntAt;
        remaining = nextAt - _timing.CurTime;
        return remaining > TimeSpan.Zero;
    }

    private void StartCooldown(YautjaHuntConsoleComponent console, YautjaHuntCallOption option, bool blooding)
    {
        var baseCooldown = blooding ? console.BloodingCooldown : console.HuntCooldown;
        var multiplier = Math.Max(0.1f, option.CooldownMultiplier);
        var cooldown = TimeSpan.FromTicks((long) (baseCooldown.Ticks * multiplier));

        if (blooding)
            _nextBloodingAt = _timing.CurTime + cooldown;
        else
            _nextHuntAt = _timing.CurTime + cooldown;
    }

    private bool CanUseSelectionConsole(EntityUid user)
    {
        if (HasComp<YautjaYoungbloodComponent>(user) || HasComp<YautjaThrallComponent>(user))
            return false;

        return HasComp<YautjaComponent>(user) || HasComp<YautjaTechAuthorizedComponent>(user);
    }

    private bool CanUseHuntConsole(EntityUid user)
    {
        return HasComp<YautjaComponent>(user) && !HasComp<YautjaYoungbloodComponent>(user);
    }

    private void PopupDenied(EntityUid console, EntityUid user)
    {
        _popup.PopupEntity(Loc.GetString("cmu-yautja-hunt-console-denied"), console, user, PopupType.SmallCaution);
    }

    private void PopupHuntConsoleDenied(EntityUid console, EntityUid user, bool blooding)
    {
        var message = blooding && HasComp<YautjaYoungbloodComponent>(user)
            ? "cmu-yautja-hunt-console-blooding-youngblood-denied"
            : "cmu-yautja-hunt-console-denied";

        _popup.PopupEntity(Loc.GetString(message), console, user, PopupType.SmallCaution);
    }

    private void AnnounceToYautja(string message)
    {
        var query = EntityQueryEnumerator<YautjaComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (!Deleted(uid))
                _popup.PopupEntity(message, uid, uid, PopupType.Medium);
        }
    }

    private static string FormatRemaining(TimeSpan remaining)
    {
        var totalSeconds = Math.Max(0, (int) Math.Ceiling(remaining.TotalSeconds));
        return $"{totalSeconds / 60}:{totalSeconds % 60:00}";
    }

    private static bool DestinationMatches(string? candidateId, string? requestedId)
    {
        return requestedId == null ||
               string.Equals(candidateId, requestedId, StringComparison.OrdinalIgnoreCase);
    }

    private static bool SpawnDestinationMatches(string? candidateId, string? requestedId)
    {
        return requestedId == null ||
               candidateId == null ||
               string.Equals(candidateId, requestedId, StringComparison.OrdinalIgnoreCase);
    }
}
