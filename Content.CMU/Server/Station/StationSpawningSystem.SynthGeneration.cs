using Content.Shared._RMC14.Synth;
using Content.Shared.Preferences;
using Content.Shared.Roles;

namespace Content.Server.Station.Systems;

public sealed partial class StationSpawningSystem
{
    private void ApplySyntheticGeneration(EntityUid entity, JobPrototype? job, HumanoidCharacterProfile? profile)
    {
        if (profile == null || job is not { IsSynthetic: true, JobEntity: null, UsePlayerProfile: true } ||
            !TryComp<SynthComponent>(entity, out var synth))
        {
            return;
        }

        EntityManager.System<SharedSynthSystem>().ConfigureGeneration((entity, synth), profile.SyntheticGeneration);
    }
}
