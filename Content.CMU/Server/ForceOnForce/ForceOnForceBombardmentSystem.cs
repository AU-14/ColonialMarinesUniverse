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
    private readonly List<IncomingStrike> _incoming = new();

    private sealed class Barrage(string? enemy, int variant, TimeSpan next, EntityUid? previewTarget = null)
    {
        public readonly string? Enemy = enemy;
        public readonly int Variant = variant;
        public readonly EntityUid? PreviewTarget = previewTarget;
        public TimeSpan Next = next;
        public int Pass;
        public readonly Queue<EntityUid> Targets = new();
        public readonly Queue<int> Variations = new();
        public readonly List<Pulse> Pending = new();
    }

    private readonly record struct Pulse(EntityUid Target, TimeSpan At, Pattern Pattern, float Angle);
    private readonly record struct IncomingStrike(EntityUid Visual, EntityUid? Target, string? Enemy, Pattern Pattern);

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
        _incoming.Clear();
    }

    private void OnBombardment(Entity<MarineCommunicationsComputerComponent> console, ref ForceOnForceBombardmentMessage args)
    {
        var faction = _factions.GetFaction(args.Actor);
        if (_ticker.CurrentPreset?.ID.Equals("ForceOnForce", StringComparison.OrdinalIgnoreCase) != true ||
            args.Variant is < 0 or > 3 || !_factions.CanCommand(args.Actor) ||
            faction == null || !string.Equals(console.Comp.Faction, faction, StringComparison.OrdinalIgnoreCase)) return;
        if (_readyAt.TryGetValue(faction, out var ready) && _timing.CurTime < ready)
        {
            _popup.PopupEntity(Loc.GetString("cmu-fof-bombardment-cooldown",
                ("seconds", (int) Math.Ceiling((ready - _timing.CurTime).TotalSeconds))), console, args.Actor);
            return;
        }
        var settings = _prototypes.Index(Settings);
        var barrage = new Barrage(ForceOnForceSystem.Opponent(faction), args.Variant, _timing.CurTime + settings.Warning);
        QueueTargets(barrage);
        if (barrage.Targets.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("cmu-fof-bombardment-no-targets"), console, args.Actor);
            return;
        }
        _readyAt[faction] = _timing.CurTime + settings.Cooldown;
        _barrages.Add(barrage);
        PlayAlarm(settings);
    }

    /// <summary>Server scripting preview: runs the real sequence around an admin without faction targets.</summary>
    public bool ForcePreview(EntityUid viewer, int variant)
    {
        if (variant is < 0 or > 3 || TerminatingOrDeleted(viewer)) return false;
        var settings = _prototypes.Index(Settings);
        var barrage = new Barrage(null, variant, _timing.CurTime + settings.Warning, viewer);
        QueueTargets(barrage);
        _barrages.Add(barrage);
        PlayAlarm(settings, Transform(viewer).MapID);
        return true;
    }

    private void PlayAlarm(ForceOnForceBombardmentPrototype settings, MapId? previewMap = null)
    {
        _audio.PlayGlobal(settings.Siren, Filter.Broadcast(), true, AudioParams.Default.WithVolume(-8));
        _announce.AnnounceToMarines(Loc.GetString("cmu-fof-bombardment-warning"), filter: Filter.Broadcast());
        var query = EntityQueryEnumerator<ActorComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            var coordinates = _transform.GetMapCoordinates(uid);
            if (TryComp<MobStateComponent>(uid, out var mob) && mob.CurrentState == MobState.Dead ||
                !(previewMap == coordinates.MapId || _planet.TryGetPlanetSurfaceCoordinates(coordinates, out _))) continue;
            _popup.PopupEntity(Loc.GetString("cmu-fof-bombardment-popup"), uid, uid, PopupType.LargeCaution);
        }
    }

    private void QueueTargets(Barrage barrage)
    {
        if (barrage.PreviewTarget is { } viewer) { barrage.Targets.Enqueue(viewer); return; }
        var targets = new List<EntityUid>();
        var query = EntityQueryEnumerator<MarineComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var marine, out var mob))
            if (mob.CurrentState != MobState.Dead && marine.Faction?.Equals(barrage.Enemy, StringComparison.OrdinalIgnoreCase) == true &&
                _planet.TryGetPlanetSurfaceCoordinates(_transform.GetMapCoordinates(uid), out _)) targets.Add(uid);
        _random.Shuffle(targets);
        foreach (var target in targets) barrage.Targets.Enqueue(target);
    }

    private bool TryTarget(EntityUid target, string? enemy, out MapCoordinates origin)
    {
        origin = default;
        if (TerminatingOrDeleted(target)) return false;
        var coordinates = _transform.GetMapCoordinates(target);
        if (enemy == null) { origin = coordinates; return true; }
        return TryComp<MobStateComponent>(target, out var mob) && mob.CurrentState != MobState.Dead &&
            _factions.GetFaction(target) == enemy && _planet.TryGetPlanetSurfaceCoordinates(coordinates, out origin);
    }

    private Dictionary<MapId, List<Vector2>> GetOccupants()
    {
        var occupants = new Dictionary<MapId, List<Vector2>>();
        var mobs = EntityQueryEnumerator<MobStateComponent>();
        while (mobs.MoveNext(out var uid, out _))
        {
            var coordinates = _transform.GetMapCoordinates(uid);
            if (_planet.TryGetPlanetSurfaceCoordinates(coordinates, out var surface)) coordinates = surface;
            if (!occupants.TryGetValue(coordinates.MapId, out var positions)) occupants[coordinates.MapId] = positions = new();
            positions.Add(coordinates.Position);
        }
        return occupants;
    }

    public override void Update(float frameTime)
    {
        if (_barrages.Count == 0 && _incoming.Count == 0) return;
        var now = _timing.CurTime;
        if (!_barrages.Any(b => now >= b.Next) && !_incoming.Any(s =>
                !TryComp<ForceOnForceBombardmentVisualComponent>(s.Visual, out var v) || now >= v.ImpactAt)) return;
        var settings = _prototypes.Index(Settings);
        // Recheck every body at launch AND impact. Moving into a telegraphed site
        // cancels that impact instead of using the positions from the earlier pulse.
        var occupants = GetOccupants();
        var completed = 0;
        for (var i = _incoming.Count - 1; i >= 0 && completed < settings.EffectsPerInterval; i--)
        {
            var incoming = _incoming[i];
            if (!TryComp<ForceOnForceBombardmentVisualComponent>(incoming.Visual, out var visual)) { _incoming.RemoveAt(i); continue; }
            if (now < visual.ImpactAt) continue;
            _incoming.RemoveAt(i);
            completed++;
            var site = _transform.GetMapCoordinates(incoming.Visual);
            var valid = incoming.Target is not { } target || TryTarget(target, incoming.Enemy, out var origin) &&
                origin.MapId == site.MapId && Vector2.Distance(origin.Position, site.Position) <= settings.MaximumDistance;
            if (!valid || occupants.TryGetValue(site.MapId, out var bodies) &&
                !ForceOnForceBombardment.IsSafe(site.Position, bodies, settings.MinimumDistance))
            {
                QueueDel(incoming.Visual);
                continue;
            }
            ConfirmImpact(incoming, visual, settings);
        }
        for (var i = _barrages.Count - 1; i >= 0; i--)
        {
            var barrage = _barrages[i];
            if (now < barrage.Next) continue;
            if (barrage.Pending.Count == 0) Schedule(barrage, settings);
            var count = 0;
            while (barrage.Pending.Count > 0 && barrage.Pending[0].At <= now && count++ < settings.EffectsPerInterval)
            {
                var pulse = barrage.Pending[0];
                barrage.Pending.RemoveAt(0);
                BeginPulse(pulse, barrage.Enemy, occupants, settings);
            }
            if (barrage.Pending.Count > 0) barrage.Next = barrage.Pending[0].At;
            else if (barrage.Targets.Count > 0) barrage.Next = now + settings.Interval * _random.NextFloat(.65f, 1.4f);
            else if (++barrage.Pass >= settings.Passes) _barrages.RemoveAt(i);
            else
            {
                QueueTargets(barrage);
                barrage.Next = now + TimeSpan.FromSeconds(_random.NextFloat(.7f, 2.4f));
            }
        }
    }

    private void Schedule(Barrage barrage, ForceOnForceBombardmentPrototype settings)
    {
        var covered = new List<MapCoordinates>();
        for (var count = 0; count < settings.EffectsPerInterval && barrage.Targets.TryDequeue(out var target); count++)
        {
            if (!TryTarget(target, barrage.Enemy, out var origin) || covered.Any(c => c.MapId == origin.MapId &&
                    Vector2.DistanceSquared(c.Position, origin.Position) < settings.MaximumDistance * settings.MaximumDistance)) continue;
            covered.Add(origin);
            if (barrage.Variations.Count == 0)
            {
                var variations = new List<int> { 0, 1, 2, 3 };
                _random.Shuffle(variations);
                foreach (var variation in variations) barrage.Variations.Enqueue(variation);
            }
            var pattern = GetPattern(barrage.Variant, barrage.Variations.Dequeue());
            var angle = _random.NextFloat() * MathF.Tau;
            var at = _timing.CurTime + TimeSpan.FromSeconds(_random.NextFloat(0, .3f));
            for (var pulse = 0; pulse < pattern.Pulses; pulse++)
            {
                barrage.Pending.Add(new(target, at, pattern, angle + pulse * pattern.Sweep));
                at += TimeSpan.FromSeconds(pattern.Gap * _random.NextFloat(.65f, 1.4f));
            }
        }
        barrage.Pending.Sort((a, b) => a.At.CompareTo(b.At));
    }

    private void BeginPulse(Pulse pulse, string? enemy, Dictionary<MapId, List<Vector2>> occupants,
        ForceOnForceBombardmentPrototype settings)
    {
        if (!TryTarget(pulse.Target, enemy, out var origin)) return;
        occupants.TryGetValue(origin.MapId, out var bodies);
        for (var attempt = 0; attempt < 32; attempt++)
        {
            var angle = attempt < 8 ? pulse.Angle + _random.NextFloat(-.25f, .25f) : _random.NextFloat() * MathF.Tau;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var point = origin.Position + direction * _random.NextFloat(settings.MinimumDistance, settings.MaximumDistance);
            if (bodies != null && !ForceOnForceBombardment.IsSafe(point, bodies, settings.MinimumDistance)) continue;
            var coordinates = _transform.ToCoordinates(new MapCoordinates(point, origin.MapId));
            if (!_turf.TryGetTileRef(coordinates, out var tile) || tile.Value.Tile.IsEmpty) continue;
            SpawnIncoming(coordinates, direction, pulse.Pattern, pulse.Target, enemy);
            return;
        }
    }

    private void SpawnIncoming(EntityCoordinates coordinates, Vector2 direction, Pattern pattern, EntityUid? target, string? enemy)
    {
        var uid = Spawn("CMUFoFBombardmentImpact", coordinates);
        var visual = Comp<ForceOnForceBombardmentVisualComponent>(uid);
        visual.Variant = pattern.Variant;
        visual.Variation = pattern.Variation;
        visual.Direction = direction;
        visual.Scale = pattern.Scale;
        visual.StartedAt = _timing.CurTime;
        visual.ImpactAt = _timing.CurTime + TimeSpan.FromSeconds(pattern.LeadIn);
        Comp<TimedDespawnComponent>(uid).Lifetime = pattern.LeadIn + 8;
        Dirty(uid, visual);
        _incoming.Add(new(uid, target, enemy, pattern));
    }

    // Existing local scsi previews keep working and still recheck safety at impact.
    private void Present(EntityCoordinates coordinates, Vector2 direction, int variant, ForceOnForceBombardmentPrototype settings)
    {
        if (variant is < 0 or > 3) return;
        SpawnIncoming(coordinates, direction, GetPattern(variant, _random.Next(4)), null, null);
    }

    private void ConfirmImpact(IncomingStrike incoming, ForceOnForceBombardmentVisualComponent visual,
        ForceOnForceBombardmentPrototype settings)
    {
        var uid = incoming.Visual;
        visual.Impacted = true;
        visual.ImpactAt = _timing.CurTime;
        Dirty(uid, visual);
        var coordinates = Transform(uid).Coordinates;
        var strike = EnsureComp<FighterStrikeVisualComponent>(uid);
        strike.Kind = incoming.Pattern.Weapon;
        strike.Direction = visual.Direction;
        strike.Volleys = 1;
        strike.ImpactAt = _timing.CurTime;
        FighterEffects.AddGroundImpact(strike, Vector2.Zero, _timing.CurTime, false);
        Dirty(uid, strike);
        if (visual.Variant == 1)
        {
            var laser = Spawn("CMUFighterLaser", coordinates);
            var beam = Comp<FighterLaserComponent>(laser);
            beam.StartedAt = _timing.CurTime;
            beam.BeamColor = Color.FromHex(visual.Variation % 2 == 0 ? "#52DDFF" : "#A2C8FF");
            beam.ExpiresAt = _timing.CurTime + TimeSpan.FromSeconds(incoming.Pattern.BeamSeconds);
            _transform.SetWorldRotation(laser, new Angle(Math.Atan2(visual.Direction.Y, visual.Direction.X)));
            EnsureComp<TimedDespawnComponent>(laser).Lifetime = incoming.Pattern.BeamSeconds;
            Dirty(laser, beam);
            _fighterAudio.PlayGround(settings.Laser, coordinates, 30, -14);
        }
        _fighterAudio.PlayGround(settings.Impact, coordinates, 35, _random.NextFloat(-11, -6));
        _shake.ShakeCamera(Filter.Empty().AddInRange(_transform.ToMapCoordinates(coordinates), 18),
            visual.Variant == 3 ? 4 : 3, 1);
    }
}
