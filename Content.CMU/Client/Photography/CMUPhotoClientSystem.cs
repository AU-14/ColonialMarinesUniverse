using System.IO;
using System.Numerics;
using Content.Client.Viewport;
using Content.Shared.CMU14.Photography;
using Content.Shared.Hands.EntitySystems;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.State;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Content.Client.CMU14.Photography;

/// <summary>
/// Captures photos for the server from the local game view, caches prints' pictures, draws the camera's
/// framing outline and puts each print's thumbnail in the window of its sprite.
/// </summary>
public sealed partial class CMUPhotoClientSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IStateManager _state = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    /// <summary>
    /// Where the thumbnail sits on the print's 32x32 sprite: centred across, its middle two pixels above the
    /// sprite's (photo.rsi's window, drawn by polaroid_pixel_art.py's ITEM_WINDOW).
    /// </summary>
    private static readonly Vector2 ThumbnailOffset = new(0, 2f / 32f);

    private readonly Dictionary<int, Texture> _textures = new();
    private readonly Dictionary<int, Texture> _thumbnails = new();

    /// <summary>A capture waiting for the camera's flash to light the view before it is taken.</summary>
    private sealed class FlashWait(CMUPhotoCaptureRequestEvent request, TimeSpan giveUp)
    {
        public readonly CMUPhotoCaptureRequestEvent Request = request;
        public readonly TimeSpan GiveUp = giveUp;
        /// <summary>Frames drawn since the flash's light was first seen on.</summary>
        public int LitFrames;
    }

    private readonly List<FlashWait> _flashWaits = new();
    private readonly HashSet<int> _requested = new();
    private CMUPhotoFrameOverlay _overlay = default!;

    /// <summary>Raised when a print's picture arrives from the server.</summary>
    public event Action<int>? PhotoLoaded;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<CMUPhotoCaptureRequestEvent>(OnCaptureRequest);
        SubscribeNetworkEvent<CMUPhotoImageEvent>(OnImage);
        SubscribeLocalEvent<CMUPhotoComponent, ComponentStartup>(OnPhotoStartup);
        SubscribeLocalEvent<CMUPhotoComponent, AfterAutoHandleStateEvent>(OnPhotoState);

        _overlay = new CMUPhotoFrameOverlay(EntityManager);
        _overlays.AddOverlay(_overlay);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay(_overlay);
    }

    private void OnPhotoStartup(Entity<CMUPhotoComponent> ent, ref ComponentStartup args)
    {
        UpdateThumbnail(ent);
    }

    private void OnPhotoState(Entity<CMUPhotoComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateThumbnail(ent);
    }

    /// <summary>Shows the picture, shrunk, in the print sprite's window; an unprinted one stays dark.</summary>
    private void UpdateThumbnail(Entity<CMUPhotoComponent> ent)
    {
        if (!TryComp(ent, out SpriteComponent? sprite))
            return;

        var texture = Thumbnail(ent.Comp);
        if (texture == null && !_sprite.LayerExists((ent, sprite), CMUPhotoVisualLayers.Thumbnail))
            return;

        var layer = _sprite.LayerMapReserve((ent, sprite), CMUPhotoVisualLayers.Thumbnail);
        _sprite.LayerSetTexture((ent, sprite), layer, texture);
        _sprite.LayerSetOffset((ent, sprite), layer, ThumbnailOffset);
        _sprite.LayerSetVisible((ent, sprite), layer, texture != null);
        UpdateThumbnailGhosts((ent, sprite), ent.Comp, texture != null);
    }

    /// <summary>
    /// The print's ghosts, shrunk with the picture onto its thumbnail: at that size the blur is lost and each is
    /// a faint smudge where it stood.
    /// </summary>
    private void UpdateThumbnailGhosts(Entity<SpriteComponent> sprite, CMUPhotoComponent photo, bool shown)
    {
        var size = Math.Max(1, photo.Size);
        // Thumbnail pixels per tile of the picture, and the most a ghost's centre may move off the window's.
        var perTile = CMUPhotoLimits.ThumbnailPixels / (float) size;
        var limit = (CMUPhotoLimits.ThumbnailPixels - perTile) / 2f;
        for (var i = 0; i < CMUPhotoLimits.Ghosts; i++)
        {
            var key = $"cmu-photo-ghost-{i}";
            var ghost = shown && i < photo.Ghosts.Count ? photo.Ghosts[i] : null;
            if (ghost == null || GhostTexture(ghost) is not { } texture)
            {
                if (_sprite.LayerMapTryGet(sprite.AsNullable(), key, out var unused, false))
                    _sprite.LayerSetVisible(sprite.AsNullable(), unused, false);
                continue;
            }

            var layer = _sprite.LayerMapReserve(sprite.AsNullable(), key);
            var offset = Vector2.Clamp(ghost.Offset * perTile, new Vector2(-limit), new Vector2(limit));
            _sprite.LayerSetTexture(sprite.AsNullable(), layer, texture);
            _sprite.LayerSetScale(sprite.AsNullable(), layer, new Vector2(perTile / CMUPhotoLimits.TilePixels));
            _sprite.LayerSetOffset(sprite.AsNullable(), layer, ThumbnailOffset + offset / CMUPhotoLimits.TilePixels);
            _sprite.LayerSetColor(sprite.AsNullable(), layer, CMUPhotoGhostLayer.Tint.WithAlpha(ghost.Opacity));
            _sprite.LayerSetVisible(sprite.AsNullable(), layer, true);
        }
    }

    /// <summary>The first frame of a ghost's own sprite, or nothing if its prototype is gone.</summary>
    public Texture? GhostTexture(CMUPhotoGhost ghost)
    {
        return _prototypes.TryIndex(ghost.Prototype, out var proto) ? _sprite.Frame0(proto) : null;
    }

    private Texture? Thumbnail(CMUPhotoComponent photo)
    {
        if (photo.Thumbnail.Length == 0)
            return null;

        if (_thumbnails.TryGetValue(photo.Id, out var texture))
            return texture;

        try
        {
            texture = Texture.LoadFromPNGStream(new MemoryStream(photo.Thumbnail), $"CMU photo thumbnail {photo.Id}");
        }
        catch (Exception e)
        {
            Log.Error($"Could not load the thumbnail of photo {photo.Id}: {e.Message}");
            return null;
        }

        _thumbnails[photo.Id] = texture;
        return texture;
    }

    /// <summary>The camera in the local player's active hand, if any.</summary>
    public Entity<CMUPhotoCameraComponent>? HeldCamera()
    {
        if (_player.LocalEntity is not { } local
            || !_hands.TryGetActiveItem(local, out var item)
            || !TryComp(item, out CMUPhotoCameraComponent? camera))
            return null;

        return (item.Value, camera);
    }

    public EntityUid? HeldCameraUser()
    {
        return _player.LocalEntity;
    }

    /// <summary>A print's picture, fetched from the server the first time it is asked for.</summary>
    public Texture? GetPhoto(int id)
    {
        if (_textures.TryGetValue(id, out var texture))
            return texture;

        if (_requested.Add(id))
            RaiseNetworkEvent(new CMUPhotoImageRequestEvent(id));

        return null;
    }

    private void OnImage(CMUPhotoImageEvent ev)
    {
        try
        {
            _textures[ev.Photo] = Texture.LoadFromPNGStream(new MemoryStream(ev.Png), $"CMU photo {ev.Photo}");
        }
        catch (Exception e)
        {
            Log.Error($"Could not load photo {ev.Photo}: {e.Message}");
            return;
        }

        PhotoLoaded?.Invoke(ev.Photo);
    }

    private void OnCaptureRequest(CMUPhotoCaptureRequestEvent ev)
    {
        if (ev.Flash == null)
        {
            Capture(ev);
            return;
        }

        // The flash comes in the game state, which can land a tick after this event: wait for it.
        _flashWaits.Add(new FlashWait(ev, _timing.RealTime + CMUPhotoLimits.FlashWait));
        _overlay.Hidden = true;
    }

    /// <summary>
    /// Takes each waiting capture once its flash has been lit for a whole drawn frame, so the light is in the
    /// picture. If the flash never arrives the picture is taken anyway, as it is.
    /// </summary>
    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        for (var i = _flashWaits.Count - 1; i >= 0; i--)
        {
            var wait = _flashWaits[i];
            if (FlashLit(wait.Request.Flash))
                wait.LitFrames++;

            // Lit in two updates means a frame has been drawn with the light in it.
            if (wait.LitFrames < 2 && _timing.RealTime < wait.GiveUp)
                continue;

            _flashWaits.RemoveAt(i);
            Capture(wait.Request);
        }
    }

    private bool FlashLit(NetEntity? flash)
    {
        return TryGetEntity(flash, out var uid)
               && TryComp(uid, out PointLightComponent? light)
               && light.Enabled;
    }

    private void Capture(CMUPhotoCaptureRequestEvent ev)
    {
        if (_state.CurrentState is not IMainViewportState state)
        {
            _overlay.Hidden = false;
            RaiseNetworkEvent(new CMUPhotoCaptureResultEvent(ev.Request, Array.Empty<byte>()));
            return;
        }

        var viewport = state.Viewport.Viewport;
        var corners = FrameCorners(GetCoordinates(ev.Centre), ev.Size);

        // The outline would otherwise be in the picture.
        _overlay.Hidden = true;
        viewport.Screenshot(image =>
        {
            _overlay.Hidden = _flashWaits.Count > 0;
            var png = Crop(image, viewport, corners, ev.Size * CMUPhotoLimits.TilePixels);
            RaiseNetworkEvent(new CMUPhotoCaptureResultEvent(ev.Request, png));
        });
    }

    /// <summary>World positions of the frame's corners: the centre tile and (size - 1) / 2 tiles round it.</summary>
    public Vector2[] FrameCorners(EntityCoordinates centre, int size)
    {
        var half = size / 2f;
        var local = new[]
        {
            centre.Position + new Vector2(-half, -half),
            centre.Position + new Vector2(half, -half),
            centre.Position + new Vector2(half, half),
            centre.Position + new Vector2(-half, half),
        };

        var matrix = _transform.GetWorldMatrix(centre.EntityId);
        for (var i = 0; i < local.Length; i++)
            local[i] = Vector2.Transform(local[i], matrix);

        return local;
    }

    /// <summary>
    /// Cuts the frame out of the viewport screenshot and resamples it to the print size. Empty when the frame is
    /// not on screen, which the server reports to the photographer.
    /// </summary>
    private static byte[] Crop(Image<Rgba32> image, ScalingViewport viewport, Vector2[] corners, int size)
    {
        if (!Matrix3x2.Invert(viewport.GetLocalToScreenMatrix(), out var screenToLocal))
            return Array.Empty<byte>();

        var min = new Vector2(float.MaxValue);
        var max = new Vector2(float.MinValue);
        foreach (var corner in corners)
        {
            var pixel = Vector2.Transform(viewport.WorldToScreen(corner), screenToLocal);
            min = Vector2.Min(min, pixel);
            max = Vector2.Max(max, pixel);
        }

        // Clamped to the screen: a frame running off the edge is squeezed rather than refused.
        var x0 = Math.Clamp((int) MathF.Floor(min.X), 0, image.Width - 1);
        var y0 = Math.Clamp((int) MathF.Floor(min.Y), 0, image.Height - 1);
        var x1 = Math.Clamp((int) MathF.Ceiling(max.X), x0 + 1, image.Width);
        var y1 = Math.Clamp((int) MathF.Ceiling(max.Y), y0 + 1, image.Height);
        if (max.X <= 0 || max.Y <= 0 || min.X >= image.Width || min.Y >= image.Height)
            return Array.Empty<byte>();

        // Crop and Resize rather than reading pixels: the content sandbox does not allow the pixel indexer.
        using var print = image.Clone(context => context
            .Crop(new Rectangle(x0, y0, x1 - x0, y1 - y0))
            .Resize(size, size));

        using var stream = new MemoryStream();
        print.SaveAsPng(stream);
        return stream.ToArray();
    }
}
