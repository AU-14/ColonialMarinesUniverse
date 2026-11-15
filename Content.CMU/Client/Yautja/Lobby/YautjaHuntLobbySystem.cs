using Content.Shared.CMU14.Yautja;
using Content.Shared.GameTicking;

namespace Content.Client.CMU14.Yautja.Lobby;

public sealed class YautjaHuntLobbySystem : EntitySystem
{
    public bool HuntActive { get; private set; }
    public bool HuntReady { get; private set; }
    public bool AncientSlotAllowed { get; private set; }
    // server's call, the client-side whitelist check also counts Boosty tiers
    public bool HuntWhitelisted { get; private set; }
    public bool StrandedOpen { get; private set; }

    // the Join Hunt card also leads to the stranded slot, which can be open with no hunt running
    public bool CanSeeHuntCard => (HuntActive || StrandedOpen) && HuntWhitelisted;

    public event Action? Updated;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<YautjaHuntLobbyStatusEvent>(OnStatus);
        SubscribeNetworkEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnStatus(YautjaHuntLobbyStatusEvent ev)
    {
        HuntActive = ev.HuntActive;
        HuntReady = ev.HuntReady;
        AncientSlotAllowed = ev.AncientSlotAllowed;
        HuntWhitelisted = ev.HuntWhitelisted;
        StrandedOpen = ev.StrandedOpen;
        Updated?.Invoke();
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        HuntActive = false;
        HuntReady = false;
        StrandedOpen = false;
        Updated?.Invoke();
    }
}
