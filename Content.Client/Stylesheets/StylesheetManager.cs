using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.IoC;

namespace Content.Client.Stylesheets
{
    public sealed class StylesheetManager : IStylesheetManager
    {
        [Dependency] private readonly IUserInterfaceManager _userInterfaceManager = default!;
        [Dependency] private readonly IResourceCache _resourceCache = default!;
        [Dependency] private readonly IConfigurationManager _configurationManager = default!;

        public Stylesheet SheetNano { get; private set; } = default!;
        public Stylesheet SheetSpace { get; private set; } = default!;

        public void Initialize()
        {
            StyleNano.SetCrtPalette(_configurationManager.GetCVar(CCVars.CrtUiColor));
            RefreshNanoSheet();
            SheetSpace = new StyleSpace(_resourceCache).Stylesheet;

            _configurationManager.OnValueChanged(CCVars.CrtUiColor, OnCrtUiColorChanged);
        }

        private void OnCrtUiColorChanged(string color)
        {
            StyleNano.SetCrtPalette(color);
            RefreshNanoSheet();
        }

        private void RefreshNanoSheet()
        {
            SheetNano = new StyleNano(_resourceCache).Stylesheet;
            _userInterfaceManager.Stylesheet = SheetNano;
        }
    }
}
