using Robust.Client.UserInterface;

namespace Content.Client.Stylesheets
{
    public interface IStylesheetManager
    {
        Stylesheet SheetNano { get; }
        Stylesheet SheetSpace { get; }

        /// <summary>
        ///     Raised after a chat font option has been applied to the sheet and the open tree
        ///     restyled, never before, so chat cannot rebuild against a sheet that has not caught up.
        /// </summary>
        event Action? ChatFontChanged;

        void Initialize();
        void PreviewCrtUi(bool enabled, string color);
        void ResetCrtUiPreview();
    }
}
