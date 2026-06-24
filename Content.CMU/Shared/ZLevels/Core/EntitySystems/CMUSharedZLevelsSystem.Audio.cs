using Robust.Shared.Audio;

namespace Content.Shared._CMU14.ZLevels.Core.EntitySystems;

public abstract partial class CMUSharedZLevelsSystem
{
    public virtual bool PlayPredictedDirectlyAcrossZ(
        SoundSpecifier? sound,
        EntityUid source,
        EntityUid? user,
        int maxDepth = 1)
    {
        return false;
    }
}
