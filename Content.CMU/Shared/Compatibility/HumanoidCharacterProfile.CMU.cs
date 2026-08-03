using System.Linq;
using System.Text.RegularExpressions;
using Content.Shared._CMU14.Threats;
using Content.Shared.AU14.Allegiance;
using Content.Shared.AU14.Origin;
using Content.Shared.CCVar;
using Content.Shared.Roles;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Preferences;

public sealed partial class HumanoidCharacterProfile
{
    private static readonly Regex CmuHeightRegex = new(@"^[4-6]'(?:[0-9]|1[01])$", RegexOptions.Compiled);
    public const int MinWeight = 90;
    public const int MaxWeight = 300;

    [DataField]
    public ProtoId<AllegiancePrototype>? Allegiance { get; private set; }

    [DataField]
    public ProtoId<OriginPrototype>? Origin { get; private set; } = "UAAmerica";

    [DataField]
    public bool Synthetic { get; private set; }

    [DataField]
    public string ShortExamine { get; private set; } = string.Empty;

    [DataField]
    public string FullDescription { get; private set; } = string.Empty;

    [DataField]
    public string MedicalRecord { get; private set; } = string.Empty;

    [DataField]
    public string CriminalRecord { get; private set; } = string.Empty;

    [DataField]
    public string GeneralRecord { get; private set; } = string.Empty;

    [DataField]
    public string Height { get; private set; } = string.Empty;

    [DataField]
    public int Weight { get; private set; } = 160;

    [DataField]
    public BuildType Build { get; private set; } = BuildType.Average;

    [DataField]
    public bool HideMetaInformation { get; private set; }

    [DataField]
    private Dictionary<string, Dictionary<ProtoId<JobPrototype>, JobPriority>> _gamemodeJobPriorities = new();

    [DataField]
    private HashSet<ProtoId<ThreatPrototype>> _threatPreferences = new();

    [DataField]
    private Dictionary<string, HashSet<ProtoId<ThreatPrototype>>> _gamemodeThreatPreferences = new();

    [DataField]
    private Dictionary<string, HashSet<ProtoId<AntagPrototype>>> _gamemodeAntagPreferences = new();

    public IReadOnlyDictionary<string, Dictionary<ProtoId<JobPrototype>, JobPriority>> GamemodeJobPriorities =>
        _gamemodeJobPriorities;

    public IReadOnlyDictionary<string, HashSet<ProtoId<AntagPrototype>>> GamemodeAntagPreferences =>
        _gamemodeAntagPreferences;

    public IReadOnlyDictionary<string, HashSet<ProtoId<ThreatPrototype>>> GamemodeThreatPreferences =>
        _gamemodeThreatPreferences;

    public IReadOnlyDictionary<ProtoId<JobPrototype>, JobPriority> GetJobPrioritiesForGamemode(string? gamemode)
    {
        var key = NormalizePreferenceGamemode(gamemode);
        return !string.IsNullOrEmpty(key) && _gamemodeJobPriorities.TryGetValue(key, out var priorities)
            ? priorities
            : _jobPriorities;
    }

    public IReadOnlySet<ProtoId<ThreatPrototype>> GetThreatPreferencesForGamemode(string? gamemode)
    {
        var key = NormalizePreferenceGamemode(gamemode);
        return !string.IsNullOrEmpty(key) && _gamemodeThreatPreferences.TryGetValue(key, out var preferences)
            ? preferences
            : _threatPreferences;
    }

    public JobPriority GetJobPriorityForGamemode(string? gamemode, ProtoId<JobPrototype> job)
    {
        return GetJobPrioritiesForGamemode(gamemode).GetValueOrDefault(job, JobPriority.Never);
    }

    public IReadOnlySet<ProtoId<AntagPrototype>> GetAntagPreferencesForGamemode(string? gamemode)
    {
        var key = NormalizePreferenceGamemode(gamemode);
        return !string.IsNullOrEmpty(key) && _gamemodeAntagPreferences.TryGetValue(key, out var preferences)
            ? preferences
            : _antagPreferences;
    }

    private static string NormalizePreferenceGamemode(string? gamemode)
    {
        if (string.IsNullOrWhiteSpace(gamemode))
            return string.Empty;

        return gamemode.Trim().ToLowerInvariant() switch
        {
            "insurgency" => "Insurgency",
            "colonyfall" => "ColonyFall",
            "distresssignal" => "DistressSignal",
            _ => gamemode.Trim(),
        };
    }

    public HumanoidCharacterProfile WithSynthetic(bool synthetic)
    {
        return new HumanoidCharacterProfile(this) { Synthetic = synthetic };
    }

    public HumanoidCharacterProfile WithAllegiance(ProtoId<AllegiancePrototype>? allegiance)
    {
        return new HumanoidCharacterProfile(this) { Allegiance = allegiance };
    }

    public HumanoidCharacterProfile WithOrigin(ProtoId<OriginPrototype>? origin)
    {
        return new HumanoidCharacterProfile(this) { Origin = origin };
    }

    public HumanoidCharacterProfile WithShortExamine(string value) => new(this) { ShortExamine = value };
    public HumanoidCharacterProfile WithFullDescription(string value) => new(this) { FullDescription = value };
    public HumanoidCharacterProfile WithMedicalRecord(string value) => new(this) { MedicalRecord = value };
    public HumanoidCharacterProfile WithCriminalRecord(string value) => new(this) { CriminalRecord = value };
    public HumanoidCharacterProfile WithGeneralRecord(string value) => new(this) { GeneralRecord = value };
    public HumanoidCharacterProfile WithHeight(string value) => new(this) { Height = value };
    public HumanoidCharacterProfile WithWeight(int value) => new(this) { Weight = Math.Clamp(value, MinWeight, MaxWeight) };
    public HumanoidCharacterProfile WithBuild(BuildType value) => new(this) { Build = value };
    public HumanoidCharacterProfile WithHideMetaInformation(bool value) => new(this) { HideMetaInformation = value };

    public HumanoidCharacterProfile WithGamemodeJobPriority(
        string? gamemode,
        ProtoId<JobPrototype> job,
        JobPriority priority)
    {
        var key = NormalizePreferenceGamemode(gamemode);
        if (string.IsNullOrEmpty(key))
            return WithJobPriority(job, priority);

        var all = _gamemodeJobPriorities.ToDictionary(
            pair => pair.Key,
            pair => new Dictionary<ProtoId<JobPrototype>, JobPriority>(pair.Value));
        if (!all.TryGetValue(key, out var priorities))
            all[key] = priorities = new Dictionary<ProtoId<JobPrototype>, JobPriority>(_jobPriorities);

        if (priority == JobPriority.High)
        {
            foreach (var (otherJob, otherPriority) in priorities.ToArray())
            {
                if (otherJob != job && otherPriority == JobPriority.High)
                    priorities[otherJob] = JobPriority.Medium;
            }
        }

        if (priority == JobPriority.Never)
            priorities.Remove(job);
        else
            priorities[job] = priority;

        return new(this) { _gamemodeJobPriorities = all };
    }

    public HumanoidCharacterProfile WithGamemodeAntagPreference(
        string? gamemode,
        ProtoId<AntagPrototype> antag,
        bool enabled)
    {
        var key = NormalizePreferenceGamemode(gamemode);
        if (string.IsNullOrEmpty(key))
            return WithAntagPreference(antag, enabled);

        var all = _gamemodeAntagPreferences.ToDictionary(
            pair => pair.Key,
            pair => new HashSet<ProtoId<AntagPrototype>>(pair.Value));
        if (!all.TryGetValue(key, out var preferences))
            all[key] = preferences = new HashSet<ProtoId<AntagPrototype>>(_antagPreferences);

        if (enabled)
            preferences.Add(antag);
        else
            preferences.Remove(antag);

        return new(this) { _gamemodeAntagPreferences = all };
    }

    public HumanoidCharacterProfile WithGamemodeThreatPreference(
        string? gamemode,
        ProtoId<ThreatPrototype> threat,
        bool enabled)
    {
        var key = NormalizePreferenceGamemode(gamemode);
        var all = _gamemodeThreatPreferences.ToDictionary(
            pair => pair.Key,
            pair => new HashSet<ProtoId<ThreatPrototype>>(pair.Value));
        if (!all.TryGetValue(key, out var preferences))
            all[key] = preferences = new HashSet<ProtoId<ThreatPrototype>>(_threatPreferences);

        if (enabled)
            preferences.Add(threat);
        else
            preferences.Remove(threat);

        return new(this) { _gamemodeThreatPreferences = all };
    }

    private void CopyCmuFrom(HumanoidCharacterProfile other)
    {
        Allegiance = other.Allegiance;
        Origin = other.Origin;
        Synthetic = other.Synthetic;
        ShortExamine = other.ShortExamine;
        FullDescription = other.FullDescription;
        MedicalRecord = other.MedicalRecord;
        CriminalRecord = other.CriminalRecord;
        GeneralRecord = other.GeneralRecord;
        Height = other.Height;
        Weight = other.Weight;
        Build = other.Build;
        HideMetaInformation = other.HideMetaInformation;
        _gamemodeJobPriorities = other._gamemodeJobPriorities.ToDictionary(
            pair => pair.Key,
            pair => new Dictionary<ProtoId<JobPrototype>, JobPriority>(pair.Value));
        _gamemodeAntagPreferences = other._gamemodeAntagPreferences.ToDictionary(
            pair => pair.Key,
            pair => new HashSet<ProtoId<AntagPrototype>>(pair.Value));
        _threatPreferences = new HashSet<ProtoId<ThreatPrototype>>(other._threatPreferences);
        _gamemodeThreatPreferences = other._gamemodeThreatPreferences.ToDictionary(
            pair => pair.Key,
            pair => new HashSet<ProtoId<ThreatPrototype>>(pair.Value));
    }

    private bool CmuMemberwiseEquals(HumanoidCharacterProfile other)
    {
        return Allegiance == other.Allegiance &&
               Origin == other.Origin &&
               Synthetic == other.Synthetic &&
               ShortExamine == other.ShortExamine &&
               FullDescription == other.FullDescription &&
               MedicalRecord == other.MedicalRecord &&
               CriminalRecord == other.CriminalRecord &&
               GeneralRecord == other.GeneralRecord &&
               Height == other.Height &&
               Weight == other.Weight &&
               Build == other.Build &&
               HideMetaInformation == other.HideMetaInformation;
    }

    private void AddCmuHash(ref HashCode hash)
    {
        hash.Add(Allegiance);
        hash.Add(Origin);
        hash.Add(Synthetic);
        hash.Add(ShortExamine);
        hash.Add(FullDescription);
        hash.Add(MedicalRecord);
        hash.Add(CriminalRecord);
        hash.Add(GeneralRecord);
        hash.Add(Height);
        hash.Add(Weight);
        hash.Add(Build);
        hash.Add(HideMetaInformation);
    }

    private void EnsureCmuValid(IConfigurationManager configuration)
    {
        static string ClampField(string value, int maximum)
        {
            var stripped = FormattedMessage.RemoveMarkupOrThrow(value);
            return stripped.Length > maximum ? stripped[..maximum] : stripped;
        }

        ShortExamine = ClampField(ShortExamine, configuration.GetCVar(CCVars.MaxShortExamineLength));
        var maximum = configuration.GetCVar(CCVars.MaxCharacterDescriptionFieldLength);
        FullDescription = ClampField(FullDescription, maximum);
        MedicalRecord = ClampField(MedicalRecord, maximum);
        CriminalRecord = ClampField(CriminalRecord, maximum);
        GeneralRecord = ClampField(GeneralRecord, maximum);
        Height = CmuHeightRegex.IsMatch(Height)
            ? Height
            : string.Empty;
        Weight = Math.Clamp(Weight, MinWeight, MaxWeight);
        if (!Enum.IsDefined(Build))
            Build = BuildType.Average;
    }
}
