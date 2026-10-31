using System.Linq;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared._RMC14.Rangefinder;
using Content.Shared._RMC14.Rules;
using Content.Shared.ActionBlocker;
using Content.Shared.Buckle.Components;
using Content.Shared.CMU14.Fighter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.GameTicking;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Guidance;

/// <summary>Aircraft-wide laser receivers and physical, per-munition GPS programming.</summary>
public sealed class CMUGuidanceSystem : EntitySystem
{
    [Dependency] private ActionBlockerSystem _blocker = default!;
    [Dependency] private SharedDropshipSystem _dropships = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedMapSystem _maps = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private RMCPlanetSystem _planet = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private SharedDropshipWeaponSystem _weapons = default!;

    public const int GpsDeviation = 15;
    public static readonly TimeSpan ReportLifetime = TimeSpan.FromMinutes(5);
    private int _nextReportId;
    private readonly List<GroundReport> _reports = [];
    private sealed record GroundReport(EntityUid Source, string Faction, MapCoordinates Target,
        Vector2i Coordinates, Vector2i Deviation, TimeSpan ExpiresAt, CMUGuidanceReport Report);

    public override void Initialize()
    {
        SubscribeLocalEvent<RangefinderComponent, GetVerbsEvent<Verb>>(OnVerbs);
        SubscribeLocalEvent<CMUMunitionGuidanceComponent, ExaminedEvent>(OnAmmoExamined);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _reports.Clear());
        Subs.BuiEvents<CMUGuidanceControlComponent>(CMUGuidanceUi.Key, subs =>
        {
            subs.Event<CMUSetLaserCodeMessage>(OnCode);
            subs.Event<CMUProgramGpsMessage>(OnProgram);
            subs.Event<BoundUIOpenedEvent>(OnOpened);
        });
        Subs.BuiEvents<DropshipTerminalWeaponsComponent>(DropshipTerminalWeaponsUi.Key,
            subs => subs.Event<CMUOpenGuidanceMessage>((Entity<DropshipTerminalWeaponsComponent> ent,
                ref CMUOpenGuidanceMessage args) => Open(ent, args.Actor)));
    }

    public static bool ValidCode(string code)
    {
        return code.Length == 6 && code[4] == '-' &&
            code.Where((_, index) => index != 4).All(c => c is >= '0' and <= '9');
    }

    public string GetCode(EntityUid uid) =>
        TryComp(uid, out CMULaserCodeComponent? code) ? code.Code : CMULaserCodeComponent.Default;

    public bool TryGetLaserCode(EntityUid target, out string code)
    {
        code = CMULaserCodeComponent.Default;
        if (TryComp(target, out LaserDesignatorTargetComponent? laser))
        {
            if (laser.LaserDesignator is { } designator)
                code = GetCode(designator);
            return true;
        }
        if (!HasComp<FighterLaserComponent>(target))
            return false;
        code = GetCode(target);
        return true;
    }

    public EntityUid Receiver(EntityUid source)
    {
        if (TryComp(source, out FighterSeatComponent? seat) && seat.Aircraft is { } aircraft)
            return aircraft;
        return _dropships.TryGetGridDropship(source, out var ship) ? ship.Owner : source;
    }

    public bool CanDetect(EntityUid receiver, EntityUid target) =>
        !TryGetLaserCode(target, out var code) || code == GetCode(Receiver(receiver));

    public bool IsGuided(EntityUid ammo)
    {
        return HasComp<CMUMunitionGuidanceComponent>(ammo) ||
            TryComp(ammo, out DropshipAmmoComponent? payload) &&
            payload.Weapon.Id is "RMCDropshipAttachmentGuidedMissileLauncher" or "CMUDropshipAttachmentJDAMBombClamp";
    }

    public bool UsesGps(EntityUid ammo) =>
        TryComp(ammo, out CMUMunitionGuidanceComponent? guidance) && guidance.Mode == CMUGuidanceMode.Gps;

    public bool TryGetGps(EntityUid ammo, out EntityCoordinates coordinates)
    {
        coordinates = default;
        if (!UsesGps(ammo) || !TryComp(ammo, out CMUMunitionGuidanceComponent? guidance) ||
            guidance.Coordinates is not { } target || !float.IsFinite(target.X) || !float.IsFinite(target.Y))
            return false;
        var maps = EntityQueryEnumerator<MapComponent>();
        while (maps.MoveNext(out var uid, out var map))
        {
            if (map.MapId != target.MapId)
                continue;
            coordinates = _maps.TryFindGridAt(target, out var grid, out _)
                ? _transform.ToCoordinates(grid, target)
                : _transform.ToCoordinates(uid, target);
            return true;
        }
        return false;
    }

    private bool CanProgram(EntityUid host, EntityUid user)
    {
        if (!_blocker.CanInteract(user, host))
            return false;
        if (TryComp(host, out FighterSeatComponent? seat))
            return seat.Occupant == user && TryComp(user, out BuckleComponent? buckle) && buckle.BuckledTo == host;
        return _interaction.InRangeUnobstructed(user, host);
    }

    public void Open(EntityUid host, EntityUid user)
    {
        if (_net.IsClient || !CanProgram(host, user) ||
            !(HasComp<DropshipTerminalWeaponsComponent>(host) || HasComp<FighterSeatComponent>(host) ||
                TryComp(host, out RangefinderComponent? rangefinder) && rangefinder.CanDesignate))
            return;
        EnsureComp<CMUGuidanceControlComponent>(host);
        _ui.SetUi(host, CMUGuidanceUi.Key, new InterfaceData("CMUGuidanceBui"));
        _ui.OpenUi(host, CMUGuidanceUi.Key, user);
        Refresh(host);
    }

    private void OnVerbs(Entity<RangefinderComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !ent.Comp.CanDesignate)
            return;
        var host = args.Target;
        var user = args.User;
        args.Verbs.Add(new Verb { Text = Loc.GetString("cmu-guidance-title"), Act = () => Open(host, user) });
    }

    private void OnOpened(Entity<CMUGuidanceControlComponent> ent, ref BoundUIOpenedEvent args) => Refresh(ent);

    private List<EntityUid> GetAmmo(EntityUid host)
    {
        var receiver = Receiver(host);
        if (TryComp(receiver, out FighterWeaponsComponent? fighter))
        {
            return fighter.Hardpoints.Where(uid => TryComp(uid, out FighterHardpointComponent? point) &&
                    point.Ammo is { } ammo && IsGuided(ammo))
                .Select(uid => Comp<FighterHardpointComponent>(uid).Ammo!.Value).ToList();
        }
        var result = new List<EntityUid>();
        if (!HasComp<DropshipTerminalWeaponsComponent>(host))
            return result;
        var weapons = EntityQueryEnumerator<DropshipWeaponComponent>();
        while (weapons.MoveNext(out var uid, out var weapon))
        {
            if (_dropships.TryGetGridDropship(uid, out var ship) && ship.Owner == receiver &&
                _weapons.TryGetWeaponAmmo((uid, weapon), out var ammo) && IsGuided(ammo))
                result.Add(ammo);
        }
        return result;
    }

    public bool RecordGroundReport(Entity<RangefinderComponent> rangefinder, EntityUid user)
    {
        if (_net.IsClient)
            return false;
        if (!_blocker.CanInteract(user, rangefinder.Owner) || !_interaction.InRangeUnobstructed(user, rangefinder.Owner) ||
            !_planet.IsOnPlanet(Transform(user)) || _dropships.IsOnDropship(user) ||
            _weapons.GetUserFaction(user) is not { } faction ||
            rangefinder.Comp.LastCoords is not { } measured ||
            !_planet.TryGetPlanetSurfaceCoordinates(measured, out var target) ||
            !_planet.TryGetOffset(target, out var offset) ||
            _transform.GetMap(_transform.ToCoordinates(target)) is not { } map)
        {
            Error(rangefinder, user, "cmu-guidance-ground-report-invalid");
            return false;
        }
        var coordinates = target.Position.Floored() + offset;
        var previous = _reports.FirstOrDefault(report => report.Target.MapId == target.MapId &&
            report.Coordinates == coordinates && report.ExpiresAt > _timing.CurTime &&
            report.Faction.Equals(faction, StringComparison.OrdinalIgnoreCase));
        var deviation = previous?.Deviation ??
            new Vector2i(_random.Next(-GpsDeviation, GpsDeviation + 1), _random.Next(-GpsDeviation, GpsDeviation + 1));
        _reports.RemoveAll(report => report.Source == rangefinder.Owner);
        var state = new CMUGuidanceReport(++_nextReportId, Name(user), Name(map), coordinates.X, coordinates.Y);
        _reports.Add(new GroundReport(rangefinder, faction, target, coordinates, deviation,
            _timing.CurTime + ReportLifetime, state));
        _popup.PopupEntity(Loc.GetString("cmu-guidance-ground-report-sent", ("x", coordinates.X), ("y", coordinates.Y)),
            rangefinder, user);
        RefreshOpenControls();
        return true;
    }

    private bool CanUseReport(EntityUid host, GroundReport report)
    {
        var receiver = Receiver(host);
        var faction = TryComp(receiver, out FighterWeaponsComponent? fighter)
            ? fighter.Faction : _weapons.GetUserFaction(host) ?? _weapons.GetConsoleFaction(host);
        return report.ExpiresAt > _timing.CurTime && !TerminatingOrDeleted(report.Source) &&
            report.Faction.Equals(faction, StringComparison.OrdinalIgnoreCase) &&
            (!TryComp(receiver, out FighterAircraftComponent? aircraft) ||
                _transform.GetMapId(aircraft.TerrainMap) == report.Target.MapId);
    }

    public List<CMUGuidanceReport> GetReports(EntityUid host) =>
        _reports.Where(report => CanUseReport(host, report)).Select(report => report.Report).ToList();

    public bool TryGetGroundReport(EntityUid user, int id, out MapCoordinates coordinates)
    {
        coordinates = default;
        var report = _reports.FirstOrDefault(report => report.Report.Id == id && CanUseReport(user, report));
        if (report == null)
            return false;
        coordinates = new MapCoordinates(report.Target.Position.Floored() + report.Deviation, report.Target.MapId);
        return true;
    }

    public override void Update(float frameTime)
    {
        if (!_net.IsClient && _reports.RemoveAll(report =>
            report.ExpiresAt <= _timing.CurTime || TerminatingOrDeleted(report.Source)) > 0)
            RefreshOpenControls();
    }

    private void RefreshOpenControls()
    {
        var controls = EntityQueryEnumerator<CMUGuidanceControlComponent>();
        while (controls.MoveNext(out var uid, out _))
            Refresh(uid);
    }

    private void Refresh(EntityUid host)
    {
        if (_net.IsClient)
            return;
        var ammo = new List<CMUGuidanceAmmo>();
        foreach (var uid in GetAmmo(host))
        {
            TryComp(uid, out CMUMunitionGuidanceComponent? settings);
            NetEntity? map = null;
            var x = 0;
            var y = 0;
            if (TryGetGps(uid, out var coordinates))
            {
                map = GetNetEntity(_transform.GetMap(coordinates));
                if (settings?.PlanetCoordinates is { } planetCoordinates)
                {
                    x = planetCoordinates.X;
                    y = planetCoordinates.Y;
                }
            }
            ammo.Add(new CMUGuidanceAmmo(GetNetEntity(uid), Name(uid), settings?.Mode ?? CMUGuidanceMode.Laser,
                settings?.GpsOnly ?? false, map, x, y));
        }
        var designator = HasComp<RangefinderComponent>(host) || HasComp<FighterSeatComponent>(host);
        _ui.SetUiState(host, CMUGuidanceUi.Key, new CMUGuidanceState(
            HasComp<RangefinderComponent>(host) ? null : GetCode(Receiver(host)),
            designator ? GetCode(host) : null, ammo, GetReports(host)));
    }

    private void OnCode(Entity<CMUGuidanceControlComponent> ent, ref CMUSetLaserCodeMessage args)
    {
        if (_net.IsClient || !CanProgram(ent, args.Actor))
            return;
        if (!ValidCode(args.Code))
        {
            Error(ent, args.Actor, "cmu-guidance-invalid-code");
            return;
        }
        if (args.Designator && !(HasComp<FighterSeatComponent>(ent) ||
            TryComp(ent, out RangefinderComponent? rangefinder) && rangefinder.CanDesignate))
            return;
        if (!args.Designator && !(HasComp<FighterSeatComponent>(ent) ||
            HasComp<DropshipTerminalWeaponsComponent>(ent)))
            return;
        var uid = args.Designator ? ent.Owner : Receiver(ent);
        var code = EnsureComp<CMULaserCodeComponent>(uid);
        code.Code = args.Code;
        Dirty(uid, code);
        if (TryComp(uid, out FighterSeatComponent? seat) && seat.Laser is { } laser)
        {
            var live = EnsureComp<CMULaserCodeComponent>(laser);
            live.Code = code.Code;
            Dirty(laser, live);
        }
        _weapons.RefreshGuidanceTargets();
        var aircraft = EntityQueryEnumerator<FighterWeaponsComponent>();
        while (aircraft.MoveNext(out _, out var weapons))
            weapons.NextRefresh = TimeSpan.Zero;
        Refresh(ent);
    }

    private void OnProgram(Entity<CMUGuidanceControlComponent> ent, ref CMUProgramGpsMessage args)
    {
        if (_net.IsClient || !CanProgram(ent, args.Actor))
            return;
        var uid = GetEntity(args.Ammo);
        if (!GetAmmo(ent).Contains(uid) || !Enum.IsDefined(args.Mode))
        {
            Error(ent, args.Actor, "cmu-guidance-ammo-unavailable");
            return;
        }
        var guidance = EnsureComp<CMUMunitionGuidanceComponent>(uid);
        if (guidance.GpsOnly && args.Mode != CMUGuidanceMode.Gps)
        {
            Error(ent, args.Actor, "cmu-guidance-gps-only");
            return;
        }
        if (args.Mode == CMUGuidanceMode.Gps)
        {
            var reportId = args.ReportId;
            var report = _reports.FirstOrDefault(report => report.Report.Id == reportId && CanUseReport(ent, report));
            if (report == null ||
                !_planet.TryPlanetToCoordinates(report.Coordinates, out var coordinates, report.Target.MapId))
            {
                Error(ent, args.Actor, "cmu-guidance-invalid-gps");
                return;
            }
            guidance.Coordinates = new MapCoordinates(coordinates.Position + report.Deviation, coordinates.MapId);
            guidance.PlanetCoordinates = report.Coordinates;
        }
        guidance.Mode = args.Mode;
        Dirty(uid, guidance);
        if (TryComp(Receiver(ent), out FighterWeaponsComponent? weapons))
            weapons.NextRefresh = TimeSpan.Zero;
        Refresh(ent);
    }

    private void Error(EntityUid host, EntityUid user, string key) =>
        _popup.PopupEntity(Loc.GetString(key), host, user, PopupType.SmallCaution);

    private void OnAmmoExamined(Entity<CMUMunitionGuidanceComponent> ent, ref ExaminedEvent args)
    {
        args.PushText(Loc.GetString(ent.Comp.Mode == CMUGuidanceMode.Gps ? "cmu-guidance-gps" : "cmu-guidance-laser"));
        if (ent.Comp.PlanetCoordinates is { } coordinates)
            args.PushText(Loc.GetString("cmu-guidance-gps-examine", ("x", coordinates.X), ("y", coordinates.Y)));
    }
}
