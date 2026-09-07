using System.Diagnostics.CodeAnalysis;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client.Stylesheets;

public interface IStylesheetManager
{
    /// Nanotrasen styles: the default style! Use this for most UIs
    Stylesheet SheetNanotrasen { get; }

    ///
    /// System styles: use this for any admin / debug menus, and any odds and ends (like the changelog for some reason)
    ///
    Stylesheet SheetSystem { get; }


    [Obsolete("Update to use SheetNanotrasen instead")]
    Stylesheet SheetNano { get; }

    [Obsolete("Update to use SheetSystem instead")]
    Stylesheet SheetSpace { get; }

    /// get a stylesheet by name
    public bool TryGetStylesheet(string name, [MaybeNullWhen(false)]  out Stylesheet stylesheet);

    Font ApplyUiFont(Font original, string primaryPath, int size);

    void Initialize();
    void PreviewCrtUi(bool enabled, string color);
    void ResetCrtUiPreview();

    /// <summary>
    /// Raised after chat fonts and open controls have been restyled.
    /// </summary>
    event Action? ChatFontChanged;

    /// <summary>
    /// Raised after the CRT theme or its preview changes, with the new stylesheet available.
    /// </summary>
    event Action? CrtThemeChanged;

    ///
    /// Sheetlets marked with CommonSheetlet that have not satisfied the type constraints of any stylesheet
    ///
    public HashSet<Type> UnusedSheetlets { get; }
}
