using Content.Server._RMC14.Light;
using Content.Shared._RMC14.AlertLevel;
using Content.Shared.CMU14.Marines;
using Content.Shared.Light.Components;
using Robust.Server.GameObjects;

namespace Content.Server.CMU14.Light;

/// <summary>
///     Tints warship light fixtures to the RMC alert level's color.
///     Room fixtures keep their own power behavior and only the point light color is touched.
/// </summary>
public sealed partial class CMUWarshipAlertLightsSystem : EntitySystem
{
    // Alert palette. Green alert restores each fixture's original color.
    private static readonly Dictionary<RMCAlertLevels, Color> AlertColors = new()
    {
        [RMCAlertLevels.Blue] = Color.FromHex("#1150FF"),
        [RMCAlertLevels.Red] = Color.FromHex("#EE0500"),
        // [RMCAlertLevels.Yellow] = Color.FromHex("#FFFF00"), // CMU14 TODO
        [RMCAlertLevels.Delta] = Color.FromHex("#000000"), // no lights
    };

    [Dependency] private PointLightSystem _pointLight = default!;
    [Dependency] private RMCAlertLevelSystem _alertLevel = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<PoweredLightComponent, MapInitEvent>(OnLightMapInit);
        SubscribeLocalEvent<RMCBreakLightOnAttackComponent, MapInitEvent>(OnRmcLightMapInit);
        SubscribeLocalEvent<RMCAlertLevelChangedEvent>(OnAlertChanged);
    }

    private void OnLightMapInit(Entity<PoweredLightComponent> ent, ref MapInitEvent args)
        => ApplyAlertColor(ent);

    private void OnRmcLightMapInit(Entity<RMCBreakLightOnAttackComponent> ent, ref MapInitEvent args)
        => ApplyAlertColor(ent);

    private void OnAlertChanged(ref RMCAlertLevelChangedEvent args)
    {
        ApplyToWarshipFixtures<PoweredLightComponent>(args.Level);
        ApplyToWarshipFixtures<RMCBreakLightOnAttackComponent>(args.Level);
    }

    private void ApplyToWarshipFixtures<T>(RMCAlertLevels level) where T : IComponent
    {
        var query = EntityQueryEnumerator<T, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (!IsOnWarship(xform))
                continue;

            ApplyAlertColor(uid, level);
        }
    }

    private void ApplyAlertColor(EntityUid uid)
    {
        if (!IsOnWarship(Transform(uid)))
            return;

        ApplyAlertColor(uid, _alertLevel.Get());
    }

    private void ApplyAlertColor(EntityUid uid, RMCAlertLevels? level)
    {
        if (!TryComp(uid, out SharedPointLightComponent? light))
            return;

        if (level is { } lvl && AlertColors.TryGetValue(lvl, out var color))
        {
            var marker = EnsureComp<CMUWarshipAlertLightComponent>(uid);
            marker.Original ??= light.Color;
            _pointLight.SetColor(uid, color, light);
        }
        else if (TryComp<CMUWarshipAlertLightComponent>(uid, out var marker) && marker.Original is { } original)
        {
            _pointLight.SetColor(uid, original, light);
        }
    }

    private bool IsOnWarship(TransformComponent xform)
    {
        return HasComp<WarshipComponent>(xform.MapUid)
            || (xform.GridUid is { } grid && HasComp<WarshipComponent>(grid));
    }
}
