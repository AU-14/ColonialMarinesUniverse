using Content.Shared.CMU14.Threats;
using Robust.Shared.Prototypes;

namespace Content.Server.Preferences.Managers;

public sealed partial class ServerPreferencesManager
{
    // Migrate stored selections before profile validation can discard the retired IDs.
    private static ProtoId<ThreatPrototype> MigrateLegacyThreatPreference(string id)
        => id switch
        {
            "AbominationsThreatCF" => "BiomorphsThreatCF",
            "AbominationsThreatDS" => "BiomorphsThreatDS",
            _ => id,
        };
}
