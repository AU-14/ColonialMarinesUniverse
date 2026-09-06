using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.IoC;

namespace Content.Client.Stylesheets
{
    public sealed partial class StylesheetManager : IStylesheetManager
    {
        [Dependency] private IUserInterfaceManager _userInterfaceManager = default!;
        [Dependency] private IResourceCache _resourceCache = default!;
        [Dependency] private IConfigurationManager _configurationManager = default!;

        public Stylesheet SheetNano { get; private set; } = default!;
        public Stylesheet SheetSpace { get; private set; } = default!;

        /// <inheritdoc />
        public event Action? ChatFontChanged;

        public void Initialize()
        {
            StyleNano.SetCrtUiEnabled(_configurationManager.GetCVar(CCVars.CrtUiEnabled));
            StyleNano.SetCrtPalette(_configurationManager.GetCVar(CCVars.CrtUiColor));
            StyleNano.SetChatReadableFont(_configurationManager.GetCVar(CCVars.CMUChatReadableFont));
            StyleNano.SetChatFontStep(
                StyleNano.ParseChatFontStep(_configurationManager.GetCVar(CCVars.CMUChatBigFont)));
            RefreshNanoSheet();
            SheetSpace = new StyleSpace(_resourceCache).Stylesheet;

            _configurationManager.OnValueChanged(CCVars.CrtUiEnabled, OnCrtUiEnabledChanged);
            _configurationManager.OnValueChanged(CCVars.CrtUiColor, OnCrtUiColorChanged);
            _configurationManager.OnValueChanged(CCVars.CMUChatReadableFont, OnChatReadableFontChanged);
            _configurationManager.OnValueChanged(CCVars.CMUChatBigFont, OnChatBigFontChanged);
        }

        public void PreviewCrtUi(bool enabled, string color)
        {
            StyleNano.SetCrtUiEnabled(enabled);
            StyleNano.SetCrtPalette(color);
            RefreshNanoSheet();
        }

        public void ResetCrtUiPreview()
        {
            StyleNano.SetCrtUiEnabled(_configurationManager.GetCVar(CCVars.CrtUiEnabled));
            StyleNano.SetCrtPalette(_configurationManager.GetCVar(CCVars.CrtUiColor));
            RefreshNanoSheet();
        }

        private void OnCrtUiEnabledChanged(bool enabled)
        {
            StyleNano.SetCrtUiEnabled(enabled);
            RefreshNanoSheet();
        }

        private void OnCrtUiColorChanged(string color)
        {
            StyleNano.SetCrtPalette(color);
            RefreshNanoSheet();
        }

        /// <summary>
        ///     Rebuilding the sheet is only half of it - message rows and the channel prompt bake a
        ///     FontOverride at construction and will not pick a new one up. ChatBox listens to the
        ///     same cvar and rebuilds itself; see ChatBox.OnChatReadableFontChanged. Everything
        ///     outside chat is caught by the full refresh below.
        /// </summary>
        private void OnChatReadableFontChanged(bool enabled)
        {
            StyleNano.SetChatReadableFont(enabled);
            ApplyChatFontChange();
        }

        private void OnChatBigFontChanged(string setting)
        {
            StyleNano.SetChatFontStep(StyleNano.ParseChatFontStep(setting));
            ApplyChatFontChange();
        }

        /// <summary>
        ///     The shared tail of both chat font options.
        /// </summary>
        /// <remarks>
        ///     Order is the point: statics, then sheet, then restyle, and only then chat. Listening to
        ///     the cvars directly let chat rebuild first, which left the controls that bake a
        ///     FontOverride at the old size while the message bodies moved with the sheet.
        /// </remarks>
        private void ApplyChatFontChange()
        {
            RefreshNanoSheet();
            RefreshOpenUi();
            ChatFontChanged?.Invoke();
        }

        /// <summary>
        ///     Make every control already on screen re-read the stylesheet.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///     Swapping <see cref="IUserInterfaceManager.Stylesheet"/> is not by itself enough for
        ///     anything already built and sitting there: a control that has run its style update
        ///     once has no reason to run it again. This walks every root and forces it, which is
        ///     what makes an options toggle land on windows that are open behind the options menu
        ///     rather than only on things opened afterwards.
        ///     </para>
        ///     <para>
        ///     Restyling only. It deliberately does not re-run the CRT theme pass, which would hand
        ///     CRT typography to the windows that opt out of it on purpose - the admin-help
        ///     conversation windows are readable prose and are meant to stay in a proportional face.
        ///     </para>
        ///     <para>
        ///     Not called from <see cref="RefreshNanoSheet"/> itself, because that runs on every
        ///     tick of the colour picker's preview and a whole-tree restyle per tick is not free.
        ///     </para>
        /// </remarks>
        private void RefreshOpenUi()
        {
            foreach (var root in _userInterfaceManager.AllRoots)
            {
                root.InvalidateStyleSheet();
                root.ForceRunStyleUpdate();
            }
        }

        private void RefreshNanoSheet()
        {
            SheetNano = new StyleNano(_resourceCache).Stylesheet;
            _userInterfaceManager.Stylesheet = SheetNano;
        }
    }
}
