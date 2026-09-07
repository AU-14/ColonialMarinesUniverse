using Robust.Client.Graphics;
using Robust.Shared.Graphics;

namespace Content.Client.CMU14.ZLevels.Core;

/// <summary>
/// Restores the caller's viewport state after a composed render, including failed passes.
/// </summary>
internal readonly struct CMUZViewportRenderState : IDisposable
{
    private readonly IClydeViewport _viewport;
    private readonly IEye? _eye;
    private readonly Color? _clearColor;

    public CMUZViewportRenderState(IClydeViewport viewport)
    {
        _viewport = viewport;
        _eye = viewport.Eye;
        _clearColor = viewport.ClearColor;
    }

    public void Dispose()
    {
        _viewport.Eye = _eye;
        _viewport.ClearColor = _clearColor;
    }
}
