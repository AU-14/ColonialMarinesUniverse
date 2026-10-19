using System.Numerics;
using Content.Shared.CMU14.Photography;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.Photography;

/// <summary>A print's picture, shown at a whole number scale once the server has sent it.</summary>
public sealed class CMUPhotoView : TextureRect
{
    private readonly CMUPhotoClientSystem _photos;
    private readonly int _id;

    public CMUPhotoView(int id, int scale)
    {
        _id = id;
        _photos = IoCManager.Resolve<IEntityManager>().System<CMUPhotoClientSystem>();
        TextureScale = new Vector2(scale, scale);
        Stretch = StretchMode.KeepCentered;
        Texture = _photos.GetPhoto(id);
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();
        _photos.PhotoLoaded += OnLoaded;
        Texture ??= _photos.GetPhoto(_id);
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();
        _photos.PhotoLoaded -= OnLoaded;
    }

    private void OnLoaded(int id)
    {
        if (id == _id)
            Texture = _photos.GetPhoto(id);
    }
}

/// <summary>Opens a print full size when it is used.</summary>
public sealed class CMUPhotoBui : BoundUserInterface
{
    private DefaultWindow? _window;

    public CMUPhotoBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        if (!EntMan.TryGetComponent(Owner, out CMUPhotoComponent? photo))
        {
            Close();
            return;
        }

        _window = this.CreateWindow<DefaultWindow>();
        _window.Title = Loc.GetString("cmu-photo-window-title", ("photographer", photo.Photographer));
        // Small frames are blown up more so every print opens at a similar size.
        var scale = Math.Max(1, 448 / Math.Max(1, photo.Size * CMUPhotoLimits.TilePixels));
        _window.Contents.AddChild(new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            Children =
            {
                new CMUPhotoView(photo.Id, scale),
                Caption(photo),
            },
        });
    }

    /// <summary>What the print shows, as plain text: names are never parsed as markup.</summary>
    private static RichTextLabel Caption(CMUPhotoComponent photo)
    {
        var contents = photo.Contents.Count == 0
            ? Loc.GetString("cmu-photo-window-caption-nothing")
            : string.Join(", ", photo.Contents);
        var label = new RichTextLabel { MaxWidth = 448 };
        label.SetMessage(FormattedMessage.FromUnformatted(Loc.GetString("cmu-photo-window-caption", ("contents", contents))));
        return label;
    }
}
