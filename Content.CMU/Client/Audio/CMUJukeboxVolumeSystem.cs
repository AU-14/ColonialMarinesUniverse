using Content.Shared.Audio.Jukebox;
using Content.Shared.CCVar;
using Robust.Client.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;

namespace Content.Client.CMU14.Audio;

/// <summary>
///     Plays jukeboxes at this client's jukebox volume setting, faded by distance from full gain on the stream's
///     plateau to silence at its range. The server plays jukeboxes with rolloff 0, so this is the whole falloff
///     whatever attenuation model the client has. The client audio system re-derives a stream's live volume from
///     its params every frame and server state resets them, so the result is baked into the params every frame.
/// </summary>
public sealed partial class CMUJukeboxVolumeSystem : EntitySystem
{
    [Dependency] private AudioSystem _audioSystem = default!;
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private float _volume;

    // The server fixes a jukebox stream's volume when it starts: the track's own volume plus the instance offset.
    private readonly Dictionary<EntityUid, float> _serverVolume = new();

    public override void Initialize()
    {
        UpdatesBefore.Add(typeof(Robust.Client.Audio.AudioSystem));
        Subs.CVar(_config, CCVars.CMUJukeboxVolume, gain => _volume = SharedAudioSystem.GainToVolume(gain), true);
    }

    public override void FrameUpdate(float frameTime)
    {
        var listener = _audioSystem.GetListenerCoordinates();
        var query = EntityQueryEnumerator<JukeboxComponent>();
        while (query.MoveNext(out var jukebox))
        {
            if (!TryComp(jukebox.AudioStream, out AudioComponent? audio))
                continue;

            if (!_serverVolume.TryGetValue(audio.Owner, out var serverVolume))
            {
                serverVolume = audio.Params.Volume;
                _serverVolume[audio.Owner] = serverVolume;
            }

            var xform = Transform(audio.Owner);
            var distance = xform.MapID == listener.MapId
                ? (_transform.GetWorldPosition(xform) - listener.Position).Length()
                : float.PositiveInfinity;
            var volume = serverVolume
                + _volume
                + SharedAudioSystem.GainToVolume(Falloff(distance, audio.Params.ReferenceDistance, audio.Params.MaxDistance));
            if (audio.Params.Volume == volume)
                continue;

            // Avoid dirtying networked audio for a client-only volume setting.
#pragma warning disable RA0002
            audio.Params = audio.Params with { Volume = volume };
#pragma warning restore RA0002
            audio.Volume = volume;
        }
    }

    // Gain from 1 on the plateau to 0 at the range edge. Squared so loudness drops evenly across the span and the
    // engine's hard stop at the edge lands on silence.
    private static float Falloff(float distance, float plateau, float range)
    {
        if (distance <= plateau)
            return 1f;

        if (distance >= range)
            return 0f;

        var fade = 1f - (distance - plateau) / (range - plateau);
        return fade * fade;
    }
}
