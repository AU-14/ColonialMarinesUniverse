using System.Linq;
using System.Numerics;
using Content.Client.CMU14.Lobby;
using Content.Shared.Humanoid;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Humanoid;

public sealed partial class LayerMarkingPicker
{
    private bool _cmuGallery;

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        if (_cmuGallery && float.IsFinite(availableSize.X))
        {
            // Reserve the vertical scrollbar and gallery margins before choosing a column count.
            // Resized runs after measurement and can otherwise leave an extra, clipped column.
            Items.Columns = Math.Max(1, (int) ((availableSize.X - 24) / 190));
        }
        return base.MeasureOverride(availableSize);
    }

    public bool CmuGallery
    {
        get => _cmuGallery;
        set
        {
            if (!value || _cmuGallery) return;
            _cmuGallery = true;
            CmuLobbyPresentation.UseVerticalScrolling(this);
            Items.HorizontalExpand = true;
            foreach (var item in Items.Children.OfType<LayerMarkingItem>())
                item.UseCmuGallery();
        }
    }
}

public sealed partial class LayerMarkingItem
{
    private bool _cmuGallery;

    public void UseCmuGallery()
    {
        if (_cmuGallery) return;
        _cmuGallery = true;
        MinWidth = 150;
        var row = (BoxContainer) GetChild(0);
        row.Orientation = LayoutOrientation.Vertical;
        var image = MarkingTexture.Parent!;
        image.Orphan();
        image.HorizontalAlignment = HAlignment.Center;
        MarkingTexture.MouseFilter = MouseFilterMode.Ignore;
        Control preview = image;
        if (_layer is HumanoidVisualLayers.Hair or HumanoidVisualLayers.FacialHair)
        {
            preview = new CmuHeadPreview(_sprite, MarkingTexture.Textures, () =>
            {
                var data = _markingsModel.OrganProfileData[_organ];
                var selected = _markingsModel.SelectedMarkings(_organ, _layer);
                var color = selected is { Count: > 0 } && selected[0].MarkingColors.Count > 0 ? selected[0].MarkingColors[0] : Color.FromHex("#654535");
                return (data.Sex == Sex.Female, data.SkinColor, data.EyeColor, color);
            });
        }
        CmuLobbyPresentation.SetMarkingCardContent(SelectButton, preview, SelectButton.Text ?? string.Empty);
        ColorsButton.MinHeight = 30;
        RefreshCmuGallerySelection();
    }

    private void RefreshCmuGallerySelection()
    {
        if (_cmuGallery)
            CmuLobbyPresentation.Style(SelectButton, selected: SelectButton.Pressed);
    }
}
