using Robust.Shared.Audio;

namespace Content.Shared.CMU14.ZLevels.Core.EntitySystems;

public abstract partial class CMUSharedZLevelsSystem
{
    public virtual bool PlayPredictedThroughOpenings(SoundSpecifier? sound, EntityUid source,
        EntityUid? user, AudioParams? audioParams = null)
    {
        return false;
    }

    public virtual bool PlayPredictedDirectlyAcrossZ(
        SoundSpecifier? sound,
        EntityUid source,
        EntityUid? user,
        int maxDepth = 1)
    {
        return false;
    }
}
