using Content.Server.Spawners.Components;
using Content.Shared._CMU14.Threats;
using Content.Shared._RMC14.Spawners;
using Content.Shared.AU14;
using Content.Shared.AU14.Scenario;
using Content.Shared.AU14.util;
using Content.Shared.GameTicking;
using Content.Shared.Roles;
using Robust.Shared.Map;
using Robust.Shared.Profiling;
using Robust.Shared.Prototypes;

namespace Content.Server.AU14.Scenario;

/// <summary>
/// Maintains the live, factual index used to discover initialized round-world spawn markers.
/// Gameplay systems retain ownership of selection policy, cooldowns, randomness, and spawning.
/// </summary>
public sealed partial class CMURoundWorldIndexSystem : EntitySystem
{
    private static readonly ProtoId<JobPrototype> ColonyCivilianJob = "AU14JobCivilianColonist";

    private static readonly string ColonyCivilianSpawnTag =
        ScenarioMarkerTags.ClfCivilianSpawn(ColonyCivilianJob.Id);
    private static readonly string EntityBucketTag = ScenarioMarkerTags.Bucket(nameof(ThreatMarkerType.Entity));
    private static readonly string GenericMarkerIdTag = ScenarioMarkerTags.MarkerId(string.Empty);
    private static readonly string LeaderBucketTag = ScenarioMarkerTags.Bucket(nameof(ThreatMarkerType.Leader));
    private static readonly string MemberBucketTag = ScenarioMarkerTags.Bucket(nameof(ThreatMarkerType.Member));
    private static readonly string[] ClfCivilianSpawnTags = [ColonyCivilianSpawnTag];
    private static readonly string[] ClfSafehouseTags = [ScenarioMarkerTags.ForceClfSafehouse];

    [Dependency] private EntityQuery<AuInsertMarkerComponent> _insertQuery = default!;
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

    private readonly RoundWorldSpawnMarkerStore _index = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);

        SubscribeLocalEvent<AuInsertMarkerComponent, MapInitEvent>(OnInsertMarkerMapInit);
        SubscribeLocalEvent<ParachuteMarkerComponent, MapInitEvent>(OnParachuteMarkerMapInit);
        SubscribeLocalEvent<SafehouseMarkerComponent, MapInitEvent>(OnSafehouseMarkerMapInit);
        SubscribeLocalEvent<ScenarioSpawnMarkerComponent, MapInitEvent>(OnScenarioMarkerMapInit);
        SubscribeLocalEvent<SpawnPointComponent, MapInitEvent>(OnSpawnPointMapInit);
        SubscribeLocalEvent<ThreatSpawnMarkerComponent, MapInitEvent>(OnThreatMarkerMapInit);
        SubscribeLocalEvent<XenoLeaderSpawnPointComponent, MapInitEvent>(OnXenoLeaderMarkerMapInit);
        SubscribeLocalEvent<XenoSpawnPointComponent, MapInitEvent>(OnXenoMarkerMapInit);
    }

    /// <summary>
    /// Copies live markers in a world scope without scanning each marker component store.
    /// The destination is always cleared before results are written.
    /// </summary>
    public void CopySpawnMarkers(
        in RoundWorldScope scope,
        IReadOnlyList<string> requiredTags,
        List<EntityUid> destination)
    {
        CopySpawnMarkers(scope, requiredTags, destination, scenarioTagsAreAuthoritative: false);
    }

    private void CopySpawnMarkers(
        in RoundWorldScope scope,
        IReadOnlyList<string> requiredTags,
        List<EntityUid> destination,
        bool scenarioTagsAreAuthoritative)
    {
        using var profile = _prof.Group("CMU Round Spawn Index Query");

        if (!_index.TryCopyCandidates(requiredTags, destination))
            return;

        var candidateCount = destination.Count;
        var writeIndex = 0;
        for (var i = 0; i < destination.Count; i++)
        {
            var uid = destination[i];
            if (!MarkerMatches(uid, scope, requiredTags, scenarioTagsAreAuthoritative))
                continue;

            destination[writeIndex++] = uid;
        }

        if (writeIndex < destination.Count)
            destination.RemoveRange(writeIndex, destination.Count - writeIndex);

        if (_prof.IsEnabled)
        {
            _prof.WriteValue("CMU Round Spawn Index Candidates", candidateCount);
            _prof.WriteValue("CMU Round Spawn Index Matches", destination.Count);
        }
    }

    /// <summary>
    /// Resolves canonical Scenario Plan markers on one map. Explicit scenario tags take precedence over legacy
    /// components on hybrid marker entities, preserving Scenario Plan migration semantics.
    /// </summary>
    public List<EntityUid> ResolveScenarioSpawnMarkers(MapId mapId, IReadOnlyList<string> requiredTags)
    {
        var markers = new List<EntityUid>();
        CopySpawnMarkers(
            RoundWorldScope.Map(mapId),
            requiredTags,
            markers,
            scenarioTagsAreAuthoritative: true);

        return markers;
    }

    /// <summary>
    /// Copies normalized hostile or third-party force markers into a caller-owned result.
    /// </summary>
    public void CopyForceSpawnMarkers(
        in RoundWorldScope scope,
        bool thirdParty,
        ThreatMarkerType markerType,
        string markerId,
        bool parachute,
        List<EntityUid> destination)
    {
        var forceTag = thirdParty
            ? ScenarioMarkerTags.ForceThirdParty
            : ScenarioMarkerTags.ForceHostile;
        var bucketTag = ScenarioMarkerTags.Bucket(markerType.ToString());
        var markerIdTag = ScenarioMarkerTags.MarkerId(markerId);
        string[] tags = parachute
            ? [forceTag, bucketTag, markerIdTag, ScenarioMarkerTags.EntryParachute]
            : [forceTag, bucketTag, markerIdTag];

        CopySpawnMarkers(scope, tags, destination);
    }

    /// <summary>
    /// Copies normalized CLF safehouse markers into a caller-owned result.
    /// </summary>
    public void CopyClfSafehouseMarkers(in RoundWorldScope scope, List<EntityUid> destination)
    {
        CopySpawnMarkers(scope, ClfSafehouseTags, destination);
    }

    /// <summary>
    /// Copies normalized CLF civilian backup spawn points into a caller-owned result.
    /// </summary>
    public void CopyClfCivilianSpawnMarkers(in RoundWorldScope scope, List<EntityUid> destination)
    {
        CopySpawnMarkers(scope, ClfCivilianSpawnTags, destination);
    }

    /// <summary>
    /// Refreshes the indexed semantic facts for a marker whose mutable marker fields were changed after map init.
    /// Prototype-authored marker semantics are otherwise treated as immutable for the life of the initialized entity.
    /// </summary>
    public void RefreshMarkerFacts(EntityUid uid)
    {
        _index.RemoveMarker(uid);
        if (!TerminatingOrDeleted(uid) && HasIndexedMarkerFact(uid))
            IndexMarker(uid);
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _index.Clear();
    }

    private void OnInsertMarkerMapInit(Entity<AuInsertMarkerComponent> ent, ref MapInitEvent args)
    {
        IndexMarker(ent.Owner);
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

        if (_insertQuery.HasComponent(uid))
            _index.AddTag(uid, ScenarioMarkerTags.EntryGround);

        if (_parachuteQuery.HasComponent(uid))
            _index.AddTag(uid, ScenarioMarkerTags.EntryParachute);

        if (_scenarioQuery.TryGetComponent(uid, out var scenario))
        {
            foreach (var tag in scenario.Tags)
            {
                _index.AddTag(uid, tag);
            }
        }

        if (_threatQuery.TryGetComponent(uid, out var threat))
        {
            _index.AddTag(
                uid,
                threat.ThirdParty ? ScenarioMarkerTags.ForceThirdParty : ScenarioMarkerTags.ForceHostile);
            _index.AddTag(uid, BucketTag(threat.ThreatMarkerType));
            _index.AddTag(uid, ScenarioMarkerTags.MarkerId(threat.ID));
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

    private bool HasIndexedMarkerFact(EntityUid uid)
    {
        return _insertQuery.HasComponent(uid) ||
               _parachuteQuery.HasComponent(uid) ||
               _safehouseQuery.HasComponent(uid) ||
               _scenarioQuery.HasComponent(uid) ||
               _spawnPointQuery.HasComponent(uid) ||
               _threatQuery.HasComponent(uid) ||
               _leaderQuery.HasComponent(uid) ||
               _memberQuery.HasComponent(uid);
    }

    private void IndexLegacyXenoMarker(EntityUid uid, string bucketTag)
    {
        _index.AddTag(uid, ScenarioMarkerTags.ForceHostile);
        _index.AddTag(uid, bucketTag);
        _index.AddTag(uid, GenericMarkerIdTag);
    }

    private bool MarkerMatches(
        EntityUid uid,
        in RoundWorldScope scope,
        IReadOnlyList<string> requiredTags,
        bool scenarioTagsAreAuthoritative)
    {
        if (TerminatingOrDeleted(uid) ||
            !_metaQuery.TryGetComponent(uid, out var meta) ||
            !_xformQuery.TryGetComponent(uid, out var transform))
        {
            _index.RemoveMarker(uid);
            return false;
        }

        if (meta.EntityPaused ||
            (!scope.IncludesEveryMap && transform.MapID != scope.MapId) ||
            (scope.GridUid is { } gridUid && !IsOnGrid(transform, gridUid)))
        {
            return false;
        }

        if (_insertQuery.HasComponent(uid) && SingleTagMatches(ScenarioMarkerTags.EntryGround, requiredTags))
            return true;

        var parachute = _parachuteQuery.HasComponent(uid);
        if (_scenarioQuery.TryGetComponent(uid, out var scenario))
        {
            if (ScenarioMarkerMatches(scenario, parachute, requiredTags))
                return true;

            if (scenarioTagsAreAuthoritative)
                return false;
        }

        if (_threatQuery.TryGetComponent(uid, out var threat) &&
            ThreatMarkerMatches(threat, parachute, requiredTags))
        {
            return true;
        }

        if (parachute && SingleTagMatches(ScenarioMarkerTags.EntryParachute, requiredTags))
            return true;

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

        if (_memberQuery.HasComponent(uid) &&
            LegacyXenoMarkerMatches(MemberBucketTag, requiredTags))
        {
            return true;
        }

        return false;
    }

    private static bool IsOnGrid(TransformComponent transform, EntityUid gridUid)
    {
        return transform.GridUid == gridUid || transform.ParentUid == gridUid;
    }

    private static bool ScenarioMarkerMatches(
        ScenarioSpawnMarkerComponent marker,
        bool parachute,
        IReadOnlyList<string> requiredTags)
    {
        if (!ScenarioMarkerKindMatches(marker.Kind, requiredTags))
            return false;

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

    private static bool ScenarioMarkerKindMatches(
        SpawnMarkerKind kind,
        IReadOnlyList<string> requiredTags)
    {
        foreach (var required in requiredTags)
        {
            if (required.Equals(ScenarioMarkerTags.ForceHostile, StringComparison.OrdinalIgnoreCase) &&
                kind != SpawnMarkerKind.ThreatMarker)
            {
                return false;
            }

            if (required.Equals(ScenarioMarkerTags.ForceThirdParty, StringComparison.OrdinalIgnoreCase) &&
                kind != SpawnMarkerKind.ThirdPartyMarker)
            {
                return false;
            }

            if (required.Equals(ScenarioMarkerTags.ForceClfSafehouse, StringComparison.OrdinalIgnoreCase) &&
                kind != SpawnMarkerKind.ClfSafehouse)
            {
                return false;
            }

            if (required.StartsWith(ScenarioMarkerTags.ForceClfCivilianSpawnPrefix,
                    StringComparison.OrdinalIgnoreCase) &&
                kind != SpawnMarkerKind.ClfCivilianSpawn)
            {
                return false;
            }
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
