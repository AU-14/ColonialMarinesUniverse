using System.Numerics;
using Content.Client.Resources;
using Content.Shared.CMU14.Photography;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.Photography;

/// <summary>
/// A print held up to look at: the picture in an instant photo's paper frame, with what it shows written along
/// the deep foot of the frame in marker. The window is the print itself, sized to it: drag it anywhere to move
/// it, and close it with the pencilled cross in its corner or Escape.
/// </summary>
/// <remarks>
/// The art comes from Content.CMU/Design/Photography/PolaroidPixelArt/polaroid_pixel_art.py; the margins below
/// mirror its NINE_SLICE, CONTENT, WINDOW_SLICE and close mark geometry, in art pixels.
/// </remarks>
public sealed class CMUPhotoWindow : BaseWindow
{
    /// <summary>Screen pixels per art pixel.</summary>
    public const int ArtScale = 3;

    /// <summary>The picture is blown up to about this many pixels across, at a whole-number scale.</summary>
    public const int PictureTarget = 448;

    private const string Textures = "/Textures/CMU14/Photography/";
    private const string CaptionFont = "CMUPhotoCaption";
    private static readonly Color Ink = Color.FromHex("#22263A");

    // polaroid_pixel_art.py NINE_SLICE: the shadow pad plus a corner of paper.
    private const int SliceLeft = 9, SliceTop = 9, SliceRight = 10, SliceBottom = 10;
    // polaroid_pixel_art.py CONTENT: the shadow pad plus the paper border round the picture.
    private const int ContentLeft = 11, ContentTop = 11, ContentRight = 12, ContentBottom = 9;
    private const int WindowSlice = 2;
    // polaroid_pixel_art.py PAD_R, PAD_T, CLOSE and CLOSE_INSET: the close mark in the top border's corner.
    private const int ShadowRight = 2, ShadowTop = 1, CloseSize = 7, CloseInset = 2;
    /// <summary>The foot of the frame under the picture, where the caption is written.</summary>
    private const int CaptionGap = 3, CaptionMinHeight = 22;

    /// <summary>Prints older than these look handled, then yellowed.</summary>
    private static readonly TimeSpan WornAfter = TimeSpan.FromMinutes(20);
    private static readonly TimeSpan AgedAfter = TimeSpan.FromMinutes(60);

    public CMUPhotoWindow()
    {
        MouseFilter = MouseFilterMode.Stop;
    }

    /// <summary>Lays the window out for a print. Call once, before it opens.</summary>
    public void SetPhoto(CMUPhotoComponent photo, IResourceCache cache, IGameTiming timing)
    {
        var picture = Math.Max(1, photo.Size * CMUPhotoLimits.TilePixels);
        var scale = Math.Max(1, PictureTarget / picture);
        var width = picture * scale;

        var paper = new PanelContainer
        {
            PanelOverride = Slices(cache.GetTexture(Textures + FrameFor(photo, timing)),
                StyleBoxTexture.StretchMode.Tile,
                SliceLeft, SliceTop, SliceRight, SliceBottom,
                ContentLeft, ContentTop, ContentRight, ContentBottom),
        };

        // The recess: the paper's lip and the developed area's dark edge, the picture inside it, and the lip's
        // shadow falling over the picture.
        var recess = new PanelContainer
        {
            HorizontalAlignment = HAlignment.Center,
            PanelOverride = Slices(cache.GetTexture(Textures + "polaroid_window.png"),
                StyleBoxTexture.StretchMode.Stretch,
                WindowSlice, WindowSlice, WindowSlice, WindowSlice,
                WindowSlice, WindowSlice, WindowSlice, WindowSlice),
        };
        var shade = new PanelContainer
        {
            MouseFilter = MouseFilterMode.Ignore,
            PanelOverride = Slices(cache.GetTexture(Textures + "polaroid_shade.png"),
                StyleBoxTexture.StretchMode.Stretch,
                WindowSlice, WindowSlice, WindowSlice, WindowSlice, 0, 0, 0, 0),
        };
        var view = new CMUPhotoView(photo.Id, scale);
        recess.AddChild(new Control
        {
            SetSize = new Vector2(width, width),
            Children = { view, new CMUPhotoGhostLayer(view, photo), shade },
        });

        var foot = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            MinHeight = CaptionMinHeight * ArtScale,
            Margin = new Thickness(ArtScale * 2, CaptionGap * ArtScale, ArtScale * 2, 0),
            Children = { Written(Caption(photo), 15, HAlignment.Left, width) },
        };
        if (photo.Photographer.Length > 0)
        {
            foot.AddChild(Written(Loc.GetString("cmu-photo-window-signed", ("photographer", photo.Photographer)),
                13, HAlignment.Right, width));
        }

        paper.AddChild(new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Children = { recess, foot },
        });

        AddChild(new Control
        {
            Children = { paper, CloseMark(cache) },
        });
    }

    /// <summary>The pencilled cross in the top border's corner; it darkens under the mouse.</summary>
    private TextureButton CloseMark(IResourceCache cache)
    {
        var plain = cache.GetTexture(Textures + "polaroid_close.png");
        var hover = cache.GetTexture(Textures + "polaroid_close_hover.png");
        var close = new TextureButton
        {
            TextureNormal = plain,
            Scale = new Vector2(ArtScale, ArtScale),
            HorizontalAlignment = HAlignment.Right,
            VerticalAlignment = VAlignment.Top,
            SetSize = new Vector2(CloseSize * ArtScale, CloseSize * ArtScale),
            Margin = new Thickness(0, (ShadowTop + CloseInset) * ArtScale, (ShadowRight + CloseInset) * ArtScale, 0),
            ToolTip = Loc.GetString("cmu-photo-window-close"),
        };
        close.OnMouseEntered += _ => close.TextureNormal = hover;
        close.OnMouseExited += _ => close.TextureNormal = plain;
        close.OnPressed += _ => Close();
        return close;
    }

    /// <summary>A print gets more handled the longer it has been out of the camera.</summary>
    private static string FrameFor(CMUPhotoComponent photo, IGameTiming timing)
    {
        var age = timing.CurTime - photo.Printed;
        if (photo.Printed == TimeSpan.Zero || age < WornAfter)
            return "polaroid_frame.png";

        return age < AgedAfter ? "polaroid_frame_worn.png" : "polaroid_frame_aged.png";
    }

    private static string Caption(CMUPhotoComponent photo)
    {
        var contents = photo.Contents.Count == 0
            ? Loc.GetString("cmu-photo-window-caption-nothing")
            : string.Join(", ", photo.Contents);
        return Loc.GetString("cmu-photo-window-caption", ("contents", contents));
    }

    /// <summary>Text in marker on the frame. Names are added as plain text, never parsed as markup.</summary>
    private static RichTextLabel Written(string text, int size, HAlignment align, float width)
    {
        var message = new FormattedMessage();
        message.PushTag(new MarkupNode("font", new MarkupParameter(CaptionFont),
            new Dictionary<string, MarkupParameter> { ["size"] = new MarkupParameter((long) size) }));
        message.PushColor(Ink);
        message.AddText(text);
        message.Pop();
        message.Pop();

        var label = new RichTextLabel { MaxWidth = width, HorizontalAlignment = align };
        label.SetMessage(message);
        return label;
    }

    private static StyleBoxTexture Slices(Texture texture, StyleBoxTexture.StretchMode mode,
        int left, int top, int right, int bottom,
        int contentLeft, int contentTop, int contentRight, int contentBottom)
    {
        return new StyleBoxTexture
        {
            Texture = texture,
            TextureScale = new Vector2(ArtScale, ArtScale),
            Mode = mode,
            PatchMarginLeft = left,
            PatchMarginTop = top,
            PatchMarginRight = right,
            PatchMarginBottom = bottom,
            ContentMarginLeftOverride = contentLeft * ArtScale,
            ContentMarginTopOverride = contentTop * ArtScale,
            ContentMarginRightOverride = contentRight * ArtScale,
            ContentMarginBottomOverride = contentBottom * ArtScale,
        };
    }

    protected override DragMode GetDragModeFor(Vector2 relativeMousePos)
    {
        return DragMode.Move;
    }
}
