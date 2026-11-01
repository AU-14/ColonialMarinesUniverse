using Content.Server._RMC14.Light;
using Content.Shared._RMC14.AlertLevel;
using Content.Shared.CCVar;
using Content.Shared.CMU14.Marines;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Content.Shared.Light.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Light;

/// <summary>
///     Drives warship lighting by RMC alert level: fixtures tint on blue and red, dim to
///     half on delta, and hold a red ember on black. Rotating beacons join on every armed
///     level, sparse and slow on blue up to every fixture on black. Hull floodlights go
///     dark on red and black. Room fixtures keep their own power behavior.
///     Level changes sweep the ship south to north, each fixture dipping dark a beat
///     before the new mode lands.
/// </summary>
public sealed partial class CMUWarshipAlertLightsSystem : EntitySystem
{
    private static readonly Dictionary<RMCAlertLevels, Color> AlertColors = new()
    {
        [RMCAlertLevels.Blue] = Color.FromHex("#1150FF"),
        [RMCAlertLevels.Red] = Color.FromHex("#EE0500"),
        // [RMCAlertLevels.Yellow] = Color.FromHex("#FFFF00"), // TODO
        [RMCAlertLevels.Black] = Color.FromHex("#1A0505"),
    };

    private static readonly Color HalfBrightness = new(0.5f, 0.5f, 0.5f);

    // Beacon ladder: sparse and slow on blue, denser and faster as readiness climbs.
    // The stride spaces them along passageways and caps client light count.
    private static readonly Dictionary<RMCAlertLevels, (EntProtoId Proto, int Stride)> Beacons = new()
    {
        [RMCAlertLevels.Blue] = ("CMUWarshipAlertBeaconBlue", 8),
        [RMCAlertLevels.Red] = ("CMUWarshipAlertBeaconRed", 4),
        [RMCAlertLevels.Black] = ("CMUWarshipAlertBeaconBlack", 1),
        [RMCAlertLevels.Delta] = ("CMUWarshipAlertBeaconDelta", 6),
    };

    [Dependency] private PointLightSystem _pointLight = default!;
    [Dependency] private RMCAlertLevelSystem _alertLevel = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private CMUSharedZLevelsSystem _zLevels = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;

    private bool _enabled = true;

    private readonly List<(EntityUid Uid, EntProtoId Proto, RMCAlertLevels Level)> _pendingBeacons = new();
    private const int BeaconsPerTick = 20;

    // Alert changes sweep the ship south to north instead of landing in one tick:
    // every deck rolls into the new mode as its own wave.
    private readonly List<(EntityUid Uid, RMCAlertLevels? Level, float Y, float X)> _pendingLightChanges = new();
    private const int LightWavePerTick = 20;

    // Wave fixtures dip dark for a beat before the new mode lands, like a hall's
    // amps staging in on a current surge instead of an instant jump.
    private static readonly TimeSpan FlickerDuration = TimeSpan.FromSeconds(0.25);
    private readonly List<(EntityUid Uid, RMCAlertLevels? Level, bool WasEnabled, TimeSpan ResumeAt)> _pendingFlickers = new();

    public override void Initialize()
    {
        Subs.CVar(_cfg, CCVars.EnableWarshipAlertLights, OnCVarChanged);
        SubscribeLocalEvent<PoweredLightComponent, ComponentStartup>(OnLightStartup);
        SubscribeLocalEvent<RMCBreakLightOnAttackComponent, MapInitEvent>(OnRmcLightMapInit);
        SubscribeLocalEvent<CMUWarshipExteriorLightComponent, ComponentStartup>(OnExteriorLightStartup);
        SubscribeLocalEvent<RMCAlertLevelChangedEvent>(OnAlertChanged);
    }

    private void OnCVarChanged(bool value)
    {
        _enabled = value;

        if (!value)
        {
            ApplyToWarshipFixtures<PoweredLightComponent>(null, null);
            ApplyToWarshipFixtures<RMCBreakLightOnAttackComponent>(null, null);
            CullWarshipExteriorLights(null, null);
            return;
        }

        var alerts = EntityQueryEnumerator<RMCAlertLevelComponent>();
        while (alerts.MoveNext(out var uid, out var comp))
        {
            EntityUid? ship = HasComp<MapComponent>(uid) ? uid : null;
            ApplyToWarshipFixtures<PoweredLightComponent>(comp.Level, ship);
            ApplyToWarshipFixtures<RMCBreakLightOnAttackComponent>(comp.Level, ship);
            CullWarshipExteriorLights(comp.Level, ship);
        }
    }

    private void OnLightStartup(Entity<PoweredLightComponent> ent, ref ComponentStartup args)
    {
        if (!_enabled)
            return;

        ApplyAlertColor(ent);
    }

    private void OnRmcLightMapInit(Entity<RMCBreakLightOnAttackComponent> ent, ref MapInitEvent args)
    {
        if (!_enabled)
            return;

        ApplyAlertColor(ent);
    }

    private void OnAlertChanged(ref RMCAlertLevelChangedEvent args)
    {
        if (!_enabled)
            return;

        ApplyToWarshipFixtures<PoweredLightComponent>(args.Level, args.Ship);
        ApplyToWarshipFixtures<RMCBreakLightOnAttackComponent>(args.Level, args.Ship);
        CullWarshipExteriorLights(args.Level, args.Ship);
    }

    private void ApplyToWarshipFixtures<T>(RMCAlertLevels? level, EntityUid? ship) where T : IComponent
    {
        var batch = new List<(EntityUid Uid, RMCAlertLevels? Level, float Y, float X)>();
        var query = EntityQueryEnumerator<T, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (!IsOnWarship(xform))
                continue;

            if (ship != null && !_zLevels.IsSameZNetwork(xform.MapUid, ship.Value))
                continue;

            var pos = xform.WorldPosition;
            batch.Add((uid, level, pos.Y, pos.X));
        }

        // South first; a global Y-sort interleaves the decks so each sweeps at once.
        batch.Sort(static (a, b) =>
        {
            var cmp = a.Y.CompareTo(b.Y);
            return cmp != 0 ? cmp : a.X.CompareTo(b.X);
        });

        _pendingLightChanges.AddRange(batch);
    }

    private void ApplyAlertColor(EntityUid uid)
    {
        var xform = Transform(uid);
        if (!IsOnWarship(xform))
            return;

        ApplyAlertColor(uid, _alertLevel.Get(xform.MapUid));
    }

    private void OnExteriorLightStartup(Entity<CMUWarshipExteriorLightComponent> ent, ref ComponentStartup args)
    {
        if (!_enabled)
            return;

        var xform = Transform(ent);
        if (!IsOnWarship(xform))
            return;

        ApplyExteriorLightState(ent, _alertLevel.Get(xform.MapUid));
    }

    private void CullWarshipExteriorLights(RMCAlertLevels? level, EntityUid? ship)
    {
        var query = EntityQueryEnumerator<CMUWarshipExteriorLightComponent, PointLightComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var marker, out _, out var xform))
        {
            if (!IsOnWarship(xform))
                continue;

            if (ship != null && !_zLevels.IsSameZNetwork(xform.MapUid, ship.Value))
                continue;

            ApplyExteriorLightState((uid, marker), level);
        }
    }

    private void ApplyExteriorLightState(Entity<CMUWarshipExteriorLightComponent> ent, RMCAlertLevels? level)
    {
        if (!TryComp(ent, out PointLightComponent? light))
            return;

        if (level is RMCAlertLevels.Red or RMCAlertLevels.Black)
        {
            ent.Comp.Original ??= light.Color;
            ent.Comp.OriginalEnabled ??= light.Enabled;
            _pointLight.SetEnabled(ent, false, light);
        }
        else if (level == RMCAlertLevels.Delta)
        {
            ent.Comp.Original ??= light.Color;
            ent.Comp.OriginalEnabled ??= light.Enabled;
            _pointLight.SetEnabled(ent, ent.Comp.OriginalEnabled.Value, light);
            _pointLight.SetColor(ent, ent.Comp.Original.Value * HalfBrightness, light);
        }
        else if (ent.Comp.Original is { } original)
        {
            var enabled = ent.Comp.OriginalEnabled ?? true;
            ent.Comp.Original = null;
            ent.Comp.OriginalEnabled = null;
            _pointLight.SetColor(ent, original, light);
            _pointLight.SetEnabled(ent, enabled, light);
        }
    }

    private void ApplyAlertColor(EntityUid uid, RMCAlertLevels? level)
    {
        if (HasComp<CMUWarshipAlertLightExemptComponent>(uid))
            return;

        if (!TryComp(uid, out PointLightComponent? light))
            return;

        if (level is { } lvl && AlertColors.TryGetValue(lvl, out var color))
        {
            var marker = EnsureComp<CMUWarshipAlertLightComponent>(uid);
            marker.Original ??= light.Color;
            UpdateBeacon(uid, marker, lvl);
            _pointLight.SetColor(uid, color, light);
        }
        else if (level == RMCAlertLevels.Delta)
        {
            var marker = EnsureComp<CMUWarshipAlertLightComponent>(uid);
            marker.Original ??= light.Color;
            UpdateBeacon(uid, marker, RMCAlertLevels.Delta);
            _pointLight.SetColor(uid, marker.Original.Value * HalfBrightness, light);
        }
        else if (TryComp<CMUWarshipAlertLightComponent>(uid, out var marker) && marker.Original is { } original)
        {
            RemoveBeacon(marker);
            _pointLight.SetColor(uid, original, light);
        }
    }

    private void UpdateBeacon(EntityUid uid, CMUWarshipAlertLightComponent marker, RMCAlertLevels level)
    {
        if (!Beacons.TryGetValue(level, out var spec) || uid.Id % (uint) spec.Stride != 0)
        {
            RemoveBeacon(marker);
            return;
        }

        if (marker.Beacon is { } beacon
            && (marker.BeaconLevel != level || TerminatingOrDeleted(beacon)))
            RemoveBeacon(marker);

        if (marker.Beacon != null)
            return;

        // Beacon spawns queue up: Delta arms hundreds of fixtures and spawning
        // every stride-th beacon in one tick hitches the server.
        _pendingBeacons.Add((uid, spec.Proto, level));
    }

    public override void Update(float frameTime)
    {
        if (_pendingLightChanges.Count > 0)
        {
            var processed = Math.Min(LightWavePerTick, _pendingLightChanges.Count);
            for (var i = 0; i < processed; i++)
            {
                var (uid, level, _, _) = _pendingLightChanges[i];
                if (TerminatingOrDeleted(uid))
                    continue;

                // A newer level change queued behind this entry: let it carry the light.
                if (level is { } lvl
                    && _alertLevel.Get(Transform(uid).MapUid) != lvl)
                    continue;

                if (TryComp(uid, out PointLightComponent? light))
                {
                    // Remember whether it was lit: unpowered or broken fixtures must
                    // not come back on when the wave passes.
                    var wasEnabled = light.Enabled;
                    _pointLight.SetEnabled(uid, false, light);
                    _pendingFlickers.Add((uid, level, wasEnabled, _timing.CurTime + FlickerDuration));
                }
                else
                {
                    ApplyAlertColor(uid, level);
                }
            }

            _pendingLightChanges.RemoveRange(0, processed);
        }

        for (var i = _pendingFlickers.Count - 1; i >= 0; i--)
        {
            var (uid, level, wasEnabled, resumeAt) = _pendingFlickers[i];
            if (_timing.CurTime < resumeAt)
                continue;

            _pendingFlickers.RemoveAt(i);

            if (TerminatingOrDeleted(uid))
                continue;

            if (TryComp(uid, out PointLightComponent? light))
                _pointLight.SetEnabled(uid, wasEnabled, light);

            // A newer level change landed while dark: keep its color, just relight.
            if (level is { } lvl
                && _alertLevel.Get(Transform(uid).MapUid) != lvl)
                continue;

            ApplyAlertColor(uid, level);
        }

        if (_pendingBeacons.Count == 0)
            return;

        var processedBeacons = Math.Min(BeaconsPerTick, _pendingBeacons.Count);
        for (var i = 0; i < processedBeacons; i++)
        {
            var (uid, proto, level) = _pendingBeacons[i];
            if (!TryComp<CMUWarshipAlertLightComponent>(uid, out var marker)
                || marker.Beacon != null)
                continue;

            marker.Beacon = Spawn(proto, Transform(uid).Coordinates);
            _transform.SetParent(marker.Beacon.Value, uid);
            marker.BeaconLevel = level;
        }

        _pendingBeacons.RemoveRange(0, processedBeacons);
    }

    private void RemoveBeacon(CMUWarshipAlertLightComponent marker)
    {
        if (marker.Beacon is not { } beacon)
            return;

        marker.Beacon = null;
        marker.BeaconLevel = null;
        if (!TerminatingOrDeleted(beacon))
            Del(beacon);
    }

    private bool IsOnWarship(TransformComponent xform)
    {
        if (HasComp<WarshipComponent>(xform.MapUid)
            || (xform.GridUid is { } grid && HasComp<WarshipComponent>(grid)))
            return true;

        return xform.MapUid is { } map
            && _zLevels.TryGetZNetwork(map, out var network)
            && _zLevels.TryGetMapAtDepth(network.Value, 0, out var primary)
            && HasComp<RMCAlertLevelComponent>(primary);
    }
}
