using System.Linq;
using System.Numerics;
using Content.Server.CMU14.TacticalMap.Scout;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Rangefinder.Spotting;
using Content.Shared._RMC14.Scoping;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared._RMC14.Weapons.Ranged.Whitelist;
using Content.Shared.ActionBlocker;
using Content.Shared.CMU14.TacticalMap.Scout;
using Content.Shared.Examine;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.IdentityManagement;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._RMC14.TacticalMap;

public sealed partial class TacticalMapSystem
{
    [Dependency] private ActionBlockerSystem _scoutActionBlocker = default!;
    [Dependency] private ExamineSystemShared _scoutExamine = default!;
    [Dependency] private SharedMapSystem _scoutMaps = default!;
    [Dependency] private MetaDataSystem _scoutMetadata = default!;
    [Dependency] private IPrototypeManager _scoutPrototypes = default!;

    private static readonly ProtoId<CMUScoutReportSettingsPrototype> ScoutReportSettings = "CMUScoutReports";
    private static readonly EntProtoId ScoutInfantryAction = "CMUActionReportInfantry";
    private static readonly EntProtoId ScoutVehicleAction = "CMUActionReportVehicle";
    private static readonly EntProtoId ScoutEmplacementAction = "CMUActionReportEmplacement";
    private static readonly EntProtoId ScoutUnknownAction = "CMUActionReportUnknown";
    private static readonly SpriteSpecifier.Rsi ScoutReportIcon =
        new(new ResPath("/Textures/_RMC14/Interface/map_blips.rsi"), "enemy_blip");

    private void InitializeScoutReports()
    {
        SubscribeLocalEvent<MarineComponent, ScopedEvent>(OnScoutScoped);
        SubscribeLocalEvent<ScopingComponent, ComponentShutdown>(OnScoutUnscoped);
        SubscribeLocalEvent<CMUScoutOpticsComponent, CMUScoutReportActionEvent>(OnScoutReport);
        SubscribeLocalEvent<CMUScoutReporterComponent, ComponentShutdown>(OnScoutReporterShutdown);
    }

    private bool HasScoutTraining(EntityUid user, CMUScoutOpticsComponent optics) =>
        optics.RequiresScoutTraining
            ? HasComp<ScoutWhitelistComponent>(user)
            : HasComp<SpotterWhitelistComponent>(user);

    private void OnScoutScoped(Entity<MarineComponent> ent, ref ScopedEvent args)
    {
        if (!TryComp(args.Scope, out CMUScoutOpticsComponent? optics))
            return;

        if (!HasScoutTraining(ent, optics))
        {
            ScoutReportPopup(ent, "cmu-scout-report-untrained");
            return;
        }

        _actions.AddAction(ent, ref optics.InfantryAction, ScoutInfantryAction, args.Scope);
        _actions.AddAction(ent, ref optics.VehicleAction, ScoutVehicleAction, args.Scope);
        _actions.AddAction(ent, ref optics.EmplacementAction, ScoutEmplacementAction, args.Scope);
        _actions.AddAction(ent, ref optics.UnknownAction, ScoutUnknownAction, args.Scope);
        UpdateScoutActions(optics, _scoutPrototypes.Index(ScoutReportSettings),
            TryComp(ent, out CMUScoutReporterComponent? reporter) ? reporter.NextReport : TimeSpan.Zero);
    }

    private void UpdateScoutActions(CMUScoutOpticsComponent optics, CMUScoutReportSettingsPrototype settings,
        TimeSpan nextReport)
    {
        var description = Loc.GetString("cmu-scout-report-action-description",
            ("lifetime", settings.Lifetime.TotalSeconds),
            ("cooldown", settings.Cooldown.TotalSeconds),
            ("limit", settings.MaxActiveReports),
            ("range", settings.ReportRange));
        foreach (var action in new[] { optics.InfantryAction, optics.VehicleAction, optics.EmplacementAction, optics.UnknownAction })
        {
            _actions.SetUseDelay(action, settings.Cooldown);
            if (nextReport > _timing.CurTime)
                _actions.SetCooldown(action, nextReport - settings.Cooldown, nextReport);
            else
                _actions.RemoveCooldown(action);

            if (action is { } uid)
                _scoutMetadata.SetEntityDescription(uid, description);
        }
    }

    private void OnScoutUnscoped(Entity<ScopingComponent> ent, ref ComponentShutdown args)
    {
        if (!TryComp(ent.Comp.Scope, out CMUScoutOpticsComponent? optics))
            return;

        foreach (var action in new[] { optics.InfantryAction, optics.VehicleAction, optics.EmplacementAction, optics.UnknownAction })
        {
            if (_actions.GetAction(action) is { } attached && attached.Comp.AttachedEntity == ent.Owner)
                _actions.RemoveAction(action);
        }
    }

    private void OnScoutReport(Entity<CMUScoutOpticsComponent> ent, ref CMUScoutReportActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = TryReportScoutContact(ent, args.Performer, args.Target, args.ContactType);
    }

    public bool TryReportScoutContact(Entity<CMUScoutOpticsComponent> optics, EntityUid user,
        EntityCoordinates target, CMUScoutContactType contactType)
    {
        if (!HasScoutTraining(user, optics.Comp))
            return ScoutReportPopup(user, "cmu-scout-report-untrained");

        if (!TryComp(user, out ScopingComponent? scoping) || scoping.Scope != optics.Owner ||
            !TryComp(optics, out ScopeComponent? scope) || scope.User != user ||
            !_scoutActionBlocker.CanInteract(user, optics))
            return ScoutReportPopup(user, "cmu-scout-report-not-scoped");

        if (!TryComp(user, out MarineComponent? marine) ||
            !TryNormalizeHumanFaction(marine.Faction, out var faction))
            return ScoutReportPopup(user, "cmu-scout-report-no-faction");

        if (contactType is < CMUScoutContactType.Unknown or > CMUScoutContactType.Emplacement ||
            !target.IsValid(EntityManager) || !float.IsFinite(target.X) || !float.IsFinite(target.Y))
            return ScoutReportPopup(user, "cmu-scout-report-invalid-position");

        var settings = _scoutPrototypes.Index(ScoutReportSettings);
        var reporter = EnsureComp<CMUScoutReporterComponent>(user);
        if (_timing.CurTime < reporter.NextReport)
            return ScoutReportPopup(user, "cmu-scout-report-cooldown", ("cooldown", settings.Cooldown.TotalSeconds));

        var origin = _transform.GetMapCoordinates(user);
        var observed = _transform.ToMapCoordinates(target);
        var zoom = scope.ZoomLevels[scope.CurrentZoomLevel].Zoom;
        var viewCenter = origin.Position + scoping.EyeOffset;
        // The examine helper's range tolerance must not extend the configured reporting limit.
        if (TryComp(user, out BlindableComponent? blind) && blind.IsBlind ||
            origin.MapId != observed.MapId ||
            Vector2.Distance(origin.Position, observed.Position) > settings.ReportRange ||
            Vector2.Distance(viewCenter, observed.Position) > ExamineSystemShared.ExamineRange * zoom ||
            !_scoutExamine.InRangeUnOccluded(origin, observed, settings.ReportRange, uid => uid == user))
            return ScoutReportPopup(user, "cmu-scout-report-not-visible");

        var gridId = _transform.GetGrid(target);
        if (!TryComp(gridId, out MapGridComponent? grid) ||
            !TryGetTrackingMap(gridId.Value, out var map))
            return ScoutReportPopup(user, "cmu-scout-report-invalid-position");

        // Resolve the clicked location once. Never retain the target entity or its coordinates.
        var indices = _scoutMaps.WorldToTile(gridId.Value, grid, observed.Position);
        ExpireScoutReports(reporter);
        while (reporter.Reports.Count >= settings.MaxActiveReports)
            RemoveScoutReport(reporter, 0);

        var time = _timing.CurTime;
        var blip = new TacticalMapBlip(indices, ScoutReportIcon, Color.Orange,
            TacticalMapBlipStatus.Alive, null, false)
        {
            ScoutReport = new CMUScoutReportInfo(Identity.Name(user, EntityManager), contactType,
                time, time + settings.Lifetime),
        };
        var key = _nextIntelBlipKey--;
        reporter.Reports.Add(new CMUScoutReport(key, map, gridId.Value, faction, blip));
        reporter.NextReport = time + settings.Cooldown;
        UpdateScoutActions(optics.Comp, settings, reporter.NextReport);
        _intelBlipGrids[key] = gridId.Value;
        InvalidateScoutReportMap(map, faction);
        ScoutReportPopup(user, "cmu-scout-report-sent");
        TransmitScoutReport(user, faction, contactType, observed, settings);
        return true;
    }

    private bool ScoutReportPopup(EntityUid user, string message, params (string, object)[] args)
    {
        _popup.PopupEntity(Loc.GetString(message, args), user, user);
        return false;
    }

    private void OnScoutReporterShutdown(Entity<CMUScoutReporterComponent> ent, ref ComponentShutdown args)
    {
        for (var i = ent.Comp.Reports.Count - 1; i >= 0; i--)
            RemoveScoutReport(ent.Comp, i);
    }

    private void UpdateScoutReports()
    {
        var query = EntityQueryEnumerator<CMUScoutReporterComponent>();
        while (query.MoveNext(out var reporter))
            ExpireScoutReports(reporter);
    }

    private void ExpireScoutReports(CMUScoutReporterComponent reporter)
    {
        for (var i = reporter.Reports.Count - 1; i >= 0; i--)
        {
            var report = reporter.Reports[i];
            if (report.Blip.ScoutReport is not { } info || _timing.CurTime >= info.ExpiresAt ||
                TerminatingOrDeleted(report.Map) || TerminatingOrDeleted(report.Grid))
                RemoveScoutReport(reporter, i);
        }
    }

    private void RemoveScoutReport(CMUScoutReporterComponent reporter, int index)
    {
        var report = reporter.Reports[index];
        reporter.Reports.RemoveAt(index);
        _intelBlipGrids.Remove(report.Key);
        InvalidateScoutReportMap(report.Map, report.Faction);
    }

    private void InvalidateScoutReportMap(EntityUid map, string faction)
    {
        if (!TryComp(map, out TacticalMapComponent? tactical))
            return;

        tactical.MapDirty = true;
        tactical.NextUpdatePerFaction[faction] = TimeSpan.Zero;
    }

    private void AddScoutReports(Dictionary<int, TacticalMapBlip> blips, TacticalMapComponent map, string faction)
    {
        // Reports are recipient-only overlays, never inputs to sensors or published live snapshots.
        var query = EntityQueryEnumerator<CMUScoutReporterComponent>();
        while (query.MoveNext(out var reporter))
        {
            foreach (var report in reporter.Reports)
            {
                if (TryComp(report.Map, out TacticalMapComponent? reportMap) && reportMap == map &&
                    report.Faction == faction &&
                    report.Blip.ScoutReport is { } info && _timing.CurTime < info.ExpiresAt)
                    blips[report.Key] = report.Blip;
            }
        }
    }

    private void AddScoutReportsToUser(Entity<TacticalMapUserComponent> user, TacticalMapComponent map)
    {
        void Add(bool allowed, string faction, Dictionary<int, TacticalMapBlip> blips)
        {
            foreach (var key in blips.Where(pair => pair.Value.ScoutReport != null).Select(pair => pair.Key).ToArray())
                blips.Remove(key);
            if (allowed)
                AddScoutReports(blips, map, faction);
        }

        Add(user.Comp.Marines, MarinesFaction, user.Comp.MarineBlips);
        Add(user.Comp.Opfor, OpforFaction, user.Comp.OpforBlips);
        Add(user.Comp.Govfor, GovforFaction, user.Comp.GovforBlips);
        Add(user.Comp.Clf, ClfFaction, user.Comp.ClfBlips);
        Add(user.Comp.WeYu, WeYuFaction, user.Comp.WeYuBlips);
    }
}
