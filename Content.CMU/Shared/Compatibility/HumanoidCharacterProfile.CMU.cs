using Content.Shared._CMU14.Threats;
using Content.Shared.AU14.Allegiance;
using Content.Shared.AU14.Origin;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared.Preferences;

public sealed partial class HumanoidCharacterProfile
{
    [DataField]
    public ProtoId<AllegiancePrototype>? Allegiance { get; private set; }

    [DataField]
    public ProtoId<OriginPrototype>? Origin { get; private set; } = "UAAmerica";

    [DataField]
    public bool Synthetic { get; private set; }

    [DataField]
    private Dictionary<string, Dictionary<ProtoId<JobPrototype>, JobPriority>> _gamemodeJobPriorities = new();

    [DataField]
    private HashSet<ProtoId<ThreatPrototype>> _threatPreferences = new();

    [DataField]
    private Dictionary<string, HashSet<ProtoId<ThreatPrototype>>> _gamemodeThreatPreferences = new();

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
}
