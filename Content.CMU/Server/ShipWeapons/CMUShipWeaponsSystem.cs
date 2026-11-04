using System.Linq;
using System.Numerics;
using Content.Server.Administration.Logs;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Explosion;
using Content.Shared._RMC14.Map;
using Content.Shared._RMC14.Marines.Announce;
using Content.Shared._RMC14.Mortar;
using Content.Shared._RMC14.Overwatch;
using Content.Shared._RMC14.Rules;
using Content.Shared.Access.Systems;
using Content.Shared.CMU14.ShipWeapons;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Content.Shared.CMU14.ZLevels.Ordnance;
using Content.Shared.Database;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.ShipWeapons;

public sealed partial class CMUShipWeaponsSystem : EntitySystem
{
    [Dependency] private AccessReaderSystem _access = default!;
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private AreaSystem _area = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private Content.Server.Explosion.EntitySystems.ExplosionSystem _explosion = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedMarineAnnounceSystem _marineAnnounce = default!;
    [Dependency] private SharedMortarSystem _mortar = default!;
    [Dependency] private CMUTopDownOrdnanceSystem _ordnance = default!;
    [Dependency] private SharedOverwatchConsoleSystem _overwatch = default!;
    [Dependency] private RMCPlanetSystem _planet = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedRMCExplosionSystem _rmcExplosion = default!;
    [Dependency] private RMCMapSystem _rmcMap = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private CMUSharedZLevelsSystem _zLevels = default!;

    private readonly HashSet<Entity<MobStateComponent>> _mobs = new();

    private static readonly SoundSpecifier TravelSound = new SoundPathSpecifier("/Audio/_RMC14/Weapons/gun_orbital_travel.ogg");

    /// <summary>How long <see cref="TravelSound"/> runs, so it can start that long before the first impact.</summary>
    private static readonly TimeSpan TravelSoundLength = TimeSpan.FromSeconds(12);

    public override void Initialize()
    {
        InitializeGuns();
        InitializeEffects();
        SubscribeLocalEvent<CMUShipWeaponsTerminalComponent, BoundUIOpenedEvent>(OnTerminalOpened);
        Subs.BuiEvents<CMUShipWeaponsTerminalComponent>(CMUShipWeaponsUiKey.Key,
            subs => subs.Event<CMUShipWeaponsArmBuiMsg>(OnArm));
        SubscribeLocalEvent<OverwatchConsoleComponent, CMUShipWeaponsRefreshEvent>(OnOverwatchOpened);
        Subs.BuiEvents<OverwatchConsoleComponent>(OverwatchConsoleUI.Key,
            subs => subs.Event<CMUShipWeaponSelectBuiMsg>(OnSelect));
        SubscribeLocalEvent<OverwatchConsoleComponent, CMUShipWeaponLaunchEvent>(OnLaunch);
        SubscribeLocalEvent<CMUTechShipWeaponMissionEvent>(OnTechMission);
        SubscribeLocalEvent<CMUShipWeaponStrikeSpawnerComponent, MapInitEvent>(OnStrikeSpawnerMapInit);
    }

    private void OnStrikeSpawnerMapInit(Entity<CMUShipWeaponStrikeSpawnerComponent> ent, ref MapInitEvent args)
    {
        if (!StartStrike(_transform.GetMapCoordinates(ent), ent.Comp.Weapon, ent.Comp.Bearing, ent.Comp.Instant))
            Log.Warning($"{ToPrettyString(ent)} couldn't start its strike there: out of bounds, underground or protected.");

        QueueDel(ent);
    }

    private void OnTechMission(CMUTechShipWeaponMissionEvent ev)
    {
        // The team's ship is whichever one has that team's arming terminal.
        var terminals = EntityQueryEnumerator<CMUShipWeaponsTerminalComponent>();
        while (terminals.MoveNext(out var uid, out var terminal))
        {
            if (!string.Equals(terminal.Faction, ev.Team, StringComparison.OrdinalIgnoreCase) ||
                !TryGetShip(uid, out var ship))
            {
                continue;
            }

            var state = EnsureComp<CMUShipWeaponsComponent>(ship);
            state.Weapons.Add(ev.Weapon);
            state.MaxUses++;
            state.RemainingUses++;
            _adminLog.Add(LogType.Action, LogImpact.Medium,
                $"Tech purchase added a {ev.Weapon} fire mission to ship {ToPrettyString(ship)} for {ev.Team}");
            UpdateShip(ship);
            return;
        }

        Log.Warning($"No {ev.Team} ship weapons terminal to give a {ev.Weapon} fire mission to.");
    }

    /// <summary>
    /// Entity identity separates duplicate ship templates and factions. Decks of one ship share a network.
    /// Non-Z ships on the same map remain separate because their grids, not the map, own the loadouts.
    /// </summary>
    public bool TryGetShip(EntityUid terminal, out EntityUid ship)
    {
        var xform = Transform(terminal);
        if (xform.MapUid is { } map && _zLevels.TryGetZNetwork(map, out var network))
        {
            ship = network.Value.Owner;
            return true;
        }

        ship = xform.GridUid ?? default;
        return ship.IsValid();
    }

    private void OnTerminalOpened(Entity<CMUShipWeaponsTerminalComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateTerminal(ent);
    }

    private void OnOverwatchOpened(Entity<OverwatchConsoleComponent> ent, ref CMUShipWeaponsRefreshEvent args)
    {
        UpdateOverwatch(ent);
    }

    private CMUShipWeaponStatus[] GetWeapons(EntityUid ship)
    {
        return TryComp(ship, out CMUShipWeaponsComponent? state)
            ? state.Weapons.Select(w => new CMUShipWeaponStatus(w, state.RemainingUses, state.MaxUses)).ToArray()
            : [];
    }

    private void UpdateTerminal(EntityUid terminal)
    {
        var available = TryGetShip(terminal, out var ship);
        var weapons = available
            ? (_requireGuns ? GetSupportedWeapons(ship).ToArray() :
                _prototypes.EnumeratePrototypes<CMUShipWeaponPrototype>().Select(w => new ProtoId<CMUShipWeaponPrototype>(w.ID)).ToArray())
            : [];
        var terminalComp = Comp<CMUShipWeaponsTerminalComponent>(terminal);
        _ui.SetUiState(terminal, CMUShipWeaponsUiKey.Key,
            new CMUShipWeaponsBuiState(available, available ? GetWeapons(ship) : [], weapons, _requireGuns,
                terminalComp.MaxSelections, Math.Max(0, terminalComp.Uses),
                available && TryComp(ship, out CMUShipWeaponsComponent? state) && state.Locked));
    }

    private void UpdateOverwatch(Entity<OverwatchConsoleComponent> console)
    {
        var weapons = TryGetShip(console, out var ship) ? GetWeapons(ship) : [];
        var selected = console.Comp.CMUSelectedShipWeapon;
        // Once the ship has fire missions they replace the standard cannon, so the console always has one picked.
        if (selected == null || !weapons.Any(w => w.Weapon == selected))
            selected = weapons.Length > 0 ? weapons[0].Weapon : (ProtoId<CMUShipWeaponPrototype>?) null;

        var next = TryComp(ship, out CMUShipWeaponsComponent? state) ? state.NextLaunch : TimeSpan.Zero;
        _overwatch.CMUSetShipWeapons(console, weapons, selected, console.Comp.CMUShipWeaponBearing, next);
    }

    private void UpdateShip(EntityUid ship)
    {
        var terminals = EntityQueryEnumerator<CMUShipWeaponsTerminalComponent>();
        while (terminals.MoveNext(out var uid, out _))
        {
            if (TryGetShip(uid, out var owner) && owner == ship)
                UpdateTerminal(uid);
        }

        var consoles = EntityQueryEnumerator<OverwatchConsoleComponent>();
        while (consoles.MoveNext(out var uid, out var console))
        {
            if (TryGetShip(uid, out var owner) && owner == ship)
                UpdateOverwatch((uid, console));
        }
    }

    private void Reject(EntityUid user, string message)
    {
        _popup.PopupCursor(Loc.GetString(message), user, PopupType.LargeCaution);
    }

    private void OnArm(Entity<CMUShipWeaponsTerminalComponent> ent, ref CMUShipWeaponsArmBuiMsg args)
    {
        if (!_access.IsAllowed(args.Actor, ent))
        {
            Reject(args.Actor, "cmu-ship-weapons-access-denied");
            return;
        }

        if (!TryGetShip(ent, out var ship))
        {
            Reject(args.Actor, "cmu-ship-weapons-no-ship");
            return;
        }

        if (TryComp(ship, out CMUShipWeaponsComponent? existing) && existing.Locked)
        {
            Reject(args.Actor, "cmu-ship-weapons-locked");
            UpdateTerminal(ent);
            return;
        }

        if (args.Weapons.Length < 1 || args.Weapons.Length > ent.Comp.MaxSelections ||
            args.Weapons.Distinct().Count() != args.Weapons.Length ||
            args.Weapons.Any(w => !_prototypes.HasIndex(w)))
        {
            _popup.PopupCursor(Loc.GetString("cmu-ship-weapons-invalid-selection",
                ("max", ent.Comp.MaxSelections)), args.Actor, PopupType.LargeCaution);
            return;
        }

        if (_requireGuns)
        {
            var supported = GetSupportedWeapons(ship);
            if (args.Weapons.Any(w => !supported.Contains(w)))
            {
                Reject(args.Actor, "cmu-ship-weapons-missing-gun");
                UpdateTerminal(ent);
                return;
            }
        }

        var state = EnsureComp<CMUShipWeaponsComponent>(ship);
        state.Locked = true;
        state.Weapons.UnionWith(args.Weapons);
        // Added on top of any missions bonus disks have already brought in.
        var uses = Math.Max(0, ent.Comp.Uses);
        state.MaxUses += uses;
        state.RemainingUses += uses;
        _adminLog.Add(LogType.Action, LogImpact.High,
            $"{ToPrettyString(args.Actor)} permanently armed {string.Join(", ", args.Weapons)} on ship {ToPrettyString(ship)} with {state.MaxUses} shared uses");

        UpdateShip(ship);
    }

    private void OnSelect(Entity<OverwatchConsoleComponent> ent, ref CMUShipWeaponSelectBuiMsg args)
    {
        if (!_access.IsAllowed(args.Actor, ent))
        {
            Reject(args.Actor, "cmu-ship-weapons-access-denied");
            return;
        }

        if (!Enum.IsDefined(args.Bearing) ||
            args.Weapon is { } weapon &&
            (!TryGetShip(ent, out var ship) ||
             !TryComp(ship, out CMUShipWeaponsComponent? state) ||
             !state.Weapons.Contains(weapon)))
        {
            Reject(args.Actor, "cmu-ship-weapons-not-armed");
            return;
        }

        _overwatch.CMUSetShipWeapons(ent, ent.Comp.CMUShipWeapons, args.Weapon, args.Bearing,
            ent.Comp.CMUNextShipWeaponLaunch);
        UpdateOverwatch(ent);
    }

    private void OnLaunch(Entity<OverwatchConsoleComponent> ent, ref CMUShipWeaponLaunchEvent args)
    {
        if (!_access.IsAllowed(args.User, ent))
        {
            Reject(args.User, "cmu-ship-weapons-access-denied");
            return;
        }

        if (!ent.Comp.CanOrbitalBombardment ||
            ent.Comp.CMUSelectedShipWeapon is not { } weapon ||
            !TryGetShip(ent, out var ship) ||
            !TryComp(ship, out CMUShipWeaponsComponent? state) ||
            !state.Weapons.Contains(weapon))
        {
            Reject(args.User, "cmu-ship-weapons-not-armed");
            return;
        }

        if (state.RemainingUses <= 0)
        {
            Reject(args.User, "cmu-ship-weapons-depleted");
            return;
        }

        if (_timing.CurTime < state.NextLaunch)
        {
            Reject(args.User, "cmu-ship-weapons-cooldown");
            return;
        }

        if (!_planet.TryPlanetToCoordinates(ent.Comp.OrbitalCoordinates, out var selected) ||
            !TryResolveImpact(selected, out var impact) ||
            !TryGetGroundCoordinates(impact, out var impactCoordinates))
        {
            Reject(args.User, "cmu-ship-weapons-invalid-target");
            return;
        }

        var profile = _prototypes.Index(weapon);
        Entity<CMUShipWeaponGunComponent> firingGun = default;
        if (_requireGuns && !TryGetFiringGun(ship, weapon, out firingGun))
        {
            Reject(args.User, "cmu-ship-weapons-no-loaded-gun");
            return;
        }

        StartStrike(selected, weapon, ent.Comp.CMUShipWeaponBearing, false);
        state.RemainingUses--;
        state.NextLaunch = _timing.CurTime + state.Cooldown;
        if (_requireGuns)
        {
            _guns.Spend(firingGun);
            _audio.PlayPvs(new SoundPathSpecifier("/Audio/_RMC14/Weapons/Vehicles/smokelauncher_fire.ogg"), firingGun);
        }

        if (ent.Comp.Squad is { } squad)
            _marineAnnounce.AnnounceSquad(Loc.GetString("cmu-ship-weapons-squad-warning",
                ("weapon", Loc.GetString(profile.Name))), GetEntity(squad));

        _popup.PopupCursor(Loc.GetString("cmu-ship-weapons-launched", ("weapon", Loc.GetString(profile.Name))),
            args.User, PopupType.Large);
        _adminLog.Add(LogType.RMCOrbitalBombardment, LogImpact.High,
            $"{ToPrettyString(args.User)} launched {weapon} from ship {ToPrettyString(ship)} at {ent.Comp.OrbitalCoordinates}; {state.RemainingUses} shared uses remain");
        UpdateShip(ship);
    }

    /// <summary>
    ///     Puts a strike in the air over <paramref name="target"/>: the laser, the alert and the launch warning, then
    ///     the first impact 24 seconds later. <paramref name="instant"/> skips the flight and the warnings, for testing.
    /// </summary>
    public bool StartStrike(MapCoordinates target, ProtoId<CMUShipWeaponPrototype> weapon, CMUShipWeaponBearing bearing,
        bool instant)
    {
        if (!TryResolveImpact(target, out var impact) || !TryGetGroundCoordinates(impact, out var impactCoordinates))
            return false;

        var strike = Spawn("CMUShipWeaponStrike", _transform.ToCoordinates(target));
        var firing = EnsureComp<CMUShipWeaponStrikeComponent>(strike);
        firing.Weapon = weapon;
        firing.Bearing = bearing;
        if (instant)
        {
            firing.NextShot = _timing.CurTime;
            firing.DirectionalWarned = firing.Warned = firing.TravelPlayed = true;
            return true;
        }

        firing.NextShot = _timing.CurTime + TimeSpan.FromSeconds(24);
        firing.DirectionalWarningAt = _timing.CurTime + TimeSpan.FromSeconds(3);
        Spawn("RMCLaserDropshipTarget", impactCoordinates);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/_RMC14/Effects/ob_alert.ogg"), impactCoordinates);
        _mortar.PopupWarning(impact, 30, "cmu-ship-weapons-incoming", "cmu-ship-weapons-incoming", true);
        return true;
    }

    private bool TryResolveImpact(MapCoordinates selected, out MapCoordinates impact)
    {
        impact = default;
        if (!TryGetGroundCoordinates(selected, out _) ||
            !_ordnance.TryResolveImpactColumn(selected, CMUTopDownOrdnanceKind.OrbitalBombardment, out var column) ||
            column.TerminalImpact is not { } terminal)
        {
            return false;
        }

        impact = terminal.Coordinates;
        return true;
    }

    private bool TryGetGroundCoordinates(MapCoordinates target, out EntityCoordinates coordinates)
    {
        coordinates = default;
        if (!_map.TryFindGridAt(target, out var grid, out _))
            return false;

        coordinates = _transform.ToCoordinates(grid, target);
        return _rmcMap.TryGetTileDef(coordinates, out var tile) && tile.ID != ContentTileDefinition.SpaceID;
    }

    public static Vector2 WalkingOffset(CMUShipWeaponPrototype profile, int shot, CMUShipWeaponBearing bearing)
    {
        var direction = bearing switch
        {
            CMUShipWeaponBearing.North => Vector2.UnitY,
            CMUShipWeaponBearing.East => Vector2.UnitX,
            CMUShipWeaponBearing.South => -Vector2.UnitY,
            CMUShipWeaponBearing.West => -Vector2.UnitX,
            _ => throw new ArgumentOutOfRangeException(nameof(bearing), bearing, null),
        };
        var fraction = profile.Shots <= 1 ? 0.5f : (float) shot / (profile.Shots - 1);
        return direction * ((fraction - 0.5f) * profile.WalkingDistance);
    }

    public override void Update(float frameTime)
    {
        if (_requireGuns && _timing.CurTime >= _nextTerminalRefresh)
        {
            _nextTerminalRefresh = _timing.CurTime + TimeSpan.FromSeconds(1);
            RefreshTerminals();
        }

        var strikes = EntityQueryEnumerator<CMUShipWeaponStrikeComponent>();
        while (strikes.MoveNext(out var uid, out var strike))
        {
            if (!strike.DirectionalWarned && _timing.CurTime >= strike.DirectionalWarningAt)
            {
                strike.DirectionalWarned = true;
                if (TryResolveImpact(_transform.GetMapCoordinates(uid), out var warning))
                {
                    _mortar.PopupWarning(warning, Math.Max(30, _prototypes.Index(strike.Weapon).FootprintRadius),
                        "cmu-ship-weapons-directional", "cmu-ship-weapons-directional-above", true);
                }
            }

            // The incoming whistle runs right up to the first impact, the way the standard orbital cannon times it.
            if (!strike.TravelPlayed && strike.Shot == 0 && _timing.CurTime >= strike.NextShot - TravelSoundLength)
            {
                strike.TravelPlayed = true;
                if (TryResolveImpact(_transform.GetMapCoordinates(uid), out var travel))
                {
                    _audio.PlayPvs(TravelSound, _transform.ToCoordinates(travel), AudioParams.Default.WithMaxDistance(75));
                }
            }

            if (!strike.Warned && _timing.CurTime >= strike.NextShot - TimeSpan.FromSeconds(4))
            {
                strike.Warned = true;
                if (TryResolveImpact(_transform.GetMapCoordinates(uid), out var warning))
                    _mortar.PopupWarning(warning, 20, "rmc-ob-warning-three", "rmc-ob-warning-above-three", true);
            }

            if (_timing.CurTime < strike.NextShot)
                continue;

            var profile = _prototypes.Index(strike.Weapon);
            var offset = WalkingOffset(profile, strike.Shot, strike.Bearing);
            if (profile.Spread > 0)
                offset += _random.NextVector2(profile.Spread);

            var target = _transform.GetMapCoordinates(uid).Offset(offset);
            if (TryResolveImpact(target, out var impact))
                Impact(uid, profile, impact);
            else
                Log.Info($"Ship weapon {strike.Weapon} shell {strike.Shot + 1} intercepted or out of bounds at {target}.");

            strike.Shot++;
            strike.NextShot = _timing.CurTime + profile.Interval;
            if (strike.Shot >= profile.Shots)
                QueueDel(uid);
        }

        UpdateEmsFields();
    }

    private void Impact(EntityUid source, CMUShipWeaponPrototype profile, MapCoordinates impact)
    {
        if (profile.RailDamage is { } damage)
            RailImpact(source, damage, impact);

        if (profile.BlastRadius > 0)
        {
            var total = _explosion.RadiusToIntensity(profile.BlastRadius, profile.Slope, profile.MaxIntensity);
            _rmcExplosion.QueueExplosion(impact, profile.Airburst ? "RMC" : "RMCOBXenoTunnel",
                total, profile.Slope, profile.MaxIntensity, source,
                tileBreakScale: profile.Airburst ? 0 : 1, maxTileBreak: profile.Airburst ? 0 : int.MaxValue,
                canCreateVacuum: false);
            var effect = Spawn("RMCExplosionEffectClusterOB", _transform.ToCoordinates(impact));
            _rmcExplosion.TryDoEffect(effect);
        }

        if (profile.Cloud is { } cloud)
        {
            for (var x = -profile.CloudRadius; x <= profile.CloudRadius; x++)
            for (var y = -profile.CloudRadius; y <= profile.CloudRadius; y++)
            {
                if (x * x + y * y > profile.CloudRadius * profile.CloudRadius)
                    continue;

                var cell = impact.Offset(new Vector2(x, y));
                if (!TryGetGroundCoordinates(cell, out var coordinates) ||
                    !_area.CanOrbitalBombard(coordinates, out _))
                    continue;

                Spawn(cloud, coordinates);
            }
        }

        if (profile.Ems)
        {
            var field = Spawn("CMUShipWeaponEmsField", _transform.ToCoordinates(impact));
            var ems = Comp<CMUShipWeaponEmsFieldComponent>(field);
            ems.Radius = profile.CloudRadius;
            ems.EndsAt = _timing.CurTime + profile.FieldDuration;
            ems.StunAfterField = profile.StunAfterField;
        }
    }
}
