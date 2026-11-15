using System.Collections.Generic;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared.CMU14.Round;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Inventory;
using Content.Shared.Radio.Components;
using Content.Shared.Roles;
using JetBrains.Annotations;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.CMU14.Round;

[UsedImplicitly]
public sealed partial class AddColonistRuleSystem : GameRuleSystem<AddColonistRuleComponent>
{
    [Dependency] private StationJobsSystem _stationJobs = default!;
    [Dependency] private StationSystem _stationSystem = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private IRobustRandom _random = default!;

    private ISawmill _logs = default!;

    public override void Initialize()
    {
        base.Initialize();
        _logs = Logger.GetSawmill("addcolonist");
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawn);
    }

    protected override void Started(EntityUid uid, AddColonistRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        var mapId = _gameTicker.DefaultMap;
        var stationUid = _stationSystem.GetStationInMap(mapId);
        if (stationUid == null || !Exists(stationUid.Value))
            return;

        var stationJobs = EntityManager.GetComponentOrNull<StationJobsComponent>(stationUid.Value);
        if (stationJobs == null)
            return;

        // The rule is FoF's only colonist slot source: it opens the roster itself so
        // planet declarations never leak these jobs into other gamemodes.
        var counts = new Dictionary<ProtoId<JobPrototype>, int>(component.Jobs);
        if (component.RandomJobs.Count > 0)
            counts.TryAdd(_random.Pick(component.RandomJobs), 1);

        foreach (var (jobId, count) in counts)
        {
            _stationJobs.TrySetJobSlot(stationUid.Value, jobId.ToString(), count, true, stationJobs);
            _stationJobs.SetRoundStartJobSlot(stationUid.Value, jobId, count, stationJobs);
        }
    }

    private void OnPlayerSpawn(PlayerSpawnCompleteEvent ev)
    {
        var rule = GetColonistRule(ev.JobId);
        if (rule == null)
            return;

        if (_inventory.TryGetSlotEntity(ev.Mob, "ears", out var existing)
            && HasComp<HeadsetComponent>(existing))
            return;

        var headset = Spawn(rule.Headset, Transform(ev.Mob).Coordinates);
        if (!_inventory.TryEquip(ev.Mob, headset, "ears", silent: true, force: true))
            _logs.Warning($"could not equip {rule.Headset} on a spawned {ev.JobId} colonist");
    }

    private AddColonistRuleComponent? GetColonistRule(string? jobId)
    {
        if (jobId == null)
            return null;

        var query = EntityQueryEnumerator<AddColonistRuleComponent>();
        while (query.MoveNext(out _, out var rule))
        {
            if (rule.Jobs.ContainsKey(jobId) || rule.RandomJobs.Contains(jobId))
                return rule;
        }

        return null;
    }
}
