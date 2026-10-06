using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Client.Guidebook.Richtext;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.Guidebook;

/// <summary>
///     Guidebook tag that embeds an image, e.g. &lt;CMUGuideImage Path="/ServerInfo/Guidebook/x.png"/&gt;.
///     Scales down to fit the page width while keeping its aspect ratio, but never scales up.
///     An optional MaxHeight (in pixels) shrinks it further, e.g. to keep a logo small.
/// </summary>
[UsedImplicitly]
public sealed partial class CMUGuideImage : TextureRect, IDocumentTag
{
    [Dependency] private IResourceCache _resources = default!;

    private float _maxHeight = float.PositiveInfinity;

    public CMUGuideImage()
    {
        IoCManager.InjectDependencies(this);
        Stretch = StretchMode.KeepAspectCentered;
        HorizontalExpand = true;
        HorizontalAlignment = HAlignment.Center;
        Margin = new Thickness(0, 4);
    }

    public bool TryParseTag(Dictionary<string, string> args, [NotNullWhen(true)] out Control? control)
    {
        control = null;
        if (!args.TryGetValue("Path", out var path))
            return false;

        if (!_resources.TryGetResource<TextureResource>(new ResPath(path), out var texture))
            return false;

        if (args.TryGetValue("MaxHeight", out var maxHeight) && float.TryParse(maxHeight, out var parsed) && parsed > 0)
            _maxHeight = parsed;

        Texture = texture;
        control = this;
        return true;
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        if (Texture == null)
            return Vector2.Zero;

        var size = Texture.Size * TextureScale;
        var scale = float.IsFinite(availableSize.X) && size.X > availableSize.X
            ? availableSize.X / size.X
            : 1f;
        scale = MathF.Min(scale, _maxHeight / size.Y);

        return size * scale;
    }
}
