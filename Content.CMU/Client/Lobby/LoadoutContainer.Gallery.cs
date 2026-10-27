using Content.Client.CMU14.Lobby;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Lobby.UI.Loadouts;

public sealed partial class LoadoutContainer
{
    private bool _cmuGalleryCard;

    public void UseCmuGallery()
    {
        if (_cmuGalleryCard) return;
        _cmuGalleryCard = true;
        Orientation = LayoutOrientation.Vertical;
        SeparationOverride = 6;
        MinWidth = 150;
        SelectButton.MinHeight = 128;
        SelectButton.Margin = new Thickness(0);
        SelectButton.Label.ClipText = true;
        SelectButton.ToolTip = SelectButton.Text;
        CmuLobbyPresentation.Style(SelectButton, selected: IsSelected);
        var image = Sprite.Parent!;
        image.Orphan();
        image.HorizontalAlignment = HAlignment.Center;
        image.MouseFilter = MouseFilterMode.Ignore;
        Sprite.MouseFilter = MouseFilterMode.Ignore;
        SelectButton.Label.Orphan();
        var content = new BoxContainer { Orientation = LayoutOrientation.Vertical, SeparationOverride = 8 };
        content.AddChild(image);
        var name = new RichTextLabel { HorizontalExpand = true, MaxWidth = 150, MouseFilter = MouseFilterMode.Ignore };
        name.SetMessage(SelectButton.Text ?? string.Empty);
        content.AddChild(name);
        SelectButton.AddChild(content);
    }
}
