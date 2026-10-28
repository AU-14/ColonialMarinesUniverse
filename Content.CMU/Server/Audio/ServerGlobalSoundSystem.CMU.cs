using Content.Shared.CMU14.Audio;
using Robust.Shared.Player;

namespace Content.Server.Audio;

public sealed partial class ServerGlobalSoundSystem
{
    public void StopAdminGlobal(Filter playerFilter, bool replay = true)
    {
        RaiseNetworkEvent(new CMUStopAdminSoundEvent(), playerFilter, recordReplay: replay);
    }
}
