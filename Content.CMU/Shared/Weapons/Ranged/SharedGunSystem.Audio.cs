using Content.Shared.CMU14.Audio;
using Robust.Shared.Audio;

namespace Content.Shared.Weapons.Ranged.Systems;

public abstract partial class SharedGunSystem
{
    [Dependency] private CMUSharedAudioBudgetSystem _cmuAudioBudget = default!;

    protected void PlayCMUTransientSound(SoundSpecifier? sound, EntityUid source, AudioParams? parameters = null)
    {
        if (!_zLevels.PlayPredictedThroughOpenings(sound, source, null, parameters))
            Audio.PlayPvs(sound, source, parameters);
    }
}
