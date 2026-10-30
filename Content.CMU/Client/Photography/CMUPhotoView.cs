using System.Numerics;
using Content.Shared.CMU14.Photography;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Photography;

/// <summary>
/// A print's picture, shown at a whole number scale once the server has sent it. A picture that arrives while
/// it is being looked at develops in, the way an instant print does.
/// </summary>
public sealed class CMUPhotoView : TextureRect
{
    private const float DevelopSeconds = 1.2f;

    private readonly CMUPhotoClientSystem _photos;
    private readonly int _id;
    private float _developed = 1;

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
        if (id != _id)
            return;

        Texture = _photos.GetPhoto(id);
        _developed = 0;
        ModulateSelfOverride = Color.White.WithAlpha(0f);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (_developed >= 1)
            return;

        _developed = MathF.Min(1, _developed + args.DeltaSeconds / DevelopSeconds);
        // Eased: the image comes up quickly out of the dark, then settles.
        var shown = 1 - (1 - _developed) * (1 - _developed);
        ModulateSelfOverride = Color.White.WithAlpha(shown);
    }
}

/// <summary>Opens a print in its polaroid frame when it is used.</summary>
public sealed partial class CMUPhotoBui : BoundUserInterface
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IResourceCache _cache = default!;

    private CMUPhotoWindow? _window;

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

        // Laid out before it opens, so it opens centred on its full size.
        _window = this.CreateDisposableControl<CMUPhotoWindow>();
        _window.OnClose += Close;
        _window.SetPhoto(photo, _cache, _timing);
        _window.OpenCentered();
    }
}
