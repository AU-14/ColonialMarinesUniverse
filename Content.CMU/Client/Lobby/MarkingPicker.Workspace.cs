using Content.Client.CMU14.Lobby;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Humanoid;

public sealed partial class MarkingPicker
{
    private bool _workspaceGallery;
    public void EnableWorkspaceGallery()
    {
        _workspaceGallery = true;
        ApplyWorkspaceGallery();
    }

    private void ApplyWorkspaceGallery()
    {
        if (!_workspaceGallery) return;
        CmuLobbyPresentation.UseVerticalScrolling(this);
        foreach (var child in OrganTabs.Children)
            if (child is OrganMarkingPicker organ) organ.EnableWorkspaceGallery();
        OrganTabs.PanelStyleBoxOverride = CmuLobbyPresentation.Box(padding: 8);
    }
}

public sealed partial class OrganMarkingPicker
{
    private bool _workspaceGallery;
    public void EnableWorkspaceGallery()
    {
        _workspaceGallery = true;
        ApplyWorkspaceGallery();
    }

    private void ApplyWorkspaceGallery()
    {
        if (!_workspaceGallery) return;
        CmuLobbyPresentation.UseVerticalScrolling(this);
        foreach (var child in LayerTabs.Children)
            if (child is LayerMarkingPicker layer) layer.CmuGallery = true;
        LayerTabs.PanelStyleBoxOverride = CmuLobbyPresentation.Box(padding: 8);
    }
}
