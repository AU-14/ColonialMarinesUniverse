using System.Numerics;
using Content.Shared.CMU14.Photography;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.Photography;

/// <summary>
/// The ghosts that stood in a print's frame, drawn over its picture: each its own sprite, faint at the opacity
/// rolled when the photo was taken, and blurred by laying many dim copies round where it stood. The picture's
/// pixels are never touched; the ghosts come up with it as it develops.
/// </summary>
public sealed class CMUPhotoGhostLayer : Control
{
    /// <summary>A cold cast, so ghosts read as something on the film rather than in the room.</summary>
    public static readonly Color Tint = Color.FromHex("#DCE8FF");

    /// <summary>
    /// Blur taps: the ghost's place, then two rings round it, in picture pixels, each with its share of the
    /// ghost's opacity. Where every copy overlaps the ghost shows at exactly its rolled opacity; the core carries
    /// most of it so the ghost keeps its shape, and the rings soften it into a haze.
    /// </summary>
    private static readonly (Vector2 Offset, float Share)[] Taps = BuildTaps();

    private readonly CMUPhotoView _view;
    private readonly CMUPhotoComponent _photo;
    private readonly CMUPhotoClientSystem _photos;

    public CMUPhotoGhostLayer(CMUPhotoView view, CMUPhotoComponent photo)
    {
        _view = view;
        _photo = photo;
        _photos = IoCManager.Resolve<IEntityManager>().System<CMUPhotoClientSystem>();
        MouseFilter = MouseFilterMode.Ignore;
        RectClipContent = true;
    }

    private static (Vector2, float)[] BuildTaps()
    {
        var taps = new List<(Vector2, float)> { (Vector2.Zero, 0.55f) };
        foreach (var (radius, count, turn, share) in new[] { (1.2f, 8, 0f, 0.3f), (2.6f, 12, 0.13f, 0.15f) })
        {
            for (var i = 0; i < count; i++)
            {
                var angle = MathF.Tau * (i / (float) count + turn);
                taps.Add((new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius, share / count));
            }
        }

        return taps.ToArray();
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        if (_photo.Ghosts.Count == 0 || _view.Texture == null)
            return;

        var size = Math.Max(1, _photo.Size);
        // Screen pixels per tile and per picture pixel.
        var perTile = PixelSize.X / (float) size;
        var perPixel = perTile / CMUPhotoLimits.TilePixels;
        var developed = _view.ModulateSelfOverride?.A ?? 1f;

        foreach (var ghost in _photo.Ghosts)
        {
            if (_photos.GhostTexture(ghost) is not { } texture)
                continue;

            var opacity = Math.Clamp(ghost.Opacity, 0f, 0.99f) * developed;

            var drawn = new Vector2(texture.Width, texture.Height) * perPixel;
            var centre = new Vector2((ghost.Offset.X + size / 2f) * perTile, (size / 2f - ghost.Offset.Y) * perTile);
            foreach (var (offset, share) in Taps)
            {
                // Shares multiply out in transparency: (1 - a1)(1 - a2)... = 1 - opacity.
                var alpha = 1f - MathF.Pow(1f - opacity, share);
                var topLeft = centre - drawn / 2f + offset * perPixel;
                handle.DrawTextureRect(texture, UIBox2.FromDimensions(topLeft, drawn), Tint.WithAlpha(alpha));
            }
        }
    }
}
