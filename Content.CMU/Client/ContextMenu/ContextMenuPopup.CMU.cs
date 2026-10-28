using Robust.Client.UserInterface;

namespace Content.Client.ContextMenu.UI;

public sealed partial class ContextMenuPopup
{
    internal Action? CMUPopulate;
    internal bool CMUContentsCreated;

    internal void CMUEnsureContents()
    {
        var populate = CMUPopulate;
        CMUPopulate = null;
        CMUContentsCreated = true;
        populate?.Invoke();
    }

    private void CMUOnChildRemoved(Control control)
    {
        _uiController.OnRemoveElement(this, control);
    }
}
