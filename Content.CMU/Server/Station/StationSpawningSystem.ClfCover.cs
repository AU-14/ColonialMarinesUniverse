using Content.Server.Access.Components;
using Content.Shared.Roles;

namespace Content.Server.Station.Systems;

public sealed partial class StationSpawningSystem
{
    // A covert recruit keeps the title and icon initialized by the civilian card's preset.
    private bool HasClfCoverId(EntityUid card, JobPrototype job)
    {
        return string.Equals(job.RoundForce, "CLF", StringComparison.OrdinalIgnoreCase) &&
               TryComp(card, out PresetIdCardComponent? preset) && preset.JobName is { } cover &&
               ProtoMan.HasIndex(cover);
    }
}
