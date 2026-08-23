using System.Linq;
using Content.Server.Medical.CrewMonitoring;
using Content.Server.PowerCell;
using Content.Server.Station.Components;
using Content.Shared.Damage;
using Content.Shared.Medical.CrewMonitoring;
using Content.Shared.Medical.SuitSensor;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Pinpointer;
using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Areas;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._CMU14.Yautja;

public sealed partial class YautjaCrewMonitoringConsoleSystem : EntitySystem
{
    private static readonly string[] OxygenDamageTypes = ["Asphyxiation", "Bloodloss"];
    private static readonly string[] ToxinDamageTypes = ["Poison", "Radiation"];
    private static readonly string[] BurnDamageTypes = ["Heat", "Shock", "Cold", "Caustic"];
    private static readonly string[] BruteDamageTypes = ["Blunt", "Slash", "Piercing"];

    [Dependency] private IGameTiming _gameTiming = default!;
    [Dependency] private PowerCellSystem _cell = default!;
    [Dependency] private UserInterfaceSystem _uiSystem = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MobThresholdSystem _mobThresholds = default!;
    [Dependency] private AreaSystem _areas = default!;

    private TimeSpan _nextUpdate;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<YautjaCrewMonitoringConsoleComponent, BoundUIOpenedEvent>(OnUIOpened);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_gameTiming.CurTime < _nextUpdate)
            return;

        _nextUpdate = _gameTiming.CurTime + TimeSpan.FromSeconds(3);

        var query = EntityQueryEnumerator<YautjaCrewMonitoringConsoleComponent, CrewMonitoringConsoleComponent>();
        while (query.MoveNext(out var monitor, out _, out _))
            Refresh(monitor);
    }

    public void Refresh(EntityUid monitor, CrewMonitoringConsoleComponent? component = null)
    {
        if (!Resolve(monitor, ref component))
            return;

        component.ConnectedSensors.Clear();

        var yautja = EntityQueryEnumerator<YautjaComponent, TransformComponent>();
        while (yautja.MoveNext(out var target, out var yautjaComponent, out var transform))
        {
            if (!TryBuildStatus(target, yautjaComponent, transform, out var status))
                continue;

            component.ConnectedSensors[target.ToString()] = status;
        }

        UpdateUserInterface(monitor, component);
    }

    private void OnUIOpened(EntityUid uid, YautjaCrewMonitoringConsoleComponent component, BoundUIOpenedEvent args)
    {
        if (!_cell.TryUseActivatableCharge(uid))
            return;

        Refresh(uid);
    }

    private bool TryBuildStatus(EntityUid target, YautjaComponent yautja, TransformComponent transform,
        out SuitSensorStatus status)
    {
        status = default!;

        if (!TryComp<MobStateComponent>(target, out var mobState))
            return false;

        if (transform.GridUid == null && transform.MapUid == null)
            return false;

        var area = _areas.TryGetArea(target, out _, out var areaPrototype)
            ? areaPrototype.Name
            : Loc.GetString("cmu-yautja-crew-monitor-area-unknown");

        var rank = yautja.ClanRank;
        if (TryComp<YautjaYoungbloodComponent>(target, out var youngblood) && !youngblood.Blooded)
            rank = YautjaRank.YoungBlood;

        var rankName = HasComp<YautjaBadBloodComponent>(target)
            ? Loc.GetString("cmu-yautja-rank-badblood")
            : Loc.GetString(YautjaRankMetadata.For(rank).LocalizedName);

        var jobDepartments = new List<string> { area };
        status = new SuitSensorStatus(
            GetNetEntity(target),
            GetNetEntity(target),
            MetaData(target).EntityName,
            rankName,
            "JobIconNoId",
            jobDepartments)
        {
            IsAlive = !_mobState.IsDead(target, mobState),
            LocationKind = GetLocationKind(transform),
            Area = area,
            CanTrack = true,
            Coordinates = GetNetCoordinates(transform.Coordinates),
        };

        if (TryComp<DamageableComponent>(target, out var damageable))
        {
            status.TotalDamage = damageable.TotalDamage.Int();
            status.OxygenDamage = YautjaCrewMonitoringMetadata.SumDamageGroup(damageable.Damage, OxygenDamageTypes);
            status.ToxinDamage = YautjaCrewMonitoringMetadata.SumDamageGroup(damageable.Damage, ToxinDamageTypes);
            status.BurnDamage = YautjaCrewMonitoringMetadata.SumDamageGroup(damageable.Damage, BurnDamageTypes);
            status.BruteDamage = YautjaCrewMonitoringMetadata.SumDamageGroup(damageable.Damage, BruteDamageTypes);
        }

        if (_mobThresholds.TryGetThresholdForState(target, MobState.Critical, out var threshold))
            status.TotalDamageThreshold = threshold.Value.Int();

        return true;
    }

    private YautjaCrewMonitoringLocationKind GetLocationKind(TransformComponent transform)
    {
        if (transform.GridUid is not { } grid ||
            !TryComp<BecomesStationComponent>(grid, out var station))
            return YautjaCrewMonitoringLocationKind.HuntingGround;

        return station.Id == "CMUYautjaHunterShip"
            ? YautjaCrewMonitoringLocationKind.MainShip
            : YautjaCrewMonitoringLocationKind.HuntingGround;
    }

    private void UpdateUserInterface(EntityUid uid, CrewMonitoringConsoleComponent component)
    {
        if (!_uiSystem.IsUiOpen(uid, CrewMonitoringUIKey.Key))
            return;

        var xform = Transform(uid);
        if (xform.GridUid is { } grid)
            EnsureComp<NavMapComponent>(grid);

        _uiSystem.SetUiState(uid, CrewMonitoringUIKey.Key,
            new CrewMonitoringState(component.ConnectedSensors.Values.ToList()));
    }
}
