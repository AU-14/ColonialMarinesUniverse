using System.Linq;
using Content.Shared.AU14.util;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.AU14.Scenario;

#pragma warning disable RA0042 // Runtime prototype registration still requires explicit PrototypeAttribute metadata.
[Prototype("votingChoices")]
public sealed partial class VotingChoicesPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public string Preset { get; private set; } = string.Empty;

    [DataField]
    public List<string> Planets { get; private set; } = new();

    [DataField]
    public List<ProtoId<RoundGroupPrototype>> Groups { get; private set; } = new();

    [DataField]
    public ScenarioThreatVoteDefinition ThreatVote { get; private set; } = new();

    [DataField]
    public List<ScenarioPlanetVotingChoicesDefinition> PlanetChoices { get; private set; } = new();

    public IReadOnlyList<string> Presets =>
        string.IsNullOrWhiteSpace(Preset)
            ? Array.Empty<string>()
            : new[] { Preset };

    public IReadOnlyList<string> SupportedPlanets
    {
        get
        {
            if (PlanetChoices.Count == 0)
                return Planets;

            return Planets
                .Concat(PlanetChoices.SelectMany(choice => choice.Planets))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }

    public IReadOnlyList<ScenarioDeferredForceChoiceDefinition> DeferredForceChoices
    {
        get
        {
            if (!ThreatVote.HasData)
                return Array.Empty<ScenarioDeferredForceChoiceDefinition>();

            return new[] { ThreatVote.ToDeferredForceChoice() };
        }
    }
}

[DataDefinition]
public sealed partial class ScenarioPlanetVotingChoicesDefinition
{
    [DataField(required: true)]
    public List<string> Planets { get; private set; } = new();

    [DataField]
    public List<ProtoId<RoundGroupPrototype>> Groups { get; private set; } = new();

    [DataField]
    public ScenarioThreatVoteDefinition ThreatVote { get; private set; } = new();

    [DataField]
    public ScenarioThreatVoteDefinition BackupThreatVote { get; private set; } = new();

    [DataField]
    public List<ProtoId<RoundGroupPrototype>> BackupGroups { get; private set; } = new();

    [DataField]
    public bool BackupQuiet { get; private set; }

    public bool HasData => Groups.Count > 0 || ThreatVote.HasData;

    public bool HasBackupData => BackupGroups.Count > 0 || BackupThreatVote.HasData || BackupQuiet;

    public IReadOnlyList<ScenarioDeferredForceChoiceDefinition> DeferredForceChoices =>
        ThreatVote.HasData
            ? new[] { ThreatVote.ToDeferredForceChoice() }
            : Array.Empty<ScenarioDeferredForceChoiceDefinition>();

    public IReadOnlyList<ScenarioDeferredForceChoiceDefinition> BackupDeferredForceChoices =>
        BackupThreatVote.HasData
            ? new[] { BackupThreatVote.ToDeferredForceChoice() }
            : Array.Empty<ScenarioDeferredForceChoiceDefinition>();

    public bool SupportsPlanet(string planetId)
    {
        return Planets.Count == 0 ||
               Planets.Any(candidate => candidate.Equals(planetId, StringComparison.OrdinalIgnoreCase));
    }
}

[Prototype("votingBackup")]
public sealed partial class VotingBackupPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public string Preset { get; private set; } = string.Empty;

    [DataField(required: true)]
    public ProtoId<VotingChoicesPrototype> Setup { get; private set; }

    [DataField]
    public List<string> Planets { get; private set; } = new();

    public ProtoId<VotingChoicesPrototype> VotingChoices => Setup;

    public IReadOnlyList<string> SupportedPlanets => Planets;
}

[Prototype("roundGroup")]
public sealed partial class RoundGroupPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public RoundForceSide Side { get; private set; } = RoundForceSide.None;

    [DataField]
    public RoundForceSource Source { get; private set; } = RoundForceSource.None;

    [DataField(required: true)]
    public RoundGroupKind Kind { get; private set; }

    [DataField(required: true)]
    public string SourcePrototypeId { get; private set; } = string.Empty;

    [DataField]
    public ScenarioSpawnDefinition Spawn { get; private set; } = new();

    [DataField]
    public string GroupId { get; private set; } = string.Empty;

    [DataField]
    public List<string> WinConditionRuleIds { get; private set; } = new();

    [DataField]
    public ScenarioSpawnTimingDefinition Timing { get; private set; } = new();
}

[DataDefinition]
[Virtual]
public partial class ScenarioSpawnDefinition
{
    [DataField]
    public List<ScenarioSpawnBodyBucketDefinition> BodyBuckets { get; private set; } = new();

    [DataField]
    public List<SpawnMarkerRequirementDefinition> MarkerRequirements { get; private set; } = new();

    [DataField]
    public bool AllowsUnderfill { get; private set; }

    public bool HasData =>
        BodyBuckets.Count > 0 ||
        MarkerRequirements.Count > 0 ||
        AllowsUnderfill;
}

[Prototype("spawnMarker")]
public sealed partial class SpawnMarkerPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public SpawnMarkerKind Kind { get; private set; }

    [DataField(required: true)]
    public List<string> Tags { get; private set; } = new();

    [DataField]
    public int Count { get; private set; } = 1;
}
#pragma warning restore RA0042

[DataDefinition]
public sealed partial class ScenarioDeferredForceChoiceDefinition
{
    private const string DefaultThreatVoteChoiceId = "DeferredThreat:{presetId}:{planetId}";
    private const string DefaultThreatVoteReservationPolicy = "SmallestCandidateBodyCountAllowsUnderfill";

    public ScenarioDeferredForceChoiceDefinition()
    {
    }

    public ScenarioDeferredForceChoiceDefinition(
        string choiceId,
        List<ProtoId<RoundGroupPrototype>> candidates,
        string reservationPolicy)
    {
        ChoiceId = choiceId;
        Candidates = candidates;
        ReservationPolicy = reservationPolicy;
    }

    [DataField(required: true)]
    public string ChoiceId { get; private set; } = DefaultThreatVoteChoiceId;

    [DataField(required: true)]
    public List<ProtoId<RoundGroupPrototype>> Candidates { get; private set; } = new();

    [DataField]
    public string ReservationPolicy { get; private set; } = DefaultThreatVoteReservationPolicy;
}

[DataDefinition]
public sealed partial class ScenarioThreatVoteDefinition
{
    private const string DefaultChoiceId = "DeferredThreat:{presetId}:{planetId}";
    private const string DefaultReservationPolicy = "SmallestCandidateBodyCountAllowsUnderfill";

    [DataField]
    public string ChoiceId { get; private set; } = DefaultChoiceId;

    [DataField(required: true)]
    public List<ProtoId<RoundGroupPrototype>> Candidates { get; private set; } = new();

    [DataField]
    public string ReservationPolicy { get; private set; } = DefaultReservationPolicy;

    public bool HasData => Candidates.Count > 0;

    public ScenarioDeferredForceChoiceDefinition ToDeferredForceChoice()
    {
        return new ScenarioDeferredForceChoiceDefinition(ChoiceId, Candidates, ReservationPolicy);
    }
}

[DataDefinition]
public sealed partial class ScenarioSpawnBodyBucketDefinition
{
    [DataField(required: true)]
    public string Bucket { get; private set; } = string.Empty;

    [DataField]
    public int Count { get; private set; }

    [DataField]
    public Dictionary<string, int> Bodies { get; private set; } = new();

    [DataField]
    public Dictionary<string, JobScaleEntry> Scaling { get; private set; } = new();
}

[DataDefinition]
public sealed partial class SpawnMarkerRequirementDefinition
{
    [DataField(required: true)]
    public string Bucket { get; private set; } = string.Empty;

    [DataField]
    public int RequiredBodyCount { get; private set; }

    [DataField]
    public int RequiredMarkerCount { get; private set; } = 1;

    [DataField(required: true)]
    public List<string> RequiredTags { get; private set; } = new();

    [DataField]
    public bool WarningOnly { get; private set; }
}

[DataDefinition]
public sealed partial class ScenarioSpawnTimingDefinition
{
    [DataField]
    public int? DelayMinSeconds { get; private set; }

    [DataField]
    public int? DelayMaxSeconds { get; private set; }
}

public enum RoundGroupKind
{
    Hostile,
    ThirdParty,
    Clf,
    Platoon,
}

public enum RoundForceSide
{
    None,
    Govfor,
    Opfor,
    Clf,
    Hostile,
    ThirdParty,
    Civilian,
}

public enum RoundForceSource
{
    None,
    GameRule,
    Platoon,
    Threat,
    ThirdParty,
    SpawnMarker,
}

public enum SpawnMarkerKind
{
    ThreatMarker,
    ThirdPartyMarker,
    ClfSafehouse,
    ClfCivilianSpawn,
}
