using Content.Shared.CCVar;
using Content.Shared.CMU14.Audio;
using Robust.Client.Graphics;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Audio;

public sealed partial class CMUClientAudioBudgetSystem : CMUSharedAudioBudgetSystem
{
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override (EntityUid Entity, AudioComponent Component)? PlayPredicted(
        SoundSpecifier? sound, EntityUid source, EntityUid? user, AudioParams? parameters = null)
    {
        if (sound == null || !_timing.IsFirstTimePredicted)
            return null;

        var audioParams = parameters ?? sound.Params;
        if (audioParams.Loop)
            return base.PlayPredicted(sound, source, user, parameters);

        var listener = _eye.CurrentEye.Position;
        var transform = Transform(source);
        if (listener.MapId != transform.MapID ||
            Audio.GetAudioDistance((_transform.GetWorldPosition(transform) - listener.Position).Length()) > audioParams.MaxDistance)
            return null;

        var resolved = Audio.ResolveSound(sound);
        var file = Audio.GetAudioPath(resolved);
        var limit = Math.Max(0, Math.Min(Configuration.GetCVar(CCVars.CMUTransientAudioPerFile),
            Configuration.GetCVar(Robust.Shared.CVars.AudioDefaultConcurrent)));
        var count = 0;
        // Include paused/detached and engine-rejected entities conservatively. Their
        // lifetime, not the engine's private counter, controls admission here.
        var query = AllEntityQuery<AudioComponent>();
        while (query.MoveNext(out _, out var audio))
            if (!audio.Deleted && audio.FileName == file)
                count++;
        if (count >= limit)
            return null;

        return Audio.PlayEntity(resolved, Filter.Local(), source, false, audioParams);
    }
}
