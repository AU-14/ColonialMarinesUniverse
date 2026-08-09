using Content.Server.AU14.Round;

namespace Content.Server._RMC14.Rules.DistressSignal;

public sealed partial class CMDistressSignalRuleSystem
{
    [Dependency] private PlatoonSpawnRuleSystem _platoonSpawn = default!;

    private bool HasCmuPlatoonDropshipSetup()
    {
        return _platoonSpawn.SelectedGovforPlatoon?.CompatibleDropships.Count > 0 ||
               _platoonSpawn.SelectedOpforPlatoon?.CompatibleDropships.Count > 0;
    }

    internal static bool ShouldInitializeLegacyDropships(bool cmuPlatoonOwnsDropships)
    {
        return !cmuPlatoonOwnsDropships;
    }
}
