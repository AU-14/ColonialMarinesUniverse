using System.Linq;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Rules;
using Content.Shared.Administration.Logs;
using Content.Shared.CMU14.Round;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
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
    public static readonly TimeSpan DangerCloseWindow = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan ClosedRequestLifetime = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan RequestLifetime = TimeSpan.FromMinutes(15);

    private static readonly SoundSpecifier DangerCloseSound = new SoundPathSpecifier("/Audio/Machines/warning_buzzer.ogg");

    private readonly List<Entry> _requests = new();
    private readonly HashSet<Entity<MobStateComponent>> _mobs = new();
    private int _nextId = 1;
    private TimeSpan _nextSync;

    private sealed class Entry
    {
        public CMUCasRequest Request;
        public string Faction = string.Empty;
        public EntityUid Rto;
        public TimeSpan Created;
        public TimeSpan? ClosedAt;
    }

    public override void Initialize()
    {
        Subs.BuiEvents<DropshipTerminalWeaponsComponent>(DropshipTerminalWeaponsUi.Key,
            subs => subs.Event<CMUCasRequestActionMsg>(OnAction));
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
    public bool TryCreateRequest(EntityUid rto, EntityUid target, CMUCasMunition munition, out int id)
    {
        id = 0;
        if (_net.IsClient || !TryComp(target, out DropshipTargetComponent? mark) ||
            _weapons.GetUserFaction(rto) is not { } faction)
            return false;
        if (!string.IsNullOrEmpty(mark.CreatorFaction) &&
            !string.Equals(mark.CreatorFaction, faction, StringComparison.OrdinalIgnoreCase))
            return false;

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

        var coordinates = _transform.GetMapCoordinates(target);
        var friendlies = CountFriendlies(faction, coordinates, DangerCloseRange);
        id = _nextId++;
        var request = new CMUCasRequest(id, Name(rto), netTarget, mark.Abbreviation, DescribeLocation(coordinates),
            DescribeTarget(faction, coordinates), DescribeFriendlies(faction, coordinates), munition, friendlies > 0,
            CMUCasRequestStatus.Pending, null);
        _requests.Add(new Entry { Request = request, Faction = faction, Rto = rto, Created = _timing.CurTime });

        var danger = request.DangerClose ? Loc.GetString("cmu-cas-radio-danger-close") : string.Empty;
        Radio(rto, faction, Loc.GetString("cmu-cas-radio-request",
            ("callsign", request.Callsign), ("id", id), ("target", request.TargetName),
            ("location", request.Location), ("description", request.TargetDescription),
            ("friendlies", request.Friendlies), ("munition", MunitionName(munition)), ("danger", danger)));
        _adminLog.Add(LogType.RMCDropshipWeapon,
            $"{ToPrettyString(rto)} filed CAS request {id} on {ToPrettyString(target)} for {munition} (danger close: {request.DangerClose}).");
        Sync();
        return true;
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
                SetStatus(entry, CMUCasRequestStatus.Complete, aircraft);
                var bda = TryGetEntity(request.Target, out var bdaTarget) && !TerminatingOrDeleted(bdaTarget)
                    ? DescribeDamage(entry.Faction, _transform.GetMapCoordinates(bdaTarget.Value))
                    : Loc.GetString("cmu-cas-bda-unknown");
                Radio(console, entry.Faction, Loc.GetString("cmu-cas-radio-complete",
                    ("aircraft", aircraft), ("callsign", request.Callsign), ("target", request.TargetName),
                    ("bda", bda)));
                break;
            default:
                return;
        }

        _adminLog.Add(LogType.RMCDropshipWeapon,
            $"{ToPrettyString(actor)} set CAS request {request.Id} to {args.Status} from {ToPrettyString(console)}.");
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
            if (entry.Request.Status is not (CMUCasRequestStatus.Accepted or CMUCasRequestStatus.ClearedHot) ||
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
        return TryComp(console, out WhitelistedShuttleComponent? whitelist) &&
               !string.IsNullOrWhiteSpace(whitelist.Faction)
            ? whitelist.Faction
            : null;
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient || _timing.CurTime < _nextSync)
            return;
        _nextSync = _timing.CurTime + TimeSpan.FromSeconds(1);

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

    public static string MunitionName(CMUCasMunition munition)
    {
        return Robust.Shared.Localization.Loc.GetString($"cmu-cas-munition-{munition.ToString().ToLowerInvariant()}");
    }
}
