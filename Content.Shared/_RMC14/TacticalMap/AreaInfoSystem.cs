using Content.Shared.CMU14.ZLevels.Ordnance;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.CCVar;
using Content.Shared.Alert;
using Content.Shared.Coordinates;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Profiling;
using Robust.Shared.Timing;
using Content.Shared.Timing;

namespace Content.Shared._RMC14.TacticalMap;

public sealed partial class AreaInfoSystem : EntitySystem
{
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private InventorySystem _inv = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private AreaSystem _area = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private CMUTopDownOrdnanceSystem _topDownOrdnance = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private ProfManager _prof = default!;

    private readonly Queue<Entity<AreaInfoComponent>> _marineAlertCopyQueue = new();
    private readonly DeadlineQueue<Entity<AreaInfoComponent>> _refreshDeadlines = new();

    private TimeSpan _maxProcessTime;

    public override void Initialize()
    {
        SubscribeLocalEvent<GrantAreaInfoComponent, GotEquippedEvent>(OnGotEquipped);
        SubscribeLocalEvent<GrantAreaInfoComponent, GotUnequippedEvent>(OnGotUnequipped);
        SubscribeLocalEvent<AreaInfoComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<AreaInfoComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<AreaInfoComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<AreaInfoComponent, ComponentRemove>(OnRemove);
        SubscribeLocalEvent<AreaInfoComponent, EntityPausedEvent>(OnPaused);
        SubscribeLocalEvent<AreaInfoComponent, EntityUnpausedEvent>(OnUnpaused);
        SubscribeLocalEvent<AreaInfoComponent, MoveEvent>(OnMoveEvent);

        Subs.CVar(_config, RMCCVars.RMCMaxTacmapAlertProcessTimeMilliseconds, v => _maxProcessTime = TimeSpan.FromMilliseconds(v), true);
    }

    private void OnGotEquipped(Entity<GrantAreaInfoComponent> ent, ref GotEquippedEvent args)
    {
        if (_timing.ApplyingState)
            return;

        if ((ent.Comp.Slots & args.SlotFlags) == 0)
            return;

        EnsureComp<AreaInfoComponent>(args.EquipTarget);
    }

    private void OnGotUnequipped(Entity<GrantAreaInfoComponent> ent, ref GotUnequippedEvent args)
    {
        if (_timing.ApplyingState)
            return;

        if ((ent.Comp.Slots & args.SlotFlags) == 0)
            return;
        if (!_inv.TryGetInventoryEntity<GrantAreaInfoComponent>(args.EquipTarget, out _))
            RemCompDeferred<AreaInfoComponent>(args.EquipTarget);
    }
    private void OnMapInit(Entity<AreaInfoComponent> ent, ref MapInitEvent args)
    {
        UpdateAreaInfoAlert(ent, false);
    }

    private void OnStartup(Entity<AreaInfoComponent> ent, ref ComponentStartup args)
    {
        ScheduleRefresh(ent);
    }

    private void OnShutdown(Entity<AreaInfoComponent> ent, ref ComponentShutdown args)
    {
        _refreshDeadlines.Remove(ent);
    }

    private void OnPaused(Entity<AreaInfoComponent> ent, ref EntityPausedEvent args)
    {
        _refreshDeadlines.Remove(ent);
    }

    private void OnUnpaused(Entity<AreaInfoComponent> ent, ref EntityUnpausedEvent args)
    {
        // This deadline was not pause-offset by the old polling loop. Overdue alerts
        // remain eligible immediately on unpause, without scheduling paused entities.
        ScheduleRefresh(ent);
    }

    /// <summary>
    /// Changes the next periodic refresh without scanning all area-info components.
    /// Like the original deadline field, this does not cancel a refresh already queued.
    /// </summary>
    public void SetNextUpdateTime(Entity<AreaInfoComponent> ent, TimeSpan deadline)
    {
        if (_net.IsClient || ent.Comp.Deleted)
            return;

        ent.Comp.NextUpdateTime = deadline;
        ScheduleRefresh(ent);
    }

    private void ScheduleRefresh(Entity<AreaInfoComponent> ent)
    {
        if (_net.IsServer && !ent.Comp.Deleted && !Paused(ent))
            _refreshDeadlines.Schedule(ent, ent.Comp.NextUpdateTime);
    }

    private void OnRemove(Entity<AreaInfoComponent> ent, ref ComponentRemove args)
    {
        _alerts.ClearAlert((ent.Owner, null), ent.Comp.Alert);
    }

    private void OnMoveEvent(Entity<AreaInfoComponent> ent, ref MoveEvent args)
    {
        if (_timing.ApplyingState)
            return;

        if (_prof.IsEnabled)
        {
            using var profile = _prof.Group("AreaInfoSystem.OnMoveEvent");
            UpdateAreaInfoAlert(ent, true);
            return;
        }

        // update the alert when they move to a new area
        UpdateAreaInfoAlert(ent, true);
    }

    private void UpdateAreaInfoAlert(Entity<AreaInfoComponent> ent, bool checkMove)
    {
        if (_prof.IsEnabled)
        {
            using var profile = _prof.Group("AreaInfoSystem.UpdateAlert");
            UpdateAreaInfoAlertCore(ent, checkMove);
            return;
        }

        UpdateAreaInfoAlertCore(ent, checkMove);
    }

    private void UpdateAreaInfoAlertCore(Entity<AreaInfoComponent> ent, bool checkMove)
    {
        if (GetAreaInfo(ent, checkMove) is not { areaName: var areaName, ceilingLevel: var ceilingLevel, restrictions: var restrictions })
            return;

        var presentation = (areaName, ceilingLevel, restrictions);
        if (ent.Comp.LastPresentation != presentation || ent.Comp.LastMessage == null)
        {
            ent.Comp.LastPresentation = presentation;
            ent.Comp.LastMessage = Loc.GetString("rmc-area-info",
                ("area", areaName), ("ceilingLevel", ceilingLevel), ("restrictions", restrictions));
        }

        _alerts.ShowAlert((ent.Owner, null), ent.Comp.Alert,
            severity: ceilingLevel, dynamicMessage: ent.Comp.LastMessage);
    }

    private (string areaName, short ceilingLevel, string restrictions)? GetAreaInfo(Entity<AreaInfoComponent> ent, bool checkMove)
    {
        if (!_prof.IsEnabled)
            return GetAreaInfoCore(ent, checkMove);

        using var profile = _prof.Group("AreaInfoSystem.GetAreaInfo");
        return GetAreaInfoCore(ent, checkMove);
    }

    private (string areaName, short ceilingLevel, string restrictions)? GetAreaInfoCore(Entity<AreaInfoComponent> ent, bool checkMove)
    {
        var time = _timing.CurTime;
        if (checkMove)
        {
            if (!AreaInfoUpdateThrottle.ShouldUpdate(time, ent.Comp.LastMoveUpdate, ent.Comp.LastMoveInterval))
                return null;

            ent.Comp.LastMoveUpdate = time;
        }

        var coordinates = ent.Owner.ToCoordinates();
        if (!_area.TryGetArea(coordinates, out var area, out var areaProto))
            return (Loc.GetString("rmc-tacmap-alert-no-area"), 0, string.Empty);

        short ceilingLevel = 0;
        short severityToUse = 0;
        var mapCoordinates = _transform.ToMapCoordinates(coordinates);
        var canOrbitalBombard = _topDownOrdnance.TryResolveImpactColumn(
            mapCoordinates,
            CMUTopDownOrdnanceKind.OrbitalBombardment,
            out var orbitalBombardment);
        var canMortarFire = _topDownOrdnance.TryResolveImpactColumn(
            mapCoordinates,
            CMUTopDownOrdnanceKind.Mortar,
            out var mortarFire);

        var (hasHiveCoreProtection, hasPylonProtection) = GetRoofingProtection(coordinates);
        var canCAS = _area.CanCAS(coordinates);
        var canSupplyDrop = _area.CanSupplyDrop(mapCoordinates);
        var canMortarPlacement = _area.CanMortarPlacement(coordinates);
        var canLase = _area.CanLase(coordinates);

        // Determine ceiling level based on effective protection (including roofing entities)
        // Note: severityToUse is offset by +1 because roofnull is at index 0 (for "no area" case)
        if (!canOrbitalBombard)
        {
            ceilingLevel = 4;
            severityToUse = hasHiveCoreProtection ? (short)7 : (short)5;
        }
        else if (!canCAS)
        {
            ceilingLevel = 3;
            severityToUse = hasPylonProtection ? (short)6 : (short)4;
        }
        else if (!canSupplyDrop || !canMortarFire)
        {
            ceilingLevel = 2;
            severityToUse = (short)3;
        }
        else if (!canMortarPlacement || !canLase || !_area.CanMedevac(coordinates) || !_area.CanParadrop(coordinates))
        {
            ceilingLevel = 1;
            severityToUse = (short)2;
        }
        else
        {
            ceilingLevel = 0;
            severityToUse = (short)1;
        }

        // Read permissions every time, but only rebuild text when its inputs change.
        var restrictionState = new AreaInfoRestrictionState(
            ceilingLevel, hasHiveCoreProtection, hasPylonProtection,
            canOrbitalBombard, orbitalBombardment is { Redirected: true }, canCAS, canSupplyDrop,
            canMortarFire, mortarFire is { Redirected: true }, canMortarPlacement, canLase,
            area.Value.Comp.Medevac, area.Value.Comp.Paradropping, area.Value.Comp.NoTunnel,
            area.Value.Comp.Unweedable, area.Value.Comp.ResinAllowed);
        if (ent.Comp.LastRestrictionState == restrictionState && ent.Comp.LastRestrictionText is { } cachedRestrictions)
            return (areaProto.Name, severityToUse, cachedRestrictions);

        // Build the restrictions string with clean formatting
        var allowedActions = new List<string>();
        var restrictedActions = new List<string>();

        if (canOrbitalBombard)
            allowedActions.Add(GetOrdnanceActionLabel("Orbital Strike", orbitalBombardment));
        else
            restrictedActions.Add("Orbital Strike");

        if (canCAS)
            allowedActions.Add("Close Air Support");
        else
            restrictedActions.Add("Close Air Support");

        if (canSupplyDrop)
            allowedActions.Add("Supply Drops");
        else
            restrictedActions.Add("Supply Drops");

        if (canMortarFire)
            allowedActions.Add(GetOrdnanceActionLabel("Mortar Fire", mortarFire));
        else
            restrictedActions.Add("Mortar Fire");

        if (canMortarPlacement)
            allowedActions.Add("Mortar Placement");
        else
            restrictedActions.Add("Mortar Placement");

        if (canLase)
            allowedActions.Add("Laser Designation");
        else
            restrictedActions.Add("Laser Designation");

        if (area.Value.Comp.Medevac)
            allowedActions.Add("Casualty Evacuation");
        else
            restrictedActions.Add("Casualty Evacuation");

        if (area.Value.Comp.Paradropping)
            allowedActions.Add("Paradropping");
        else
            restrictedActions.Add("Paradropping");

        // Add special restrictions
        if (area.Value.Comp.NoTunnel)
            restrictedActions.Add("Tunneling");
        if (area.Value.Comp.Unweedable)
            restrictedActions.Add("Weed Placement");
        else if (!area.Value.Comp.ResinAllowed)
            restrictedActions.Add("Resin Structures");

        var protectionSource = "";
        if (hasHiveCoreProtection)
            protectionSource = "\nProtection: Hive Core";
        else if (hasPylonProtection)
            protectionSource = "\nProtection: Hive Pylon";

        var restrictionsStr = $"\nCeiling level: {ceilingLevel}{protectionSource}";

        if (allowedActions.Count > 0)
        {
            restrictionsStr += "\n\nAllowed:";
            restrictionsStr += "\n• " + string.Join("\n• ", allowedActions);
        }

        if (restrictedActions.Count > 0)
        {
            restrictionsStr += "\n\nBlocked:";
            restrictionsStr += "\n• " + string.Join("\n• ", restrictedActions);
        }

        ent.Comp.LastRestrictionState = restrictionState;
        ent.Comp.LastRestrictionText = restrictionsStr;
        return (areaProto.Name, severityToUse, restrictionsStr);
    }

    private static string GetOrdnanceActionLabel(string label, CMUTopDownOrdnanceResult? result)
    {
        return result is { Redirected: true }
            ? $"{label} (top-down)"
            : label;
    }

    private (bool HiveCore, bool Pylon) GetRoofingProtection(EntityCoordinates coordinates)
    {
        var hiveCore = false;
        var pylon = false;
        var scanned = 0;
        var matched = 0;
        var roofs = EntityQueryEnumerator<RoofingEntityComponent>();
        while (roofs.MoveNext(out var uid, out var roof))
        {
            scanned++;
            var isCore = !hiveCore && !roof.CanOrbitalBombard && roof.Range > 10;
            var isPylon = !pylon && roof.CanOrbitalBombard && !roof.CanCAS && roof.Range < 10;
            if (!isCore && !isPylon) continue;
            matched++;
            if (!coordinates.TryDistance(_entityManager, uid.ToCoordinates(), out var distance) || !(distance <= roof.Range))
                continue;
            hiveCore |= isCore;
            pylon |= isPylon;
            if (hiveCore && pylon) break;
        }

        if (_prof.IsEnabled)
        {
            _prof.WriteValue("AreaInfoSystem Roofing Entities Scanned", scanned);
            _prof.WriteValue("AreaInfoSystem Roofing Predicate Matches", matched);
        }
        return (hiveCore, pylon);
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var time = _timing.CurTime;
        if (_marineAlertCopyQueue.Count > 0)
        {
            var budget = new TimeSliceBudget(_maxProcessTime, 128);
            while (_marineAlertCopyQueue.Count > 0)
            {
                if (!budget.TryConsume())
                    return;

                var ent = _marineAlertCopyQueue.Dequeue();
                if (TerminatingOrDeleted(ent) || ent.Comp.Deleted)
                    continue;

                UpdateAreaInfoAlert(ent, false);
            }
        }

        // Discover the entire due wave before rescheduling. Zero/negative intervals
        // must not repeatedly enqueue the same component during this update.
        while (_refreshDeadlines.TryTakeDue(time, out var ent))
        {
            if (ent.Comp.Deleted || TerminatingOrDeleted(ent) || Paused(ent))
                continue;

            _marineAlertCopyQueue.Enqueue(ent);
        }

        // The preceding wave is empty here. Preserve the existing next-tick refresh
        // and deadline-at-enqueue behavior, including when a wave spans many ticks.
        foreach (var ent in _marineAlertCopyQueue)
            SetNextUpdateTime(ent, time + ent.Comp.UpdateInterval);
    }

    public override void Shutdown()
    {
        _refreshDeadlines.Clear();
        _marineAlertCopyQueue.Clear();
        base.Shutdown();
    }
}
