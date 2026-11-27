using Content.Shared.CMU14.TargetLock;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.TargetLock;

/// <summary>Shows the scanner's report on screen when the server sends one, replacing the one already up.</summary>
public sealed class CMUTargetLockSystem : EntitySystem
{
    private CMUTargetLockWindow? _window;

    public override void Initialize()
    {
        SubscribeNetworkEvent<CMUTargetLockReportEvent>(OnReport);
    }

    private void OnReport(CMUTargetLockReportEvent ev)
    {
        _window?.Close();
        _window = new CMUTargetLockWindow(ev);
        _window.OnClose += () => _window = null;
        _window.OpenCenteredRight();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _window?.Close();
    }
}
