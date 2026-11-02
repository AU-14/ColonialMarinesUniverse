using Robust.Client.UserInterface;

namespace Content.Client.CMU14.Interface;

/// <summary>Keeps CRT source controls live for layout/input, drawing them only through their CRT sibling.</summary>
public sealed class CrtScreenContainer : Control
{
    private CrtScreenControl? _capturing;
    private int _sourceIndex;
    private Vector2i _sourcePosition;

    protected override void PreRenderChildren(ref ControlRenderArguments args)
    {
        _capturing = null;
        base.PreRenderChildren(ref args);
    }

    protected override void RenderChildOverride(ref ControlRenderArguments args, int childIndex, Vector2i position)
    {
        var child = GetChild(childIndex);
        for (var i = childIndex + 1; i < ChildCount; i++)
        {
            if (GetChild(i) is not CrtScreenControl crt || crt.Source != child || !crt.Drawing ||
                crt.PixelPosition != child.PixelPosition || crt.PixelSize != child.PixelSize)
                continue;

            _capturing = crt;
            _sourceIndex = childIndex;
            _sourcePosition = position;
            return;
        }

        base.RenderChildOverride(ref args, childIndex, position);
        if (_capturing is { } capturing && child == capturing)
        {
            // A failed capture must show the live source in this frame as well as subsequent ones.
            if (capturing.LastError != null)
                RenderControl(ref args, _sourceIndex, _sourcePosition);
            _capturing = null;
        }
    }
}
