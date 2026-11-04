using Content.Shared.CMU14.Administration;

namespace Content.Client.CMU14.Administration;

/// <summary>
/// Whether the local admin is in in-game admin mode. The client admin manager then reports them inactive, so this
/// is what keeps the AHelp window the admin one.
/// </summary>
public sealed class CMUIngameAdminClientSystem : EntitySystem
{
    public bool IngameAdmin { get; private set; }

    public event Action? OnStateChanged;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<CMUIngameAdminStateEvent>(OnState);
    }

    private void OnState(CMUIngameAdminStateEvent ev)
    {
        if (IngameAdmin == ev.IngameAdmin)
            return;

        IngameAdmin = ev.IngameAdmin;
        OnStateChanged?.Invoke();
    }
}
