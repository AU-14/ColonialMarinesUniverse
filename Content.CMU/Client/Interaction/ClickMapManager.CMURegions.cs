using Robust.Client.Graphics;
using Robust.Shared.Graphics.RSI;

namespace Content.Client.Clickable;

internal sealed partial class ClickMapManager
{
    private readonly Dictionary<Texture, CMURsiRegion> _cmuRsiRegions = new();
    private Texture? _cmuSolidTexture;

    // Keep the same texture instance for procedural artwork and its hit tests on every renderer.
    public Texture CMUSolidTexture => _cmuSolidTexture ??= Texture.White;

    /// <summary>Reuse an RSI's alpha map for a cropped, independently animated texture.</summary>
    public void RegisterRsiRegion(Texture texture, RSI rsi, RSI.StateId state, RsiDirection direction, UIBox2i region)
    {
        if (!_rsiMaps.TryGetValue(rsi, out var data) || !data.Offsets.TryGetValue(state, out var offsets))
            return;

        _cmuRsiRegions[texture] = new CMURsiRegion(data.ClickMap, region.Size,
            offsets[(int) direction][0] + region.TopLeft);
    }

    private bool IsCMURsiRegionOccluding(Texture texture, Vector2i pos)
    {
        return _cmuRsiRegions.TryGetValue(texture, out var region)
               && SampleClickMap(region.Map, pos, region.Size, region.Offset);
    }

    private readonly record struct CMURsiRegion(ClickMap Map, Vector2i Size, Vector2i Offset);
}
