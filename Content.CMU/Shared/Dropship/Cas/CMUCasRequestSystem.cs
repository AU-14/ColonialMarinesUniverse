using System.Linq;
using Robust.Shared.Prototypes;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Rules;
using Content.Shared._RMC14.Dropship.AttachmentPoint;
using Content.Shared._RMC14.Dropship.Utility.Components;
using Content.Shared.Actions;
using Content.Shared.Administration.Logs;
using Content.Shared.CMU14.Radio;
using Content.Shared.CMU14.Round;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Dropship.Cas;

/// <summary>
/// RTO close air support requests and danger close confirmation for dropship weapons consoles.
/// </summary>
public sealed class CMUCasRequestSystem : EntitySystem
{
    [Dependency] private ISharedAdminLogManager _adminLog = default!;
    [Dependency] private AreaSystem _areas = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private SharedDropshipSystem _dropships = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private RMCPlanetSystem _planet = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedDropshipWeaponSystem _weapons = default!;

    public const float DangerCloseRange = 5f;
    public const float RequestCoverRange = 6f;
    public const float TargetScanRange = 4f;
    public const int MaxOpenRequests = 6;
    /// <summary>How far from the RTO a mark, or the hostile for a radio mark, can be.</summary>
    public const float CallRange = 45f;
    /// <summary>Marks within this distance of an RTO's laser designation count as designated.</summary>
    public const float DesignationRange = 2f;
    /// <summary>Strike spread multiplier on RTO-designated targets: 25% tighter.</summary>
    public const float DesignationSpreadMultiplier = 0.75f;
    public const int AutoVolleysPerWeapon = 3;
    public static readonly TimeSpan CarryDecisionWindow = TimeSpan.FromSeconds(12);
    public static readonly TimeSpan AutoIdleTimeout = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan DangerCloseWindow = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan ClosedRequestLifetime = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan RequestLifetime = TimeSpan.FromMinutes(15);

    private static readonly SoundSpecifier DangerCloseSound = new SoundPathSpecifier("/Audio/Machines/warning_buzzer.ogg");

    private readonly List<Entry> _requests = new();
    private readonly HashSet<Entity<MobStateComponent>> _mobs = new();
    private readonly HashSet<Entity<DropshipTargetComponent>> _marks = new();
    private readonly HashSet<Entity<CMUCasRtoDesignationComponent>> _designations = new();
    private int _nextId = 1;
    private TimeSpan _nextSync;
    private TimeSpan _nextCarry;

    private sealed class Entry
    {
        public CMUCasRequest Request;
        public string Faction = string.Empty;
        public EntityUid Rto;
        public TimeSpan Created;
        public TimeSpan? ClosedAt;
        public EntityUid? Console;
        public TimeSpan CarryDeadline;
        public TimeSpan? LastVolley;
        public TimeSpan? AutoStarted;
        public bool HadHostiles;
        public TimeSpan? TargetEmptySince;
        public readonly Dictionary<EntityUid, int> Volleys = new();
    }

    public override void Initialize()
    {
        Subs.BuiEvents<DropshipTerminalWeaponsComponent>(DropshipTerminalWeaponsUi.Key, subs =>
        {
            subs.Event<CMUCasRequestActionMsg>(OnAction);
            subs.Event<CMUCasCarryModeMsg>(OnCarryMode);
        });
        Subs.BuiEvents<CMUCasUplinkComponent>(CMUCasCallUi.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnUplinkOpened);
            subs.Event<CMUCasCallSendMsg>(OnUplinkSend);
            subs.Event<CMUCasCallClearHotMsg>(OnUplinkClearHot);
            subs.Event<CMUCasCallAbortMsg>(OnUplinkAbort);
        });
        SubscribeLocalEvent<CMUCasUplinkComponent, GetItemActionsEvent>(OnUplinkGetActions);
        SubscribeLocalEvent<CMUCasUplinkComponent, CMUCasUplinkActionEvent>(OnUplinkAction);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ =>
        {
            _requests.Clear();
            _nextId = 1;
        });
    }

    public IReadOnlyList<CMUCasRequest> GetRequests(string? faction)
    {
        return _requests.Where(entry => FactionMatches(faction, entry.Faction)).Select(entry => entry.Request).ToList();
    }

    public bool TryGetRequest(int id, out CMUCasRequest request)
    {
        var entry = _requests.FirstOrDefault(candidate => candidate.Request.Id == id);
        request = entry?.Request ?? default;
        return entry != null;
    }

    /// <summary>Files a 9-line for <paramref name="target"/>, filling the target, friendlies and location lines.</summary>
    public bool TryCreateRequest(EntityUid rto, EntityUid target, CMUCasMunition munition, out int id,
        CMUCasEffect effect = CMUCasEffect.Any, string remarks = "", bool radioMark = false)
    {
        id = 0;
        if (_net.IsClient || !TryComp(target, out DropshipTargetComponent? mark) ||
            _weapons.GetUserFaction(rto) is not { } faction)
            return false;
        if (!string.IsNullOrEmpty(mark.CreatorFaction) &&
            !string.Equals(mark.CreatorFaction, faction, StringComparison.OrdinalIgnoreCase))
            return false;

        var coordinates = _transform.GetMapCoordinates(target);
        var origin = _transform.GetMapCoordinates(rto);
        if (origin.MapId != coordinates.MapId ||
            (origin.Position - coordinates.Position).LengthSquared() > CallRange * CallRange)
        {
            _popup.PopupEntity(Loc.GetString("cmu-cas-request-out-of-range", ("range", (int) CallRange)), rto, rto,
                PopupType.SmallCaution);
            return false;
        }

        var netTarget = GetNetEntity(target);
        if (_requests.Any(entry => entry.Request.Target == netTarget && entry.ClosedAt == null))
        {
            _popup.PopupEntity(Loc.GetString("cmu-cas-request-duplicate"), rto, rto, PopupType.SmallCaution);
            return false;
        }

        if (_requests.Count(entry => entry.ClosedAt == null &&
                                     entry.Faction.Equals(faction, StringComparison.OrdinalIgnoreCase)) >= MaxOpenRequests)
        {
            _popup.PopupEntity(Loc.GetString("cmu-cas-request-full"), rto, rto, PopupType.SmallCaution);
            return false;
        }

        var friendlies = CountFriendlies(faction, coordinates, DangerCloseRange);
        id = _nextId++;
        remarks = remarks.Trim();
        if (remarks.Length > MaxRemarksLength)
            remarks = remarks[..MaxRemarksLength];
        var request = new CMUCasRequest(id, Name(rto), netTarget, mark.Abbreviation, DescribeLocation(coordinates),
            DescribeTarget(faction, coordinates), DescribeFriendlies(faction, coordinates), munition, friendlies > 0,
            CMUCasRequestStatus.Pending, null)
        {
            Effect = effect,
            Remarks = remarks,
            RadioMark = radioMark,
            Designated = IsRtoDesignated(target),
        };
        _requests.Add(new Entry
        {
            Request = request, Faction = faction, Rto = rto, Created = _timing.CurTime,
            HadHostiles = HasHostiles(faction, coordinates),
        });

        var danger = request.DangerClose ? Loc.GetString("cmu-cas-radio-danger-close") : string.Empty;
        Radio(rto, faction, Loc.GetString("cmu-cas-radio-request",
            ("callsign", request.Callsign), ("id", id), ("target", request.TargetName),
            ("location", request.Location), ("description", request.TargetDescription),
            ("friendlies", request.Friendlies), ("munition", MunitionName(munition)), ("danger", danger)));
        if (effect != CMUCasEffect.Any || remarks.Length > 0 || request.Designated || radioMark)
            Radio(rto, faction, Loc.GetString("cmu-cas-radio-request-remarks", ("id", id),
                ("effect", EffectName(effect)), ("remarks", remarks.Length > 0 ? remarks : "-"),
                ("mark", Loc.GetString(radioMark ? "cmu-cas-mark-radio" : request.Designated
                    ? "cmu-cas-mark-laser" : "cmu-cas-mark-flare"))));
        _adminLog.Add(LogType.RMCDropshipWeapon,
            $"{ToPrettyString(rto)} filed CAS request {id} on {ToPrettyString(target)} for {munition}/{effect} (danger close: {request.DangerClose}, radio mark: {radioMark}, designated: {request.Designated}, remarks: {remarks}).");
        Sync();
        return true;
    }

    public const int MaxRemarksLength = 120;

    /// <summary>
    /// Calls CAS without a flare: the uplink drops a radio mark on the nearest hostile within <see cref="CallRange"/>.
    /// </summary>
    public bool TryCreateRadioMarkRequest(EntityUid rto, CMUCasMunition munition, CMUCasEffect effect, string remarks,
        out int id, EntProtoId? markPrototype = null)
    {
        id = 0;
        if (_net.IsClient || _weapons.GetUserFaction(rto) is not { } faction)
            return false;
        if (FindNearestHostile(rto, faction) is not { } hostile)
        {
            _popup.PopupEntity(Loc.GetString("cmu-cas-request-no-hostile", ("range", (int) CallRange)), rto, rto,
                PopupType.SmallCaution);
            return false;
        }

        var mark = Spawn(markPrototype ?? "CMUCasRadioMark", _transform.GetMapCoordinates(hostile));
        var abbreviation = _weapons.GetUserAbbreviation(rto, _weapons.ComputeNextId());
        _weapons.MakeDropshipTarget(mark, abbreviation, faction);
        _metaData.SetEntityName(mark, Loc.GetString("cmu-cas-radio-mark-name", ("id", abbreviation)));
        if (TryCreateRequest(rto, mark, munition, out id, effect, remarks, true))
            return true;
        QueueDel(mark);
        return false;
    }

    /// <summary>Whether a CAS strike at <paramref name="coordinates"/> lands on an RTO's laser designation.</summary>
    public bool IsRtoDesignated(EntityCoordinates coordinates, EntityUid? target = null)
    {
        if (target is { } marked && HasComp<CMUCasRtoDesignationComponent>(marked))
            return true;
        if (!coordinates.IsValid(EntityManager))
            return false;
        _designations.Clear();
        _lookup.GetEntitiesInRange(_transform.ToMapCoordinates(coordinates), DesignationRange, _designations);
        return _designations.Count > 0;
    }

    public bool IsRtoDesignated(EntityUid target)
    {
        return IsRtoDesignated(Transform(target).Coordinates, target);
    }

    private EntityUid? FindNearestHostile(EntityUid rto, string faction)
    {
        var origin = _transform.GetMapCoordinates(rto);
        _mobs.Clear();
        _lookup.GetEntitiesInRange(origin, CallRange, _mobs);
        EntityUid? best = null;
        var bestDistance = float.MaxValue;
        foreach (var mob in _mobs)
        {
            if (mob.Owner == rto || mob.Comp.CurrentState != MobState.Alive || IsFriendly(faction, mob))
                continue;
            var distance = (_transform.GetMapCoordinates(mob).Position - origin.Position).LengthSquared();
            if (distance >= bestDistance)
                continue;
            bestDistance = distance;
            best = mob;
        }

        return best;
    }

    /// <summary>The RTO's clearance for an accepted request.</summary>
    public bool TryClearHot(EntityUid rto, int id)
    {
        if (_net.IsClient || FindEntry(id) is not { } entry || entry.Rto != rto ||
            entry.Request.Status != CMUCasRequestStatus.Accepted)
            return false;
        SetStatus(entry, CMUCasRequestStatus.ClearedHot, entry.Request.Aircraft);
        Radio(rto, entry.Faction, Loc.GetString("cmu-cas-radio-cleared",
            ("callsign", entry.Request.Callsign), ("aircraft", entry.Request.Aircraft ?? string.Empty),
            ("target", entry.Request.TargetName)));
        _adminLog.Add(LogType.RMCDropshipWeapon, $"{ToPrettyString(rto)} cleared CAS request {id} hot.");
        return true;
    }

    public bool TryAbort(EntityUid rto, int id)
    {
        if (_net.IsClient || FindEntry(id) is not { } entry || entry.Rto != rto || entry.ClosedAt != null)
            return false;
        SetStatus(entry, CMUCasRequestStatus.Denied, entry.Request.Aircraft);
        Radio(rto, entry.Faction, Loc.GetString("cmu-cas-radio-abort",
            ("callsign", entry.Request.Callsign), ("id", id)));
        _adminLog.Add(LogType.RMCDropshipWeapon, $"{ToPrettyString(rto)} aborted CAS request {id}.");
        return true;
    }

    public IEnumerable<CMUCasRequest> GetOwnRequests(EntityUid rto)
    {
        return _requests.Where(entry => entry.Rto == rto && entry.ClosedAt == null).Select(entry => entry.Request);
    }

    private void OnAction(Entity<DropshipTerminalWeaponsComponent> console, ref CMUCasRequestActionMsg args)
    {
        if (_net.IsClient || FindEntry(args.Id) is not { } entry ||
            !FactionMatches(ConsoleFaction(console), entry.Faction))
            return;

        var actor = args.Actor;
        var aircraft = _dropships.TryGetGridDropship(console, out var ship) ? Name(ship) : Name(console);
        var request = entry.Request;
        switch (args.Status)
        {
            case CMUCasRequestStatus.Accepted when request.Status == CMUCasRequestStatus.Pending:
                entry.Console = console;
                entry.CarryDeadline = _timing.CurTime + CarryDecisionWindow;
                entry.Request = entry.Request with
                {
                    CarryMode = CMUCasCarryMode.Undecided,
                    CarryCountdown = (int) CarryDecisionWindow.TotalSeconds,
                };
                SetStatus(entry, CMUCasRequestStatus.Accepted, aircraft);
                if (TryGetEntity(request.Target, out var target) && !TerminatingOrDeleted(target))
                    _weapons.SetTerminalTarget(console, target);
                Radio(console, entry.Faction, Loc.GetString("cmu-cas-radio-accepted",
                    ("aircraft", aircraft), ("callsign", request.Callsign), ("id", request.Id),
                    ("target", request.TargetName)));
                break;
            case CMUCasRequestStatus.Denied when request.Status is CMUCasRequestStatus.Pending or CMUCasRequestStatus.Accepted:
                SetStatus(entry, CMUCasRequestStatus.Denied, aircraft);
                Radio(console, entry.Faction, Loc.GetString("cmu-cas-radio-denied",
                    ("aircraft", aircraft), ("callsign", request.Callsign), ("id", request.Id)));
                break;
            case CMUCasRequestStatus.Complete when request.Status is CMUCasRequestStatus.Accepted or CMUCasRequestStatus.ClearedHot:
                Complete(entry, console, aircraft);
                break;
            default:
                return;
        }

        _adminLog.Add(LogType.RMCDropshipWeapon,
            $"{ToPrettyString(actor)} set CAS request {request.Id} to {args.Status} from {ToPrettyString(console)}.");
    }

    private void Complete(Entry entry, EntityUid source, string aircraft)
    {
        var request = entry.Request;
        SetStatus(entry, CMUCasRequestStatus.Complete, aircraft);
        var bda = TryGetEntity(request.Target, out var bdaTarget) && !TerminatingOrDeleted(bdaTarget)
            ? DescribeDamage(entry.Faction, _transform.GetMapCoordinates(bdaTarget.Value))
            : Loc.GetString("cmu-cas-bda-unknown");
        Radio(source, entry.Faction, Loc.GetString("cmu-cas-radio-complete",
            ("aircraft", aircraft), ("callsign", request.Callsign), ("target", request.TargetName),
            ("bda", bda)));
    }

    private void OnCarryMode(Entity<DropshipTerminalWeaponsComponent> console, ref CMUCasCarryModeMsg args)
    {
        if (_net.IsClient || args.Mode == CMUCasCarryMode.Undecided || FindEntry(args.Id) is not { } entry ||
            !FactionMatches(ConsoleFaction(console), entry.Faction) ||
            entry.Request.Status is not (CMUCasRequestStatus.Accepted or CMUCasRequestStatus.ClearedHot) ||
            entry.Request.CarryMode == args.Mode)
            return;
        entry.Console = console;
        SetCarryMode(entry, args.Mode, args.Actor);
    }

    /// <summary>Sets how an accepted request is carried out. Auto has the fire control computer fly the strike itself.</summary>
    public bool SetCarryMode(int id, CMUCasCarryMode mode, EntityUid? actor = null)
    {
        if (FindEntry(id) is not { } entry ||
            entry.Request.Status is not (CMUCasRequestStatus.Accepted or CMUCasRequestStatus.ClearedHot))
            return false;
        SetCarryMode(entry, mode, actor);
        return true;
    }

    private void SetCarryMode(Entry entry, CMUCasCarryMode mode, EntityUid? actor)
    {
        entry.Request = entry.Request with { CarryMode = mode, CarryCountdown = 0 };
        entry.AutoStarted = null;
        var aircraft = entry.Request.Aircraft ?? string.Empty;
        Radio(entry.Console ?? entry.Rto, entry.Faction, Loc.GetString(mode == CMUCasCarryMode.Auto
                ? "cmu-cas-radio-carry-auto" : "cmu-cas-radio-carry-manual",
            ("aircraft", aircraft), ("callsign", entry.Request.Callsign), ("id", entry.Request.Id)));
        if (actor is { } user)
            _adminLog.Add(LogType.RMCDropshipWeapon, $"{ToPrettyString(user)} set CAS request {entry.Request.Id} to {mode} carry-out.");
        else
            _adminLog.Add(LogType.RMCDropshipWeapon, $"CAS request {entry.Request.Id} defaulted to {mode} carry-out after the decision window lapsed.");
        Sync();
    }

    private void UpdateCarry()
    {
        var now = _timing.CurTime;
        var changed = false;
        foreach (var entry in _requests.ToList())
        {
            if (entry.ClosedAt == null && entry.HadHostiles &&
                TryGetEntity(entry.Request.Target, out var mark) && !TerminatingOrDeleted(mark))
            {
                if (HasHostiles(entry.Faction, _transform.GetMapCoordinates(mark.Value)))
                    entry.TargetEmptySince = null;
                else if (entry.TargetEmptySince is not { } empty)
                    entry.TargetEmptySince = now;
                else if (now - empty >= TimeSpan.FromSeconds(5))
                {
                    Cancel(entry, "cmu-cas-cancel-target-lost");
                    continue;
                }
            }
            if (entry.Request.Status is not (CMUCasRequestStatus.Accepted or CMUCasRequestStatus.ClearedHot))
                continue;
            switch (entry.Request.CarryMode)
            {
                case CMUCasCarryMode.Undecided:
                    var left = (int) Math.Ceiling(Math.Max(0, (entry.CarryDeadline - now).TotalSeconds));
                    if (left <= 0)
                    {
                        SetCarryMode(entry, CMUCasCarryMode.Auto, null);
                        break;
                    }
                    if (left != entry.Request.CarryCountdown)
                    {
                        entry.Request = entry.Request with { CarryCountdown = left };
                        changed = true;
                    }
                    break;
                case CMUCasCarryMode.Auto:
                    changed |= RunAutoCarry(entry, now);
                    break;
            }
        }

        if (changed)
            Sync();
    }

    /// <summary>Fires the ship's matching onboard ordnance at the request mark until each weapon has flown its volleys.</summary>
    private bool RunAutoCarry(Entry entry, TimeSpan now)
    {
        if (entry.Request.Status != CMUCasRequestStatus.ClearedHot)
            return false;
        if (entry.Console is not { } console || TerminatingOrDeleted(console) ||
            !TryComp(console, out DropshipTerminalWeaponsComponent? terminal) ||
            !_dropships.TryGetGridDropship(console, out var ship) ||
            !TryGetEntity(entry.Request.Target, out var target) || TerminatingOrDeleted(target))
            return false;

        var weapons = GetAutoWeapons(ship, entry.Request.Munition);
        if (weapons.Count == 0)
        {
            Cancel(entry, "cmu-cas-cancel-no-weapon");
            return false;
        }
        if (!weapons.Any(weapon => _weapons.CanFireInFlightMode(ship, weapon.Comp)))
        {
            entry.AutoStarted = null;
            if (entry.LastVolley != null)
                entry.LastVolley = now;
            return false;
        }
        entry.AutoStarted ??= now;
        var coordinates = Transform(target.Value).Coordinates;
        var fired = false;
        foreach (var (weapon, comp) in weapons)
        {
            var volleys = entry.Volleys.GetValueOrDefault(weapon);
            if (volleys >= AutoVolleysPerWeapon || comp.NextFireAt is { } next && now < next)
                continue;
            if (!_weapons.TryFireWeapon(weapon, coordinates, DropshipWeaponStrikeType.Direct, null, terminal, comp))
                continue;
            entry.Volleys[weapon] = volleys + 1;
            entry.LastVolley = now;
            fired = true;
            _adminLog.Add(LogType.RMCDropshipWeapon,
                $"Automatic carry-out of CAS request {entry.Request.Id} fired {ToPrettyString(weapon)} at {ToPrettyString(target)}.");
        }

        if (fired)
            entry.Request = entry.Request with { CarryVolleys = entry.Volleys.Values.Sum() };

        var finished = weapons.Count > 0 && weapons.All(w => entry.Volleys.GetValueOrDefault(w.Owner) >= AutoVolleysPerWeapon);
        if (finished || entry.LastVolley is { } last && now - last > AutoIdleTimeout)
        {
            Complete(entry, console, entry.Request.Aircraft ?? Name(ship));
            return false;
        }
        if (!fired && entry.LastVolley == null && now - entry.AutoStarted.Value > AutoIdleTimeout)
        {
            Cancel(entry, "cmu-cas-cancel-unable");
            return false;
        }

        return fired;
    }

    private List<Entity<DropshipWeaponComponent>> GetAutoWeapons(EntityUid ship, CMUCasMunition munition)
    {
        var all = new List<Entity<DropshipWeaponComponent>>();
        if (!TryComp(ship, out DropshipComponent? dropship))
            return all;
        foreach (var point in dropship.AttachmentPoints)
        {
            var slot = TryComp(point, out DropshipWeaponPointComponent? weaponPoint)
                ? weaponPoint.WeaponContainerSlotId
                : TryComp(point, out DropshipUtilityPointComponent? utilityPoint) ? utilityPoint.UtilitySlotId : null;
            if (slot == null || !_containers.TryGetContainer(point, slot, out var container))
                continue;
            foreach (var contained in container.ContainedEntities)
            {
                if (TryComp(contained, out DropshipWeaponComponent? weapon) && !weapon.GunneryOnly && !weapon.DirectFireOnly)
                    all.Add((contained, weapon));
            }
        }

        return all.Where(weapon => Matches(munition, weapon.Comp)).ToList();
    }

    private void Cancel(Entry entry, string reason)
    {
        entry.Request = entry.Request with { StatusDetail = Loc.GetString(reason) };
        SetStatus(entry, CMUCasRequestStatus.Denied, entry.Request.Aircraft);
        Radio(entry.Console ?? entry.Rto, entry.Faction, Loc.GetString("cmu-cas-radio-cancelled",
            ("id", entry.Request.Id), ("reason", entry.Request.StatusDetail)));
        _adminLog.Add(LogType.RMCDropshipWeapon,
            $"CAS request {entry.Request.Id} cancelled: {entry.Request.StatusDetail}.");
    }

    private bool HasHostiles(string faction, MapCoordinates coordinates)
    {
        _mobs.Clear();
        _lookup.GetEntitiesInRange(coordinates, TargetScanRange, _mobs);
        return _mobs.Any(mob => mob.Comp.CurrentState == MobState.Alive && !IsFriendly(faction, mob));
    }

    public bool CheckClearance(EntityUid console, EntityCoordinates coordinates, EntityUid? actor = null,
        EntityUid? selectedTarget = null)
    {
        var faction = ConsoleFaction(console);
        var impact = _transform.ToMapCoordinates(coordinates);
        foreach (var entry in _requests)
        {
            if (entry.ClosedAt != null || entry.Request.Status == CMUCasRequestStatus.ClearedHot ||
                !FactionMatches(faction, entry.Faction) ||
                !TryGetEntity(entry.Request.Target, out var mark) || TerminatingOrDeleted(mark))
                continue;
            var anchor = _transform.GetMapCoordinates(mark.Value);
            if (mark != selectedTarget && (anchor.MapId != impact.MapId ||
                (anchor.Position - impact.Position).LengthSquared() > RequestCoverRange * RequestCoverRange))
                continue;
            if (actor is { } user)
                _popup.PopupEntity(Loc.GetString("cmu-cas-clearance-required"), console, user, PopupType.MediumCaution);
            return false;
        }
        return true;
    }

    public static bool Matches(CMUCasMunition munition, DropshipWeaponComponent weapon)
    {
        var abbreviation = weapon.Abbreviation;
        return munition switch
        {
            CMUCasMunition.Any => !string.Equals(abbreviation, "FLR", StringComparison.Ordinal),
            CMUCasMunition.Guns => abbreviation is "GAU" or "ATC" or "HWR",
            CMUCasMunition.Gau => abbreviation == "GAU",
            CMUCasMunition.Bofors => abbreviation == "ATC",
            CMUCasMunition.Howitzer => abbreviation == "HWR",
            CMUCasMunition.Rockets => abbreviation == "RKT",
            CMUCasMunition.Missiles => abbreviation == "MSL",
            CMUCasMunition.Bombs => abbreviation == "BMB",
            CMUCasMunition.Lasers => abbreviation == "LZR",
            CMUCasMunition.ChinGun => abbreviation == "M90",
            _ => false,
        };
    }

    private void OnUplinkGetActions(Entity<CMUCasUplinkComponent> ent, ref GetItemActionsEvent args)
    {
        if ((args.SlotFlags & ent.Comp.Slot) == 0)
            return;
        args.AddAction(ref ent.Comp.Action, ent.Comp.ActionId, ent);
    }

    private void OnUplinkAction(Entity<CMUCasUplinkComponent> ent, ref CMUCasUplinkActionEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;
        if (_weapons.GetUserFaction(args.Performer) == null)
        {
            _popup.PopupClient(Loc.GetString("cmu-cas-uplink-no-faction"), args.Performer, args.Performer);
            return;
        }
        _ui.TryToggleUi(ent.Owner, CMUCasCallUi.Key, args.Performer);
    }

    private void OnUplinkOpened(Entity<CMUCasUplinkComponent> ent, ref BoundUIOpenedEvent args)
    {
        RefreshUplink(ent);
    }

    private bool CanUseUplink(EntityUid actor)
    {
        return HasComp<WearingANPRCComponent>(actor);
    }

    private void OnUplinkSend(Entity<CMUCasUplinkComponent> ent, ref CMUCasCallSendMsg args)
    {
        if (_net.IsClient || !CanUseUplink(args.Actor))
            return;
        var rto = args.Actor;
        var munition = Enum.IsDefined(args.Munition) ? args.Munition : CMUCasMunition.Any;
        var effect = Enum.IsDefined(args.Effect) ? args.Effect : CMUCasEffect.Any;
        var sent = false;
        var id = 0;
        if (args.Target is { } netTarget)
        {
            if (TryGetEntity(netTarget, out var target) && HasComp<DropshipTargetComponent>(target))
                sent = TryCreateRequest(rto, target.Value, munition, out id, effect, args.Remarks);
        }
        else
        {
            sent = TryCreateRadioMarkRequest(rto, munition, effect, args.Remarks, out id, ent.Comp.RadioMark);
        }

        if (sent)
            _popup.PopupEntity(Loc.GetString("cmu-cas-request-sent", ("id", id)), rto, rto);
        RefreshUplink(ent);
    }

    private void OnUplinkClearHot(Entity<CMUCasUplinkComponent> ent, ref CMUCasCallClearHotMsg args)
    {
        if (_net.IsClient || !CanUseUplink(args.Actor))
            return;
        TryClearHot(args.Actor, args.Id);
        RefreshUplink(ent);
    }

    private void OnUplinkAbort(Entity<CMUCasUplinkComponent> ent, ref CMUCasCallAbortMsg args)
    {
        if (_net.IsClient || !CanUseUplink(args.Actor))
            return;
        TryAbort(args.Actor, args.Id);
        RefreshUplink(ent);
    }

    private void RefreshUplinks()
    {
        var query = EntityQueryEnumerator<CMUCasUplinkComponent>();
        while (query.MoveNext(out var uid, out var uplink))
        {
            if (_ui.IsUiOpen(uid, CMUCasCallUi.Key))
                RefreshUplink((uid, uplink));
        }
    }

    private void RefreshUplink(Entity<CMUCasUplinkComponent> ent)
    {
        if (_net.IsClient)
            return;
        foreach (var actor in _ui.GetActors(ent.Owner, CMUCasCallUi.Key))
        {
            if (BuildUplinkState(actor) is { } state)
                _ui.SetUiState(ent.Owner, CMUCasCallUi.Key, state);
            return;
        }
    }

    private CMUCasCallBuiState? BuildUplinkState(EntityUid rto)
    {
        if (_weapons.GetUserFaction(rto) is not { } faction)
            return null;
        var origin = _transform.GetMapCoordinates(rto);
        var marks = new List<CMUCasCallTarget>();
        _marks.Clear();
        _lookup.GetEntitiesInRange(origin, CallRange, _marks);
        foreach (var mark in _marks.OrderBy(m => (_transform.GetMapCoordinates(m).Position - origin.Position).LengthSquared()))
        {
            if (!string.IsNullOrEmpty(mark.Comp.CreatorFaction) &&
                !string.Equals(mark.Comp.CreatorFaction, faction, StringComparison.OrdinalIgnoreCase))
                continue;
            marks.Add(DescribeCallTarget(faction, origin, mark, GetNetEntity(mark),
                mark.Comp.Abbreviation ?? Name(mark), IsRtoDesignated(mark)));
        }

        CMUCasCallTarget? nearest = null;
        if (FindNearestHostile(rto, faction) is { } hostile)
            nearest = DescribeCallTarget(faction, origin, hostile, null, Loc.GetString("cmu-cas-call-nearest-hostile"), false);

        var requests = _requests.Where(entry => entry.Rto == rto).Select(entry => entry.Request).ToList();
        return new CMUCasCallBuiState(Name(rto), marks, nearest, requests, (int) CallRange);
    }

    private CMUCasCallTarget DescribeCallTarget(string faction, MapCoordinates origin, EntityUid target,
        NetEntity? net, string name, bool designated)
    {
        var coordinates = _transform.GetMapCoordinates(target);
        return new CMUCasCallTarget(net, name, DescribeLocation(coordinates), DescribeTarget(faction, coordinates),
            DescribeFriendlies(faction, coordinates), (int) MathF.Round((coordinates.Position - origin.Position).Length()),
            CountFriendlies(faction, coordinates, DangerCloseRange) > 0, designated);
    }

    /// <summary>
    /// Danger close gate for a CAS strike: the first shot near friendlies only warns, a repeat inside
    /// <see cref="DangerCloseWindow"/> confirms. Accepted RTO requests already carry the ground commander's consent.
    /// </summary>
    public bool CheckDangerClose(Entity<DropshipTerminalWeaponsComponent> console, EntityCoordinates target,
        EntityUid actor, EntityUid weapon)
    {
        if (_net.IsClient)
            return true;

        var faction = ConsoleFaction(console);
        var coordinates = _transform.ToMapCoordinates(target);
        var friendlies = CountFriendlies(faction, coordinates, DangerCloseRange);
        if (friendlies == 0)
            return true;

        if (IsCoveredByRequest(faction, coordinates))
        {
            _adminLog.Add(LogType.RMCDropshipWeapon,
                $"{ToPrettyString(actor)} fired {ToPrettyString(weapon)} danger close ({friendlies} friendlies) under an accepted RTO request.");
            return true;
        }

        var now = _timing.CurTime;
        if (console.Comp.DangerCloseTarget is { } pending && now <= console.Comp.DangerCloseExpires &&
            pending.TryDistance(EntityManager, _transform, target, out var distance) && distance <= 2f)
        {
            console.Comp.DangerCloseExpires = now + DangerCloseWindow;
            _adminLog.Add(LogType.RMCDropshipWeapon,
                $"{ToPrettyString(actor)} confirmed danger close and fired {ToPrettyString(weapon)} with {friendlies} friendlies within {DangerCloseRange} tiles.");
            return true;
        }

        console.Comp.DangerCloseTarget = target;
        console.Comp.DangerCloseExpires = now + DangerCloseWindow;
        _popup.PopupCursor(Loc.GetString("cmu-cas-danger-close-warning", ("count", friendlies),
            ("seconds", (int) DangerCloseWindow.TotalSeconds)), actor, PopupType.LargeCaution);
        _audio.PlayPvs(DangerCloseSound, console);
        _adminLog.Add(LogType.RMCDropshipWeapon,
            $"{ToPrettyString(actor)} was warned of danger close ({friendlies} friendlies) firing {ToPrettyString(weapon)}.");
        return false;
    }

    public bool IsCoveredByRequest(string? faction, MapCoordinates coordinates)
    {
        foreach (var entry in _requests)
        {
            if (entry.Request.Status != CMUCasRequestStatus.ClearedHot ||
                !FactionMatches(faction, entry.Faction) ||
                !TryGetEntity(entry.Request.Target, out var target) || TerminatingOrDeleted(target))
                continue;
            var anchor = _transform.GetMapCoordinates(target.Value);
            if (anchor.MapId == coordinates.MapId &&
                (anchor.Position - coordinates.Position).LengthSquared() <= RequestCoverRange * RequestCoverRange)
                return true;
        }

        return false;
    }

    public int CountFriendlies(string? faction, MapCoordinates coordinates, float range)
    {
        var count = 0;
        _mobs.Clear();
        _lookup.GetEntitiesInRange(coordinates, range, _mobs);
        foreach (var mob in _mobs)
        {
            if (mob.Comp.CurrentState != MobState.Dead && IsFriendly(faction, mob))
                count++;
        }

        return count;
    }

    public string? ConsoleFaction(EntityUid console)
    {
        return _weapons.GetConsoleFaction(console);
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;
        if (_timing.CurTime >= _nextCarry)
        {
            _nextCarry = _timing.CurTime + TimeSpan.FromSeconds(0.25);
            UpdateCarry();
        }
        if (_timing.CurTime < _nextSync)
            return;
        _nextSync = _timing.CurTime + TimeSpan.FromSeconds(1);
        RefreshUplinks();

        var now = _timing.CurTime;
        _requests.RemoveAll(entry =>
            entry.ClosedAt is { } closed && now - closed > ClosedRequestLifetime ||
            now - entry.Created > RequestLifetime ||
            entry.ClosedAt == null && (!TryGetEntity(entry.Request.Target, out var target) || TerminatingOrDeleted(target)));
        // Also picks up consoles that spawned after the last change.
        Sync();
    }

    private void SetStatus(Entry entry, CMUCasRequestStatus status, string? aircraft)
    {
        entry.Request = entry.Request with { Status = status, Aircraft = aircraft };
        if (status is CMUCasRequestStatus.Denied or CMUCasRequestStatus.Complete)
            entry.ClosedAt = _timing.CurTime;
        Sync();
    }

    private void Sync()
    {
        var query = EntityQueryEnumerator<DropshipTerminalWeaponsComponent>();
        while (query.MoveNext(out var uid, out var terminal))
        {
            var requests = GetRequests(ConsoleFaction(uid));
            if (terminal.CasRequests.SequenceEqual(requests))
                continue;
            terminal.CasRequests = requests.ToList();
            Dirty(uid, terminal);
        }
    }

    private Entry? FindEntry(int id)
    {
        return _requests.FirstOrDefault(entry => entry.Request.Id == id);
    }

    private void Radio(EntityUid source, string faction, string message)
    {
        var ev = new CMUCasRadioEvent(source, faction, message);
        RaiseLocalEvent(ref ev);
    }

    private bool IsFriendly(string? faction, EntityUid mob)
    {
        return TryComp(mob, out MarineComponent? marine) &&
               (faction == null || marine.Faction is { } mobFaction &&
                   mobFaction.Equals(faction, StringComparison.OrdinalIgnoreCase));
    }

    private static bool FactionMatches(string? console, string request)
    {
        return console == null || console.Equals(request, StringComparison.OrdinalIgnoreCase);
    }

    private string DescribeLocation(MapCoordinates coordinates)
    {
        var area = _areas.TryGetArea(coordinates, out _, out var areaProto)
            ? areaProto.Name
            : Loc.GetString("cmu-dropship-radio-unknown-area");
        var position = coordinates.Position.Floored();
        if (_planet.TryGetOffset(coordinates, out var offset))
            position += offset;
        return Loc.GetString("cmu-dropship-radio-location", ("area", area), ("x", position.X), ("y", position.Y));
    }

    private string DescribeTarget(string faction, MapCoordinates coordinates)
    {
        var hostiles = 0;
        _mobs.Clear();
        _lookup.GetEntitiesInRange(coordinates, TargetScanRange, _mobs);
        foreach (var mob in _mobs)
        {
            if (mob.Comp.CurrentState == MobState.Alive && !IsFriendly(faction, mob))
                hostiles++;
        }

        return hostiles == 0
            ? Loc.GetString("cmu-cas-target-none")
            : Loc.GetString("cmu-cas-target-hostiles", ("count", hostiles));
    }

    private string DescribeFriendlies(string faction, MapCoordinates coordinates)
    {
        EntityUid? nearest = null;
        var best = float.MaxValue;
        _mobs.Clear();
        _lookup.GetEntitiesInRange(coordinates, 15f, _mobs);
        foreach (var mob in _mobs)
        {
            if (mob.Comp.CurrentState == MobState.Dead || !IsFriendly(faction, mob))
                continue;
            var distance = (_transform.GetMapCoordinates(mob).Position - coordinates.Position).LengthSquared();
            if (distance >= best)
                continue;
            best = distance;
            nearest = mob;
        }

        if (nearest is not { } friendly)
            return Loc.GetString("cmu-cas-friendlies-none");
        var delta = _transform.GetMapCoordinates(friendly).Position - coordinates.Position;
        return Loc.GetString("cmu-cas-friendlies-nearest", ("distance", (int) MathF.Round(delta.Length())),
            ("direction", delta.LengthSquared() < 0.01f ? "-" : delta.ToWorldAngle().GetDir().ToString()));
    }

    private string DescribeDamage(string faction, MapCoordinates coordinates)
    {
        int down = 0, standing = 0;
        _mobs.Clear();
        _lookup.GetEntitiesInRange(coordinates, DangerCloseRange, _mobs);
        foreach (var mob in _mobs)
        {
            if (IsFriendly(faction, mob))
                continue;
            if (mob.Comp.CurrentState == MobState.Alive)
                standing++;
            else
                down++;
        }

        return Loc.GetString("cmu-cas-bda", ("down", down), ("standing", standing));
    }

    public static string EffectName(CMUCasEffect effect)
    {
        return Robust.Shared.Localization.Loc.GetString($"cmu-cas-effect-{effect.ToString().ToLowerInvariant()}");
    }

    public static string MunitionName(CMUCasMunition munition)
    {
        return Robust.Shared.Localization.Loc.GetString($"cmu-cas-munition-{munition.ToString().ToLowerInvariant()}");
    }
}
