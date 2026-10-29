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

namespace Content.Server.CMU14.Light;

/// <summary>
///     Drives warship lighting by RMC alert level: fixtures tint on blue and red, dim to
///     half on delta, and hold a red ember on black. Rotating beacons join on every armed
///     level, sparse and slow on blue up to every fixture on black. Hull floodlights go
///     dark on red and black. Room fixtures keep their own power behavior.
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

    private bool _enabled = true;

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
        var query = EntityQueryEnumerator<T, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (!IsOnWarship(xform))
                continue;

            if (ship != null && !_zLevels.IsSameZNetwork(xform.MapUid, ship.Value))
                continue;

            ApplyAlertColor(uid, level);
        }
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

        marker.Beacon = Spawn(spec.Proto, Transform(uid).Coordinates);
        _transform.SetParent(marker.Beacon.Value, uid);
        marker.BeaconLevel = level;
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
