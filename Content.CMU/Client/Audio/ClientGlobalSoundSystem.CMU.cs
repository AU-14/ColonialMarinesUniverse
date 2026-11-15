using Content.Shared.CMU14.Audio;

namespace Content.Client.Audio;

public sealed partial class ClientGlobalSoundSystem
{
    private void OnCMUStopAdminSound(CMUStopAdminSoundEvent ev)
    {
        foreach (var stream in _adminAudio)
        {
            _audio.Stop(stream);
        }

        _adminAudio.Clear();
    }
}
