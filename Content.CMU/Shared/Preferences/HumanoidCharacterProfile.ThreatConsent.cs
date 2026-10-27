using System.Linq;
using Content.Shared.CMU14.Threats;
using Robust.Shared.Prototypes;

namespace Content.Shared.Preferences;

public sealed partial class HumanoidCharacterProfile
{
    /// <summary>An explicit empty mode selection means no threats; an unset legacy selection means any threat.</summary>
    public bool HasExplicitThreatPreferences(string? gamemode)
    {
        var key = NormalizePreferenceGamemode(gamemode);
        return (!string.IsNullOrEmpty(key) && _gamemodeThreatPreferences.ContainsKey(key)) || _threatPreferences.Count > 0;
    }

    public bool AllowsThreatForGamemode(string? gamemode, ProtoId<ThreatPrototype> threat)
    {
        return !HasExplicitThreatPreferences(gamemode) || GetThreatPreferencesForGamemode(gamemode)
            .Any(preference => preference.Id.Equals(threat.Id, StringComparison.OrdinalIgnoreCase));
    }
}
