using Content.Server.AU14.Round;
using Content.Shared.CMU.Round;

namespace Content.Server._RMC14.Rules.DistressSignal;

public sealed partial class CMDistressSignalRuleSystem
{
    [Dependency] private CMURoundDirectorSystem _cmuRoundDirector = default!;

    private bool HasCmuPlatoonDropshipSetup()
    {
        return (_cmuRoundDirector.TryGetCommittedLegacyForce(RoundSide.Govfor, out var govfor) &&
                govfor.CompatibleDropships.Count > 0) ||
               (_cmuRoundDirector.TryGetCommittedLegacyForce(RoundSide.Opfor, out var opfor) &&
                opfor.CompatibleDropships.Count > 0);
    }

    internal static bool ShouldInitializeLegacyDropships(bool cmuPlatoonOwnsDropships)
    {
        return !cmuPlatoonOwnsDropships;
    }
}
