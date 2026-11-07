using System.Numerics;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;

namespace Content.Server.CMU14.ZLevels.Core;

public sealed partial class CMUZLevelsSystem
{
    public override bool PlayPredictedThroughOpenings(SoundSpecifier? sound, EntityUid source,
        EntityUid? user, AudioParams? audioParams = null)
    {
        if (sound == null)
            return true;
        var parameters = audioParams ?? sound.Params;
        var transform = Transform(source);
        if (parameters.Loop || transform.MapUid is not { } map)
            return false;

        var resolved = _audioSystem.ResolveSound(sound);
        var creatingProjection = _creatingZLevelAudioProjection;
        _creatingZLevelAudioProjection = true;
        try
        {
            PlayDirectAudioOriginal(resolved, parameters, source, user);
            if (!_zLevelsEnabled || !_crossZAudioEnabled || !TryGetZNetwork(map, out _))
                return true;

            var position = _transform.GetWorldPosition(transform);
            var depth = Math.Min(_maxRenderDepth, MaxZLevelsBelowRendering);
            ProjectTransientAudioThroughOpenings(resolved, parameters, user, map, position, -1, depth);
            ProjectTransientAudioThroughOpenings(resolved, parameters, user, map, position, 1, depth);
            return true;
        }
        finally
        {
            _creatingZLevelAudioProjection = creatingProjection;
        }
    }

    private void ProjectTransientAudioThroughOpenings(ResolvedSoundSpecifier sound, AudioParams parameters,
        EntityUid? excluded, EntityUid sourceMap, Vector2 position, int step, int maxDepth)
    {
        Entity<CMUZLevelMapComponent?> currentMap = sourceMap;
        for (var depth = 1; depth <= maxDepth; depth++)
        {
            if (!TryMapOffset(currentMap, step, out var target))
                return;
            var surface = step > 0 ? target.Value.Owner : currentMap.Owner;
            if (!TryFindOpeningNear(surface, position, CrossZAudioOpeningRadius, out position))
                return;
            var filter = BuildCrossZAudioFilter(parameters, excluded, target.Value.Owner, position);
            if (filter.Count > 0)
                CreateZLevelAudioProjection(parameters, AudioFlags.None, sound, filter, target.Value.Owner, position);
            currentMap = (target.Value.Owner, target.Value.Comp);
        }
    }
}
