using Content.Client.Viewport;
using Robust.Shared.Graphics;

namespace Content.Client.CMU14.ZLevels.Core;

public static class CMUViewportEyeExtensions
{
    /// <summary>
    /// The player's own view: their eye, or the final <see cref="ScalingViewport.ZEye"/> pass derived from it.
    /// </summary>
    public static bool IsPlayerView(this IEye? viewportEye, IEye playerEye)
    {
        return ReferenceEquals(viewportEye, playerEye) ||
               (viewportEye is ScalingViewport.ZEye { IsFinalPass: true } zEye && ReferenceEquals(zEye.SourceEye, playerEye));
    }
}
