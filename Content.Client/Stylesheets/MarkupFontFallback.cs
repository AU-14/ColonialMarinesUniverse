using Content.Client.Resources;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Stylesheets;

/// <summary>
/// This prevents fonts from rendering as '?'
/// Markup like [head][bold][font] etc. do not get fallbacks whereas the stylesheet fonts do.
/// </summary>
public static class MarkupFontFallback
{
    private static readonly ResPath Symbols = new("/Fonts/NotoSans/NotoSansSymbols-Regular.ttf");
    private static readonly ResPath Symbols2 = new("/Fonts/NotoSans/NotoSansSymbols2-Regular.ttf");

    public static void Register()
    {
        var cache = IoCManager.Resolve<IResourceCache>();
        var prototypes = IoCManager.Resolve<IPrototypeManager>();

        IoCManager.Resolve<FontTagHijackHolder>().Hijack = (fontId, size) =>
        {
            if (!prototypes.TryIndex(fontId, out FontPrototype? proto))
                proto = prototypes.Index<FontPrototype>(FontTag.DefaultFont);

            return cache.GetFont(new[] { proto.Path, Symbols, Symbols2 }, size);
        };
    }
}
