using System.Numerics;
using System.Linq;
using Content.Server.GameTicking;
using Content.Shared._RMC14.CameraShake;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Marines.Announce;
using Content.Shared._RMC14.Rules;
using Content.Shared.CMU14.Fighter;
using Content.Shared.CMU14.ForceOnForce;
using Content.Shared.GameTicking;
using Content.Shared.Maps;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.ForceOnForce;

/// <summary>Presentation only. No projectiles, explosions, damage, fire, or destructible terrain.</summary>
public sealed partial class ForceOnForceBombardmentSystem : EntitySystem
{
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private ForceOnForceSystem _factions = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedMarineAnnounceSystem _announce = default!;
    [Dependency] private RMCPlanetSystem _planet = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private FighterAudioSystem _fighterAudio = default!;
    [Dependency] private RMCCameraShakeSystem _shake = default!;

    private static readonly ProtoId<ForceOnForceBombardmentPrototype> Settings = "CMUFoFBombardment";
    private readonly Dictionary<string, TimeSpan> _readyAt = new();
    private readonly List<Barrage> _barrages = new();
    private readonly Dictionary<EntityUid, Vector2> _flybys = new();

    private sealed class Barrage(string enemy, int variant, TimeSpan next)
    {
        public readonly string Enemy = enemy;
        public readonly int Variant = variant;
        public TimeSpan Next = next;
        public int Pass;
        public readonly Queue<EntityUid> Targets = new();
    }

    public override void Initialize()
    {
        Subs.BuiEvents<MarineCommunicationsComputerComponent>(MarineCommunicationsComputerUI.Key,
            subs => subs.Event<ForceOnForceBombardmentMessage>(OnBombardment));
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRestart);
    }

    private void OnRestart(RoundRestartCleanupEvent args)
    {
        _readyAt.Clear();
        _barrages.Clear();
        _flybys.Clear();
    }

    private void OnBombardment(Entity<MarineCommunicationsComputerComponent> console, ref ForceOnForceBombardmentMessage args)
    {
        var faction = _factions.GetFaction(args.Actor);
        if (_ticker.CurrentPreset?.ID.Equals("ForceOnForce", StringComparison.OrdinalIgnoreCase) != true ||
            args.Variant is < 0 or > 3 || !_factions.CanCommand(args.Actor) ||
            faction == null || !string.Equals(console.Comp.Faction, faction, StringComparison.OrdinalIgnoreCase))
            return;

        if (_readyAt.TryGetValue(faction, out var ready) && _timing.CurTime < ready)
        {
            _popup.PopupEntity(Loc.GetString("cmu-fof-bombardment-cooldown",
                ("seconds", (int) Math.Ceiling((ready - _timing.CurTime).TotalSeconds))), console, args.Actor);
            return;
        }
        var settings = _prototypes.Index(Settings);
        var barrage = new Barrage(ForceOnForceSystem.Opponent(faction)!, args.Variant, _timing.CurTime + settings.Warning);
        QueueTargets(barrage);
        if (barrage.Targets.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("cmu-fof-bombardment-no-targets"), console, args.Actor);
            return;
        }

        _readyAt[faction] = _timing.CurTime + settings.Cooldown;
        _barrages.Add(barrage);
        _audio.PlayGlobal(settings.Siren, Filter.Broadcast(), true, AudioParams.Default.WithVolume(-8));
        _announce.AnnounceToMarines(Loc.GetString("cmu-fof-bombardment-warning"),
            filter: Filter.Broadcast());
    }

    private void QueueTargets(Barrage barrage)
    {
        var targets = new List<EntityUid>();
        var query = EntityQueryEnumerator<MarineComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var marine, out var mob))
        {
            if (mob.CurrentState != MobState.Dead &&
                string.Equals(marine.Faction, barrage.Enemy, StringComparison.OrdinalIgnoreCase) &&
                _planet.TryGetPlanetSurfaceCoordinates(_transform.GetMapCoordinates(uid), out _))
                targets.Add(uid);
        }
        _random.Shuffle(targets);
        foreach (var target in targets) barrage.Targets.Enqueue(target);
    }

    public override void Update(float frameTime)
    {
        foreach (var (flyby, velocity) in _flybys.ToArray())
        {
            if (TerminatingOrDeleted(flyby))
            {
                _flybys.Remove(flyby);
                continue;
            }
            _transform.SetCoordinates(flyby, Transform(flyby).Coordinates.Offset(velocity * frameTime));
        }
        if (_barrages.Count == 0) return;
        var settings = _prototypes.Index(Settings);
        for (var i = _barrages.Count - 1; i >= 0; i--)
        {
            var barrage = _barrages[i];
            if (_timing.CurTime < barrage.Next) continue;
            barrage.Next = _timing.CurTime + settings.Interval;

            // Re-read every body's position at execution time, including friendlies, neutrals,
            // unconscious units, and corpses. Never select against the target alone.
            var occupants = new Dictionary<MapId, List<Vector2>>();
            var mobs = EntityQueryEnumerator<MobStateComponent, TransformComponent>();
            while (mobs.MoveNext(out var uid, out _, out var xform))
            {
                if (!_planet.TryGetPlanetSurfaceCoordinates(_transform.GetMapCoordinates(uid), out var surface)) continue;
                if (!occupants.TryGetValue(surface.MapId, out var positions))
                    occupants[surface.MapId] = positions = new();
                positions.Add(surface.Position);
            }
            var sites = new Dictionary<MapId, List<Vector2>>();
            for (var count = 0; count < settings.EffectsPerInterval && barrage.Targets.TryDequeue(out var target); count++)
            {
                if (TerminatingOrDeleted(target) || !TryComp<MobStateComponent>(target, out var mob) ||
                    mob.CurrentState == MobState.Dead ||
                    _factions.GetFaction(target) != barrage.Enemy ||
                    !_planet.TryGetPlanetSurfaceCoordinates(_transform.GetMapCoordinates(target), out var origin)) continue;
                if (!occupants.TryGetValue(origin.MapId, out var positions)) continue;
                if (!sites.TryGetValue(origin.MapId, out var used)) sites[origin.MapId] = used = new();
                if (!ForceOnForceBombardment.IsSafe(origin.Position, used, settings.MaximumDistance)) continue;
                for (var attempt = 0; attempt < 32; attempt++)
                {
                    var angle = _random.NextFloat() * MathF.Tau;
                    var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                    var point = origin.Position + direction * _random.NextFloat(settings.MinimumDistance, settings.MaximumDistance);
                    if (!ForceOnForceBombardment.IsSafe(point, positions, settings.MinimumDistance)) continue;
                    var coordinates = _transform.ToCoordinates(new MapCoordinates(point, origin.MapId));
                    if (!_turf.TryGetTileRef(coordinates, out var tile) || tile.Value.Tile.IsEmpty) continue;
                    used.Add(point);
                    Present(coordinates, direction, (barrage.Variant + barrage.Pass) % 4, settings);
                    break;
                }
            }
            if (barrage.Targets.Count != 0) continue;
            if (++barrage.Pass >= settings.Passes)
                _barrages.RemoveAt(i);
            else
            {
                QueueTargets(barrage);
                barrage.Next += TimeSpan.FromSeconds(2);
            }
        }
    }

    private void Present(EntityCoordinates coordinates, Vector2 direction, int variant, ForceOnForceBombardmentPrototype settings)
    {
        var visual = Spawn("CMUFoFBombardmentImpact", coordinates);
        var strike = Comp<FighterStrikeVisualComponent>(visual);
        strike.Kind = variant == 0 ? FighterWeaponKind.Gau : variant == 1 ? FighterWeaponKind.Rockets : FighterWeaponKind.Missile;
        strike.Direction = direction;
        strike.Volleys = variant == 0 ? 6 : 1;
        strike.VolleyInterval = .12f;
        strike.ImpactAt = _timing.CurTime;
        FighterEffects.AddGroundImpact(strike, Vector2.Zero, _timing.CurTime, false);
        Dirty(visual, strike);

        var laser = Spawn("CMUFighterLaser", coordinates);
        var beam = Comp<FighterLaserComponent>(laser);
        beam.StartedAt = _timing.CurTime;
        beam.BeamColor = Color.FromHex(variant switch
        {
            0 => "#FF3636", 1 => "#52DDFF", 2 => "#FFB436", _ => "#CF65FF",
        });
        beam.ExpiresAt = _timing.CurTime + TimeSpan.FromSeconds(1 + variant * .3);
        _transform.SetWorldRotation(laser, new Angle(Math.Atan2(direction.Y, direction.X)));
        EnsureComp<TimedDespawnComponent>(laser).Lifetime = 2;
        Dirty(laser, beam);

        var burst = Spawn("CMUFighterAirBurst", coordinates);
        var effects = Comp<FighterEffectsComponent>(burst);
        FighterEffects.Add(effects, variant % 2 == 0 ? FighterEffectKind.Flares : FighterEffectKind.Hit,
            _timing.CurTime, direction: direction, duration: 3);
        Dirty(burst, effects);
        var flyby = Spawn("CMUFighterFlyby", coordinates.Offset(-direction * 28));
        _transform.SetWorldRotation(flyby, new Angle(Math.Atan2(direction.Y, direction.X) - Math.PI / 2));
        EnsureComp<TimedDespawnComponent>(flyby).Lifetime = 2;
        _flybys[flyby] = direction * (26 + variant * 5);
        _fighterAudio.PlayGround(settings.Flyby, coordinates, 45, -12);
        _fighterAudio.PlayGround(settings.Laser, coordinates, 30, -14);
        _fighterAudio.PlayGround(settings.Impact, coordinates, 35, -8);
        _shake.ShakeCamera(Filter.Empty().AddInRange(_transform.ToMapCoordinates(coordinates), 18), 3, 1);
    }
}
