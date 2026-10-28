using System.Threading;
using Robust.Client.UserInterface;

namespace Content.Client.ContextMenu.UI;

public sealed partial class ContextMenuUIController
{
    private bool _cmuClosing;

    private static void CMUCancelTimer(ref CancellationTokenSource? source)
    {
        var old = source;
        source = null;
        old?.Cancel();
        old?.Dispose();
    }

    private void CMUOnMouseEntered(GUIMouseHoverEventArgs args)
    {
        if (args.SourceControl is ContextMenuElement element)
            OnMouseEntered(element);
    }

    private void CMUOnMouseExited(GUIMouseHoverEventArgs args)
    {
        if (args.SourceControl is ContextMenuElement element)
            OnMouseExited(element);
    }

    private void CMUUnbindElement(ContextMenuElement element)
    {
        element.OnMouseEntered -= CMUOnMouseEntered;
        element.OnMouseExited -= CMUOnMouseExited;
        if (element.CMUKeyHandler != null)
            element.OnKeyBindDown -= element.CMUKeyHandler;
        element.CMUKeyHandler = null;
    }
}
