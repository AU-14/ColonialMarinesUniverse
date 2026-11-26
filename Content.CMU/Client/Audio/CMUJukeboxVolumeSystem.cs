using Content.Shared.Audio.Jukebox;
using Content.Shared.CCVar;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;

namespace Content.Client.CMU14.Audio;

/// <summary>
///     Plays jukeboxes at this client's music volume setting. The server plays every jukebox at the same volume and
///     its state can reset the stream's params at any time, so the setting is reapplied every frame before the audio
///     system reads it.
/// </summary>
public sealed partial class CMUJukeboxVolumeSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IConfigurationManager _config = default!;

    private float _volume;

    public override void Initialize()
    {
        UpdatesBefore.Add(typeof(Robust.Client.Audio.AudioSystem));
        Subs.CVar(_config, CCVars.AmbientMusicVolume, gain => _volume = SharedAudioSystem.GainToVolume(gain), true);
    }

    public override void FrameUpdate(float frameTime)
    {
        var query = EntityQueryEnumerator<JukeboxComponent>();
        while (query.MoveNext(out var jukebox))
        {
            if (!TryComp(jukebox.AudioStream, out AudioComponent? audio) || audio.Params.Volume == _volume)
                continue;

            // SetVolume reaches the live audio source; writing Params alone never changes playback.
            _audio.SetVolume(jukebox.AudioStream.Value, _volume, audio);
        }
    }
}
