using Content.Shared.CMU14.Audio;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Robust.Shared.Audio;

namespace Content.Shared.Movement.Systems;

public abstract partial class SharedMoverController
{
    [Dependency] private CMUSharedAudioBudgetSystem _cmuAudioBudget = default!;
    [Dependency] private CMUSharedZLevelsSystem _cmuAudioZLevels = default!;

    private void PlayCMUFootstepSound(SoundSpecifier sound, EntityUid source, EntityUid user, AudioParams parameters)
    {
        if (!_cmuAudioZLevels.PlayPredictedThroughOpenings(sound, source, user, parameters))
            _cmuAudioBudget.PlayPredicted(sound, source, user, parameters);
    }
}
