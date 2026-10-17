using Content.Shared.Roles;

namespace Content.Server.CrewManifest;

public sealed partial class CrewManifestSystem
{
    private const string ClfRoundForce = "CLF";

    private static bool CMUShouldHideFromCrewManifest(JobPrototype? job)
    {
        return string.Equals(job?.RoundForce, ClfRoundForce, StringComparison.OrdinalIgnoreCase);
    }
}
