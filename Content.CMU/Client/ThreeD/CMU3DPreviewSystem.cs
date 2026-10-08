namespace Content.Client.CMU14.ThreeD;

/// <summary>Owns the workbench lifetime so disconnecting releases the old session's sprite references.</summary>
public sealed class CMU3DPreviewSystem : EntitySystem
{
    private CMU3DPreviewWindow? _window;

    public bool Open(string? model = null)
    {
        if (_window == null)
        {
            var window = new CMU3DPreviewWindow();
            _window = window;
            window.OnClose += () => _window = null;
        }
        var found = model == null || _window.SelectModel(model);
        _window.OpenCentered();
        return found;
    }

    public void Close()
    {
        var window = _window;
        _window = null;
        window?.Close();
    }

    public override void Shutdown()
    {
        Close();
        base.Shutdown();
    }
}
