using Content.Server.Spawners.Components;
using Content.Shared._CMU14.Threats;
using Content.Shared._RMC14.Spawners;
using Content.Shared.AU14;
using Content.Shared.AU14.Scenario;
using Content.Shared.GameTicking;
using Content.Shared.Roles;
using Robust.Shared.Map;
using Robust.Shared.Profiling;
using Robust.Shared.Prototypes;

namespace Content.Server.AU14.Scenario;

/// <summary>
/// Maintains a semantic index of initialized round spawn markers.
/// </summary>
public sealed partial class ScenarioSpawnIndexSystem : EntitySystem
{
    private static readonly ProtoId<JobPrototype> ColonyCivilianJob = "AU14JobCivilianColonist";

    private static readonly string ColonyCivilianSpawnTag =
        ScenarioMarkerTags.ClfCivilianSpawn(ColonyCivilianJob.Id);
    private static readonly string EntityBucketTag = ScenarioMarkerTags.Bucket(nameof(ThreatMarkerType.Entity));
    private static readonly string GenericMarkerIdTag = ScenarioMarkerTags.MarkerId(string.Empty);
    private static readonly string LeaderBucketTag = ScenarioMarkerTags.Bucket(nameof(ThreatMarkerType.Leader));
    private static readonly string MemberBucketTag = ScenarioMarkerTags.Bucket(nameof(ThreatMarkerType.Member));

    [Dependency] private EntityQuery<XenoLeaderSpawnPointComponent> _leaderQuery = default!;
    [Dependency] private EntityQuery<XenoSpawnPointComponent> _memberQuery = default!;
    [Dependency] private EntityQuery<MetaDataComponent> _metaQuery = default!;
    [Dependency] private EntityQuery<ParachuteMarkerComponent> _parachuteQuery = default!;
    [Dependency] private ProfManager _prof = default!;
    [Dependency] private EntityQuery<SafehouseMarkerComponent> _safehouseQuery = default!;
    [Dependency] private EntityQuery<ScenarioSpawnMarkerComponent> _scenarioQuery = default!;
    [Dependency] private EntityQuery<SpawnPointComponent> _spawnPointQuery = default!;
    [Dependency] private EntityQuery<ThreatSpawnMarkerComponent> _threatQuery = default!;
    [Dependency] private EntityQuery<TransformComponent> _xformQuery = default!;

    private readonly ScenarioSpawnIndexStore _index = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);

        SubscribeLocalEvent<ParachuteMarkerComponent, MapInitEvent>(OnParachuteMarkerMapInit);
        SubscribeLocalEvent<SafehouseMarkerComponent, MapInitEvent>(OnSafehouseMarkerMapInit);
        SubscribeLocalEvent<ScenarioSpawnMarkerComponent, MapInitEvent>(OnScenarioMarkerMapInit);
        SubscribeLocalEvent<SpawnPointComponent, MapInitEvent>(OnSpawnPointMapInit);
        SubscribeLocalEvent<ThreatSpawnMarkerComponent, MapInitEvent>(OnThreatMarkerMapInit);
        SubscribeLocalEvent<XenoLeaderSpawnPointComponent, MapInitEvent>(OnXenoLeaderMarkerMapInit);
        SubscribeLocalEvent<XenoSpawnPointComponent, MapInitEvent>(OnXenoMarkerMapInit);
    }

    /// <summary>
    /// Resolves live markers on one map without scanning each marker component store.
    /// </summary>
    public List<EntityUid> Resolve(MapId mapId, IReadOnlyList<string> requiredTags)
    {
        using var profile = _prof.Group("CMU Round Spawn Index Query");

        var markers = new List<EntityUid>();
        if (!_index.TryCopyCandidates(requiredTags, markers))
            return markers;

        var candidateCount = markers.Count;
        var writeIndex = 0;
        for (var i = 0; i < markers.Count; i++)
        {
            var uid = markers[i];
            if (!MarkerMatches(uid, mapId, requiredTags))
                continue;

            markers[writeIndex++] = uid;
        }

        if (writeIndex < markers.Count)
            markers.RemoveRange(writeIndex, markers.Count - writeIndex);

        if (_prof.IsEnabled)
        {
            _prof.WriteValue("CMU Round Spawn Index Candidates", candidateCount);
            _prof.WriteValue("CMU Round Spawn Index Matches", markers.Count);
        }

        return markers;
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _index.Clear();
    }

    private void OnParachuteMarkerMapInit(Entity<ParachuteMarkerComponent> ent, ref MapInitEvent args)
    {
        IndexMarker(ent.Owner);
    }

    private void OnSafehouseMarkerMapInit(Entity<SafehouseMarkerComponent> ent, ref MapInitEvent args)
    {
        IndexMarker(ent.Owner);
    }

    private void OnScenarioMarkerMapInit(Entity<ScenarioSpawnMarkerComponent> ent, ref MapInitEvent args)
    {
        IndexMarker(ent.Owner);
    }

    private void OnSpawnPointMapInit(Entity<SpawnPointComponent> ent, ref MapInitEvent args)
    {
        IndexMarker(ent.Owner);
    }

    private void OnThreatMarkerMapInit(Entity<ThreatSpawnMarkerComponent> ent, ref MapInitEvent args)
    {
        IndexMarker(ent.Owner);
    }

    private void OnXenoLeaderMarkerMapInit(Entity<XenoLeaderSpawnPointComponent> ent, ref MapInitEvent args)
    {
        IndexMarker(ent.Owner);
    }

    private void OnXenoMarkerMapInit(Entity<XenoSpawnPointComponent> ent, ref MapInitEvent args)
    {
        IndexMarker(ent.Owner);
    }

    private void IndexMarker(EntityUid uid)
    {
        _index.AddMarker(uid);

        if (_scenarioQuery.TryGetComponent(uid, out var scenario))
        {
            foreach (var tag in scenario.Tags)
            {
                _index.AddTag(uid, tag);
            }

            if (_parachuteQuery.HasComponent(uid))
                _index.AddTag(uid, ScenarioMarkerTags.EntryParachute);
        }

        if (_threatQuery.TryGetComponent(uid, out var threat))
        {
            _index.AddTag(
                uid,
                threat.ThirdParty ? ScenarioMarkerTags.ForceThirdParty : ScenarioMarkerTags.ForceHostile);
            _index.AddTag(uid, BucketTag(threat.ThreatMarkerType));
            _index.AddTag(uid, ScenarioMarkerTags.MarkerId(threat.ID));
            if (_parachuteQuery.HasComponent(uid))
                _index.AddTag(uid, ScenarioMarkerTags.EntryParachute);
        }

        if (_safehouseQuery.HasComponent(uid))
            _index.AddTag(uid, ScenarioMarkerTags.ForceClfSafehouse);

        if (_spawnPointQuery.TryGetComponent(uid, out var spawnPoint) &&
            spawnPoint.Job?.Id.Equals(ColonyCivilianJob.Id, StringComparison.OrdinalIgnoreCase) == true)
        {
            _index.AddTag(uid, ColonyCivilianSpawnTag);
        }

        if (_leaderQuery.HasComponent(uid))
            IndexLegacyXenoMarker(uid, LeaderBucketTag);

        if (_memberQuery.HasComponent(uid))
            IndexLegacyXenoMarker(uid, MemberBucketTag);
    }

    private void IndexLegacyXenoMarker(EntityUid uid, string bucketTag)
    {
        _index.AddTag(uid, ScenarioMarkerTags.ForceHostile);
        _index.AddTag(uid, bucketTag);
        _index.AddTag(uid, GenericMarkerIdTag);
    }

    private bool MarkerMatches(EntityUid uid, MapId mapId, IReadOnlyList<string> requiredTags)
    {
        if (TerminatingOrDeleted(uid) ||
            !_metaQuery.TryGetComponent(uid, out var meta) ||
            !_xformQuery.TryGetComponent(uid, out var transform))
        {
            _index.RemoveMarker(uid);
            return false;
        }

        if (meta.EntityPaused || transform.MapID != mapId)
            return false;

        var parachute = _parachuteQuery.HasComponent(uid);
        if (_scenarioQuery.TryGetComponent(uid, out var scenario))
            return ScenarioMarkerMatches(scenario, parachute, requiredTags);

        if (_threatQuery.TryGetComponent(uid, out var threat) &&
            ThreatMarkerMatches(threat, parachute, requiredTags))
        {
            return true;
        }

        if (_safehouseQuery.HasComponent(uid) &&
            SingleTagMatches(ScenarioMarkerTags.ForceClfSafehouse, requiredTags))
        {
            return true;
        }

        if (_spawnPointQuery.TryGetComponent(uid, out var spawnPoint) &&
            spawnPoint.Job?.Id.Equals(ColonyCivilianJob.Id, StringComparison.OrdinalIgnoreCase) == true &&
            SingleTagMatches(ColonyCivilianSpawnTag, requiredTags))
        {
            return true;
        }

        if (_leaderQuery.HasComponent(uid) &&
            LegacyXenoMarkerMatches(LeaderBucketTag, requiredTags))
        {
            return true;
        }

        return _memberQuery.HasComponent(uid) &&
               LegacyXenoMarkerMatches(MemberBucketTag, requiredTags);
    }

    private static bool ScenarioMarkerMatches(
        ScenarioSpawnMarkerComponent marker,
        bool parachute,
        IReadOnlyList<string> requiredTags)
    {
        foreach (var required in requiredTags)
        {
            if (parachute &&
                required.Equals(ScenarioMarkerTags.EntryParachute, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var found = false;
            foreach (var tag in marker.Tags)
            {
                if (!tag.Equals(required, StringComparison.OrdinalIgnoreCase))
                    continue;

                found = true;
                break;
            }

            if (!found)
                return false;
        }

        return true;
    }

    private static bool ThreatMarkerMatches(
        ThreatSpawnMarkerComponent marker,
        bool parachute,
        IReadOnlyList<string> requiredTags)
    {
        var forceTag = marker.ThirdParty
            ? ScenarioMarkerTags.ForceThirdParty
            : ScenarioMarkerTags.ForceHostile;
        var bucketTag = BucketTag(marker.ThreatMarkerType);

        foreach (var required in requiredTags)
        {
            if (required.Equals(forceTag, StringComparison.OrdinalIgnoreCase) ||
                required.Equals(bucketTag, StringComparison.OrdinalIgnoreCase) ||
                MatchesMarkerIdTag(required, marker.ID) ||
                parachute && required.Equals(
                    ScenarioMarkerTags.EntryParachute,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private static bool LegacyXenoMarkerMatches(string bucketTag, IReadOnlyList<string> requiredTags)
    {
        foreach (var required in requiredTags)
        {
            if (required.Equals(ScenarioMarkerTags.ForceHostile, StringComparison.OrdinalIgnoreCase) ||
                required.Equals(bucketTag, StringComparison.OrdinalIgnoreCase) ||
                required.Equals(GenericMarkerIdTag, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private static bool SingleTagMatches(string tag, IReadOnlyList<string> requiredTags)
    {
        foreach (var required in requiredTags)
        {
            if (!required.Equals(tag, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    private static bool MatchesMarkerIdTag(string required, string markerId)
    {
        const string prefix = "marker-id:";
        if (!required.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var expected = string.IsNullOrWhiteSpace(markerId) ? "<generic>" : markerId;
        return required.AsSpan(prefix.Length).Equals(expected, StringComparison.OrdinalIgnoreCase);
    }

    private static string BucketTag(ThreatMarkerType markerType)
    {
        return markerType switch
        {
            ThreatMarkerType.Leader => LeaderBucketTag,
            ThreatMarkerType.Entity => EntityBucketTag,
            ThreatMarkerType.Member => MemberBucketTag,
            _ => ScenarioMarkerTags.Bucket(markerType.ToString()),
        };
    }
}
