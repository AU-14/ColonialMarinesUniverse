using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Client._CMU14.Interface;
using Content.Client.Stylesheets.Stylesheets;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.Configuration;
using Robust.Shared.IoC;
using Robust.Shared.Reflection;

namespace Content.Client.Stylesheets
{
    public sealed partial class StylesheetManager : IStylesheetManager
    {
        [Dependency] private IConfigurationManager _configurationManager = default!;
        [Dependency] private ILogManager _logManager = default!;
        [Dependency] private IReflectionManager _reflection = default!;
        [Dependency] private IResourceCache _resCache = default!;
        [Dependency] private ISystemFontManager _systemFonts = default!;
        [Dependency] private IUserInterfaceManager _userInterfaceManager = default!;

        public Stylesheet SheetNanotrasen { get; private set; } = default!;
        public Stylesheet SheetSystem { get; private set; } = default!;

        [Obsolete("Update to use SheetNanotrasen instead")]
        public Stylesheet SheetNano { get; private set; } = default!;

        [Obsolete("Update to use SheetSystem instead")]
        public Stylesheet SheetSpace { get; private set; } = default!;

        private Dictionary<string, Stylesheet> Stylesheets { get; set; } = default!;

        public bool TryGetStylesheet(string name, [MaybeNullWhen(false)] out Stylesheet stylesheet)
        {
            return Stylesheets.TryGetValue(name, out stylesheet);
        }

        public HashSet<Type> UnusedSheetlets { get; private set; } = [];
        /// <inheritdoc />
        public event Action? ChatFontChanged;
        public event Action? CrtThemeChanged;

        private CmuUiFonts? _uiFonts;
        private CmuUiFonts UiFonts => _uiFonts ??= new CmuUiFonts(_resCache, _systemFonts);

        public Font ApplyUiFont(Font original, string primaryPath, int size) => UiFonts.Wrap(original, primaryPath, size);

        private void OnUiFontChanged(string family)
        {
            UiFonts.Select(family);
            var oldNano = SheetNanotrasen;
            var oldSystem = SheetSystem;
            SheetNanotrasen = new NanotrasenStylesheet(new BaseStylesheet.NoConfig(), this).Stylesheet;
            SheetSystem = new SystemStylesheet(new BaseStylesheet.NoConfig(), this).Stylesheet;
            Stylesheets["Nanotrasen"] = SheetNanotrasen;
            Stylesheets["System"] = SheetSystem;
            foreach (var root in _userInterfaceManager.AllRoots)
                RefreshFontOverrides(root, oldNano, oldSystem);
            RefreshCrtTheme();
            IoCManager.Resolve<FontTagHijackHolder>().HijackUpdated();
        }

        private void RefreshFontOverrides(Control control, Stylesheet oldNano, Stylesheet oldSystem)
        {
            if (ReferenceEquals(control.Stylesheet, oldNano))
                control.Stylesheet = SheetNanotrasen;
            else if (ReferenceEquals(control.Stylesheet, oldSystem))
                control.Stylesheet = SheetSystem;
            if (control is Label { FontOverride: { } font } label)
                label.FontOverride = UiFonts.Refresh(font);
            foreach (var child in control.Children)
                RefreshFontOverrides(child, oldNano, oldSystem);
        }

        public void Initialize()
        {
            var sawmill = _logManager.GetSawmill("style");
            sawmill.Debug("Initializing Stylesheets...");
            var sw = Stopwatch.StartNew();

            // add all sheetlets to the hashset
            var tys = _reflection.FindTypesWithAttribute<CommonSheetletAttribute>();
            UnusedSheetlets = [..tys];

            UiFonts.Select(_configurationManager.GetCVar(CCVars.CMUUiFont));
            Stylesheets = new Dictionary<string, Stylesheet>();
            SheetNanotrasen = Init(new NanotrasenStylesheet(new BaseStylesheet.NoConfig(), this));
            SheetSystem = Init(new SystemStylesheet(new BaseStylesheet.NoConfig(), this));

            StyleNano.SetCrtUiEnabled(_configurationManager.GetCVar(CCVars.CrtUiEnabled));
            StyleNano.SetCrtPalette(_configurationManager.GetCVar(CCVars.CrtUiColor));
            StyleNano.SetChatReadableFont(_configurationManager.GetCVar(CCVars.CMUChatReadableFont));
            StyleNano.SetChatFontStep(
                StyleNano.ParseChatFontStep(_configurationManager.GetCVar(CCVars.CMUChatBigFont)));
            RefreshNanoSheet();
            SheetSpace = new StyleSpace(_resCache).Stylesheet; // TODO: REMOVE (obsolete)

            _configurationManager.OnValueChanged(CCVars.CMUUiFont, OnUiFontChanged);
            _configurationManager.OnValueChanged(CCVars.CrtUiEnabled, OnCrtUiEnabledChanged);
            _configurationManager.OnValueChanged(CCVars.CrtUiColor, OnCrtUiColorChanged);

            // warn about unused sheetlets
            if (UnusedSheetlets.Count > 0)
            {
                var sheetlets = UnusedSheetlets.AsEnumerable()
                    .Take(5)
                    .Select(t => t.FullName ?? "<could not get FullName>")
                    .ToArray();
                sawmill.Error($"There are unloaded sheetlets: {string.Join(", ", sheetlets)}");
            }

            sawmill.Debug($"Initialized {_styleRuleCount} style rules in {sw.Elapsed}");
            _configurationManager.OnValueChanged(CCVars.CMUChatReadableFont, OnChatReadableFontChanged);
            _configurationManager.OnValueChanged(CCVars.CMUChatBigFont, OnChatBigFontChanged);
        }

        public void PreviewCrtUi(bool enabled, string color)
        {
            StyleNano.SetCrtUiEnabled(enabled);
            StyleNano.SetCrtPalette(color);
            RefreshCrtTheme();
        }

        public void ResetCrtUiPreview()
        {
            StyleNano.SetCrtUiEnabled(_configurationManager.GetCVar(CCVars.CrtUiEnabled));
            StyleNano.SetCrtPalette(_configurationManager.GetCVar(CCVars.CrtUiColor));
            RefreshCrtTheme();
        }

        private void OnCrtUiEnabledChanged(bool enabled)
        {
            StyleNano.SetCrtUiEnabled(enabled);
            RefreshCrtTheme();
        }

        private void OnCrtUiColorChanged(string color)
        {
            StyleNano.SetCrtPalette(color);
            RefreshCrtTheme();
        }

        private void RefreshCrtTheme()
        {
            RefreshNanoSheet();
            CrtThemeChanged?.Invoke();
            RefreshOpenUi();
        }

        /// <summary>
        ///     Rebuilding the sheet is only half of it - message rows and the channel prompt bake a
        ///     FontOverride at construction and will not pick a new one up. ChatBox listens to the
        ///     ChatFontChanged event and rebuilds itself. Everything
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
            var previous = SheetNano;
            var legacyNano = new StyleNano(_resCache).Stylesheet;
            SheetNano = new Stylesheet(
                SheetNanotrasen.Rules.Concat(legacyNano.Rules).ToArray());
            _userInterfaceManager.Stylesheet = SheetNano;
            if (previous != null)
            {
                foreach (var root in _userInterfaceManager.AllRoots)
                    ReplaceLegacySheet(root, previous);
            }
        }

        private void ReplaceLegacySheet(Control control, Stylesheet previous)
        {
            // Windows with an explicit sheet do not inherit the manager's replacement.
            if (ReferenceEquals(control.Stylesheet, previous))
                control.Stylesheet = SheetNano;
            foreach (var child in control.Children)
                ReplaceLegacySheet(child, previous);
        }

        private int _styleRuleCount;

        private Stylesheet Init(BaseStylesheet baseSheet)
        {
            Stylesheets.Add(baseSheet.StylesheetName, baseSheet.Stylesheet);
            _styleRuleCount += baseSheet.Stylesheet.Rules.Count;
            return baseSheet.Stylesheet;
        }
    }
}
