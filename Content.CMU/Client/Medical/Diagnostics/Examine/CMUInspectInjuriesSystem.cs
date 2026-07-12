using Content.Shared._CMU14.Medical.Diagnostics.Examine;
using JetBrains.Annotations;

namespace Content.Client._CMU14.Medical.Diagnostics.Examine;

[UsedImplicitly]
public sealed partial class CMUInspectInjuriesSystem : EntitySystem
{
    private CMUInspectInjuriesWindow? _window;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<CMUInspectInjuriesResponseEvent>(OnInspectInjuriesResponse);
    }

    public override void Shutdown()
    {
        _window?.Close();
        _window = null;

        base.Shutdown();
    }

    private void OnInspectInjuriesResponse(CMUInspectInjuriesResponseEvent ev)
    {
        if (_window == null || _window.Disposed)
        {
            _window = new CMUInspectInjuriesWindow();
            _window.OnClose += () => _window = null;
        }

        _window.SetReport(ev.TargetName, ev.Markup, ev.Bleeding);

        if (_window.IsOpen)
        {
            _window.MoveToFront();
            return;
        }

        _window.OpenCentered();
    }
}
