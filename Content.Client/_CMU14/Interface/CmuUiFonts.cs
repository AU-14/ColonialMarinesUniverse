using System.Globalization;
using System.Linq;
using Content.Client.Stylesheets.Fonts;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;

namespace Content.Client._CMU14.Interface;

/// <summary>
/// Applies the player's font preference to UI font stacks, including fonts held by open controls.
/// </summary>
internal sealed class CmuUiFonts(IResourceCache resources, ISystemFontManager systemFonts)
{
    private readonly Dictionary<(int Size, FontKind Kind), Font> _fonts = new();
    private ISystemFontFace[]? _comicSansFaces;
    private string _family = CCVars.CMUUiFontTheme;
    private readonly List<FontRequest> _requests = new();
    private int _requestsSincePrune;

    public void Select(string family)
    {
        family = family switch
        {
            CCVars.CMUUiFontNotoSans or CCVars.CMUUiFontComicSans or CCVars.CMUUiFontRobotoMono or
                CCVars.CMUUiFontCozette or CCVars.CMUUiFontNotoSansDisplay => family,
            _ => CCVars.CMUUiFontTheme,
        };

        if (_family == family)
            return;

        _family = family;
        _fonts.Clear();
    }

    public Font Wrap(Font original, string primaryPath, int size)
    {
        // Symbol and decorative fonts keep their meaning when the text face changes.
        if (primaryPath.Contains("Symbols", StringComparison.Ordinal) ||
            !(primaryPath.StartsWith("/Fonts/NotoSans", StringComparison.Ordinal) ||
              primaryPath.StartsWith("/EngineFonts/NotoSans/", StringComparison.Ordinal) ||
              primaryPath.StartsWith("/Fonts/UAVOSD/", StringComparison.Ordinal) ||
              primaryPath.StartsWith("/Fonts/RobotoMono/", StringComparison.Ordinal) ||
              primaryPath.StartsWith("/Fonts/Cozette/", StringComparison.Ordinal)))
            return original;

        var bold = primaryPath.Contains("Bold", StringComparison.Ordinal);
        var italic = primaryPath.Contains("Italic", StringComparison.Ordinal);
        var kind = (bold, italic) switch
        {
            (true, true) => FontKind.BoldItalic,
            (true, false) => FontKind.Bold,
            (false, true) => FontKind.Italic,
            _ => FontKind.Regular,
        };

        // The existing OSD layout uses 8pt where ordinary fonts use 12pt.
        var preferredSize = primaryPath.StartsWith("/Fonts/UAVOSD/", StringComparison.Ordinal)
            ? (int) Math.Round(size * 1.5f)
            : size;
        var selected = Resolve(original, preferredSize, kind);
        // Keep engine font types intact: markup reads VectorFont.Size from a stack's first face.
        var result = ReferenceEquals(selected, original) ? original : new StackedFont(((StackedFont) selected).Stack);
        // WeakReference is supported by the content sandbox. Do not retain an original font
        // strongly when it is also the weak key, otherwise unused theme fonts never expire.
        if (++_requestsSincePrune >= 128)
        {
            _requests.RemoveAll(request => !request.Font.TryGetTarget(out _));
            _requestsSincePrune = 0;
        }
        _requests.Add(new FontRequest(new WeakReference<Font>(result),
            ReferenceEquals(result, original) ? null : original, primaryPath, size));
        return result;
    }

    private Font Resolve(Font original, int size, FontKind kind)
    {
        if (_family == CCVars.CMUUiFontTheme)
            return original;

        if (_fonts.TryGetValue((size, kind), out var cached))
            return cached;

        var variant = kind.AsFileName();
        var path = _family switch
        {
            CCVars.CMUUiFontComicSans => $"/Fonts/ComicNeue/ComicNeue-{variant}.ttf",
            CCVars.CMUUiFontRobotoMono => $"/Fonts/RobotoMono/RobotoMono-{kind.SimplifyCompound().AsFileName()}.ttf",
            CCVars.CMUUiFontCozette => $"/Fonts/Cozette/CozetteVector{(kind == FontKind.Regular ? "" : kind.SimplifyCompound().AsFileName())}.ttf",
            CCVars.CMUUiFontNotoSansDisplay => $"/Fonts/NotoSansDisplay/NotoSansDisplay-{variant}.ttf",
            _ => $"/Fonts/NotoSans/NotoSans-{variant}.ttf",
        };
        var primary = _family == CCVars.CMUUiFontComicSans ? LoadComicSans(size, kind) : null;
        primary ??= new VectorFont(resources.GetResource<FontResource>(path), size);
        var symbols = kind.IsBold() ? "Bold" : "Regular";
        var font = new StackedFont(
            primary,
            new VectorFont(resources.GetResource<FontResource>($"/Fonts/NotoSans/NotoSans-{variant}.ttf"), size),
            new VectorFont(resources.GetResource<FontResource>($"/Fonts/NotoSans/NotoSansSymbols-{symbols}.ttf"), size),
            new VectorFont(resources.GetResource<FontResource>("/Fonts/NotoSans/NotoSansSymbols2-Regular.ttf"), size));
        _fonts.Add((size, kind), font);
        return font;
    }

    private Font? LoadComicSans(int size, FontKind kind)
    {
        if (!systemFonts.IsSupported)
            return null;

        _comicSansFaces ??= systemFonts.SystemFontFaces.Where(face =>
            face.GetLocalizedFamilyName(CultureInfo.InvariantCulture)
                .Equals("Comic Sans MS", StringComparison.OrdinalIgnoreCase)).ToArray();
        var face = _comicSansFaces.FirstOrDefault(face =>
            (face.Weight >= FontWeight.Bold) == kind.IsBold() &&
            (face.Slant != FontSlant.Normal) == kind.IsItalic());
        return face?.Load(size);
    }

    public Font Refresh(Font font)
    {
        foreach (var request in _requests)
        {
            if (request.Font.TryGetTarget(out var target) && ReferenceEquals(target, font))
                return Wrap(request.Original ?? font, request.PrimaryPath, request.Size);
        }
        return font;
    }

    private sealed record FontRequest(WeakReference<Font> Font, Font? Original, string PrimaryPath, int Size);
}
