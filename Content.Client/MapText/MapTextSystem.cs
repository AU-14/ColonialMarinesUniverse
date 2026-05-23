using Content.Shared.MapText;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Client.MapText;

/// <inheritdoc/>
public sealed partial class MapTextSystem : SharedMapTextSystem
{
    [Dependency] private IConfigurationManager _configManager = default!;
    [Dependency] private IUserInterfaceManager _uiManager = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IResourceCache _resourceCache = default!;
    [Dependency] private IOverlayManager _overlayManager = default!;

    private static readonly IReadOnlyDictionary<string, ResPath> FontPaths = new Dictionary<string, ResPath>
    {
        ["Default"] = new("/Fonts/NotoSans/NotoSans-Regular.ttf"),
        ["DefaultItalic"] = new("/Fonts/NotoSans/NotoSans-Italic.ttf"),
        ["DefaultBold"] = new("/Fonts/NotoSans/NotoSans-Bold.ttf"),
        ["DefaultBoldItalic"] = new("/Fonts/NotoSans/NotoSans-BoldItalic.ttf"),
        ["NotoSansDisplay"] = new("/Fonts/NotoSansDisplay/NotoSansDisplay-Regular.ttf"),
        ["NotoSansDisplayItalic"] = new("/Fonts/NotoSansDisplay/NotoSansDisplay-Italic.ttf"),
        ["NotoSansDisplayBold"] = new("/Fonts/NotoSansDisplay/NotoSansDisplay-Bold.ttf"),
        ["NotoSansDisplayBoldItalic"] = new("/Fonts/NotoSansDisplay/NotoSansDisplay-BoldItalic.ttf"),
        ["BoxRound"] = new("/Fonts/Boxfont-round/Boxfont Round.ttf"),
        ["Cozette"] = new("/Fonts/Cozette/CozetteVector.ttf"),
        ["CozetteBold"] = new("/Fonts/Cozette/CozetteVectorBold.ttf"),
        ["AnimalSilence"] = new("/Fonts/Animal Silence.otf"),
        ["Monospace"] = new("/EngineFonts/NotoSans/NotoSansMono-Regular.ttf"),
        ["Emoji"] = new("/Fonts/NotoEmoji.ttf"),
    };

    private MapTextOverlay _overlay = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<MapTextComponent, ComponentStartup>(OnComponentStartup);
        SubscribeLocalEvent<MapTextComponent, ComponentHandleState>(HandleCompState);

        _overlay = new MapTextOverlay(_configManager, EntityManager, _uiManager, _transform);
        _overlayManager.AddOverlay(_overlay);

        DebugTools.Assert(FontPaths.ContainsKey(SharedMapTextComponent.DefaultFont));
    }

    private void OnComponentStartup(Entity<MapTextComponent> ent, ref ComponentStartup args)
    {
        CacheText(ent.Comp);
        DebugTools.Assert(FontPaths.ContainsKey(ent.Comp.FontId));
    }

    private void HandleCompState(Entity<MapTextComponent> ent, ref ComponentHandleState args)
    {
        if (args.Current is not MapTextComponentState state)
            return;

        ent.Comp.Text = state.Text;
        ent.Comp.LocText = state.LocText;
        ent.Comp.Color = state.Color;
        ent.Comp.FontId = state.FontId;
        ent.Comp.FontSize = state.FontSize;
        ent.Comp.Offset = state.Offset;

        CacheText(ent.Comp);
    }

    private void CacheText(MapTextComponent component)
    {
        component.CachedFont = null;

        component.CachedText = string.IsNullOrWhiteSpace(component.Text)
            ? Loc.GetString(component.LocText)
            : component.Text;

        component.CachedFont = CreateFont(component.FontId, component.FontSize);
        if (component.CachedFont == null)
        {
            component.CachedText = Loc.GetString("map-text-font-error");
            component.Color = Color.Red;
            component.CachedFont = CreateFont(SharedMapTextComponent.DefaultFont, 14);
            return;
        }
    }

    private VectorFont? CreateFont(string fontId, int size)
    {
        if (!FontPaths.TryGetValue(fontId, out var path))
            return null;

        return new VectorFont(_resourceCache.GetResource<FontResource>(path), size);
    }
}
