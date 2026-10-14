using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.UserInterface;

/// <summary>
/// Reads the font IDs stored in CMU maps and markup until those formats use font paths directly.
/// </summary>
public static class CMUFontPrototypes
{
    public static ResPath GetPath(FontPrototype prototype)
    {
#pragma warning disable CS0618 // Existing font prototypes serialize their resource path in this engine field.
        return prototype.Path;
#pragma warning restore CS0618
    }

    public static Font CreateFont(Stack<Font> fonts, MarkupNode node, IResourceCache cache,
        IPrototypeManager prototypes, string fontId)
    {
        var size = FontTag.GetSizeForFontTag(fonts, node);
        if (IoCManager.Resolve<FontTagHijackHolder>().Hijack?.Invoke(fontId, size) is { } font)
            return font;

        if (!prototypes.TryIndex<FontPrototype>(fontId, out var prototype))
            prototype = prototypes.Index<FontPrototype>(FontTag.DefaultFont);

        return new VectorFont(cache.GetResource<FontResource>(GetPath(prototype)), size);
    }
}
