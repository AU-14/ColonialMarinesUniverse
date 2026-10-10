namespace Content.Client.CMU14.ThreeD;

/// <summary>Owns the workbench lifetime so disconnecting releases the old session's sprite references.</summary>
public sealed partial class CMU3DPreviewSystem : EntitySystem
{
    [Dependency] private CMU3DModelLibrary _models = default!;

    private CMU3DPreviewWindow? _window;
    private CMU3DModelLibrary.Lease? _modelLease;

    public bool Open(string? model = null)
    {
        if (_window == null)
        {
            _modelLease = _models.AcquireWorkbench();
            try
            {
                var window = new CMU3DPreviewWindow();
                _window = window;
                window.OnClose += () => ReleaseWindow(window);
            }
            catch
            {
                _modelLease.Dispose();
                _modelLease = null;
                throw;
            }
        }
        var found = model == null || _window.SelectModel(model);
        _window.OpenCentered();
        return found;
    }

    public void Close()
    {
        var window = _window;
        window?.Close();
        if (window != null)
            ReleaseWindow(window);
    }

    private void ReleaseWindow(CMU3DPreviewWindow window)
    {
        if (_window != window)
            return;
        _window = null;
        window.ReleaseResources();
        var lease = _modelLease;
        _modelLease = null;
        lease?.Dispose();
    }

    public override void Shutdown()
    {
        Close();
        base.Shutdown();
    }
}
