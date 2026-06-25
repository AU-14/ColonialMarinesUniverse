using System.Linq;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Marines.Announce;
using Content.Shared._RMC14.Rules;
using Content.Shared._RMC14.Thunderdome;
using Content.Shared._RMC14.Vehicle;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Announce;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared._CMU14.Yautja;
using Content.Shared._CMU14.Threats;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using AbominationComponent = Content.Shared._CMU14.Threats.Mobs.Abomination.AbominationComponent;
using AbominationMimicTransformedComponent = Content.Shared._CMU14.Threats.Mobs.Abomination.AbominationMimicTransformedComponent;
using InsurgencyRuleComponent = Content.Shared._CMU14.Threats.InsurgencyRuleComponent;

namespace Content.Shared._RMC14.Bioscan;

public sealed partial class BioscanSystem : EntitySystem
{
    [Dependency] private AreaSystem _area = default!;
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private SharedMarineAnnounceSystem _marineAnnounce = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedXenoAnnounceSystem _xenoAnnounce = default!;
    private const string None = "none";

    private TimeSpan _bioscanInitialDelay;
    private TimeSpan _bioscanCheckDelay;
    private TimeSpan _bioscanMinimumCooldown;
    private TimeSpan _bioscanBaseCooldown;
    private int _bioscanVariance;

    private readonly List<MapId> _planetMaps = new();
    private readonly List<MapId> _warshipMaps = new();
    private readonly List<string> _planetAreas = new();
    private readonly List<string> _warshipAreas = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<BioscanComponent, MapInitEvent>(OnMapInit);

        Subs.CVar(_config, RMCCVars.RMCBioscanInitialDelaySeconds, v => _bioscanInitialDelay = TimeSpan.FromSeconds(v), true);
        Subs.CVar(_config, RMCCVars.RMCBioscanCheckDelaySeconds, v => _bioscanCheckDelay = TimeSpan.FromSeconds(v), true);
        Subs.CVar(_config, RMCCVars.RMCBioscanMinimumCooldownSeconds, v => _bioscanMinimumCooldown = TimeSpan.FromSeconds(v), true);
        Subs.CVar(_config, RMCCVars.RMCBioscanBaseCooldownSeconds, v => _bioscanBaseCooldown = TimeSpan.FromSeconds(v), true);
        Subs.CVar(_config, RMCCVars.RMCBioscanVariance, v => _bioscanVariance = v, true);
    }

    private void OnMapInit(Entity<BioscanComponent> ent, ref MapInitEvent _)
    {
        ent.Comp.LastMarine = _timing.CurTime + _bioscanInitialDelay;
        ent.Comp.LastXeno = _timing.CurTime + _bioscanInitialDelay;
        Dirty(ent);
    }

    private bool TargetIsMarine(EntityUid uid) => HasComp<MarineComponent>(uid);
    private bool TargetIsThreat(EntityUid uid)
    {
        return HasComp<XenoComponent>(uid)
            || HasComp<AbominationComponent>(uid)
            || HasComp<AbominationMimicTransformedComponent>(uid)
            || HasComp<YautjaComponent>(uid)
            || HasComp<YautjaAbominationComponent>(uid);
    }

    private bool TryBioscan(
        Func<EntityUid, bool> targetIsThreat,
        TimeSpan last,
        bool force,
        ref int max,
        out int alive,
        out int aliveShip,
        out int alivePlanet,
        out string warshipArea,
        out string planetArea)
    {
        alive = 0;
        aliveShip = 0;
        alivePlanet = 0;
        warshipArea = None;
        planetArea = None;

        var time = _timing.CurTime;
        if (!force && last + _bioscanMinimumCooldown > time)
            return false;

        _planetMaps.Clear();
        _warshipMaps.Clear();
        _planetAreas.Clear();
        _warshipAreas.Clear();

        var planetQuery = EntityQueryEnumerator<RMCPlanetComponent, TransformComponent>();
        while (planetQuery.MoveNext(out _, out var xform))
            _planetMaps.Add(xform.MapID);

        var warshipQuery = EntityQueryEnumerator<AlmayerComponent, TransformComponent>();
        while (warshipQuery.MoveNext(out _, out var xform))
            _warshipMaps.Add(xform.MapID);

        var playersQuery = EntityQueryEnumerator<ActorComponent, MobStateComponent, TransformComponent>();
        while (playersQuery.MoveNext(out var uid, out _, out var mobState, out var xform))
        {
            if (!_mobState.IsAlive(uid, mobState))
                continue;

            if (HasComp<ThunderdomeMapComponent>(xform.MapUid))
                continue;

            if (!targetIsThreat(uid))
                continue;

            alive++;
            var bioscanBlocked = _area.BioscanBlocked(uid, out var name);
            var mapId = xform.MapID;
            if (_warshipMaps.Contains(mapId))
            {
                if (!bioscanBlocked && !HasComp<VehicleInteriorOccupantComponent>(uid))
                {
                    aliveShip++;

                    if (name != null)
                        _warshipAreas.Add(name);
                }
            }
            else if (_planetMaps.Contains(mapId))
            {
                alivePlanet++;

                if (!bioscanBlocked && name != null)
                    _planetAreas.Add(name);
            }
        }

        if (alive > max)
            max = alive;

        if (max != 0)
        {
            var next = _bioscanBaseCooldown * alive / max;
            if (next < _bioscanMinimumCooldown)
                next = _bioscanMinimumCooldown;

            next += last;
            if (!force && time < next)
                return false;
        }
        else if (!force)
            return false;

        if (_warshipAreas.Count > 0)
            warshipArea = _random.Pick(_warshipAreas);

        if (_planetAreas.Count > 0)
            planetArea = _random.Pick(_planetAreas);

        return true;
    }

    public void TryBioscanARES(Entity<BioscanComponent> bioscan, bool force)
    {
        var time = _timing.CurTime;
        if (!TryBioscan(
                TargetIsThreat,
                bioscan.Comp.LastMarine,
                force,
                ref bioscan.Comp.MaxXenoAlive,
                out _,
                out var aliveShip,
                out var alivePlanet,
                out var warshipArea,
                out var planetArea))
            return;

        var variance = _bioscanVariance;
        alivePlanet = Math.Max(0, alivePlanet + _random.Next(-variance, variance + 1));
        if (alivePlanet == 0)
            planetArea = None;

        bioscan.Comp.LastMarine = time;
        var message = Loc.GetString(
            "rmc-bioscan-ares",
            ("shipUncontained", aliveShip),
            ("shipLocation", warshipArea),
            ("planetLocation", planetArea),
            ("onPlanet", alivePlanet)
        );

        _marineAnnounce.AnnounceARESStaging(null, message, bioscan.Comp.MarineSound, "rmc-bioscan-ares-announcement", "govfor");
        Dirty(bioscan);
    }

    public void TryBioscanQueenMother(Entity<BioscanComponent> bioscan, bool force)
    {
        var time = _timing.CurTime;
        if (!TryBioscan(
                TargetIsMarine,
                bioscan.Comp.LastXeno,
                force,
                ref bioscan.Comp.MaxMarinesAlive,
                out _,
                out var aliveShip,
                out var alivePlanet,
                out var warshipArea,
                out var planetArea))
            return;

        var variance = _bioscanVariance;
        aliveShip = Math.Max(0, aliveShip + _random.Next(-variance, variance + 1));
        if (aliveShip == 0)
            planetArea = None;

        bioscan.Comp.LastXeno = time;
        var message = Loc.GetString(
            "rmc-bioscan-xeno",
            ("shipLocation", warshipArea),
            ("planetLocation", planetArea),
            ("onShip", aliveShip),
            ("onPlanet", alivePlanet)
        );

        message = Loc.GetString("rmc-bioscan-xeno-announcement", ("message", message));
        _xenoAnnounce.AnnounceAll(default, message, bioscan.Comp.XenoSound);
        Dirty(bioscan);
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        if (EntityQuery<InsurgencyRuleComponent>().Any())
            return;

        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<BioscanComponent>();
        while (query.MoveNext(out var uid, out var bioscan))
        {
            if (bioscan.NextCheck > time)
                continue;

            bioscan.NextCheck = time + _bioscanCheckDelay;
            Dirty(uid, bioscan);

            TryBioscanARES((uid, bioscan), false);
            TryBioscanQueenMother((uid, bioscan), false);
        }
    }
}