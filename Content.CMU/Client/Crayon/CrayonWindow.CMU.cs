using System.Numerics;
using Content.Client.CMU14.Crayon;
using Content.Client.Stylesheets;
using Content.Shared.CMU14.Crayon;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Crayon.UI;

public sealed partial class CrayonWindow
{
    [Dependency] private IClyde _cmuClyde = default!;

    private readonly CMUCrayonSymbolStorage _cmuStorage = new();
    private readonly List<OwnedTexture> _cmuTextures = new();
    private string? _cmuSelected;

    public event Action<CMUCrayonSymbol>? OnCustomSelected;

    private void CMUAddCustomSymbols()
    {
        CMUReleaseTextures();
        var grid = new GridContainer { Columns = 6, Margin = new Thickness(0, 0, 0, 8) };
        foreach (var (name, symbol) in _cmuStorage.Symbols)
        {
            var texture = CMUCrayonSymbolImage.ToTexture(_cmuClyde, symbol);
            _cmuTextures.Add(texture);
            var scale = 2f * CMUCrayonSymbol.Small / symbol.Size;
            var button = new TextureButton { TextureNormal = texture, ToolTip = name, Scale = new Vector2(scale, scale) };
            button.OnPressed += _ => CMUSelect(name, symbol);
            grid.AddChild(_cmuSelected == name
                ? new PanelContainer
                {
                    PanelOverride = new StyleBoxFlat { BackgroundColor = StyleNano.ButtonColorDefault },
                    Children = { button },
                }
                : button);
        }

        var create = new Button { Text = Loc.GetString("cmu-crayon-custom-new") };
        create.OnPressed += _ => CMUOpenCanvas(null);
        var edit = new Button { Text = Loc.GetString("cmu-crayon-custom-edit"), Disabled = _cmuSelected == null };
        edit.OnPressed += _ => CMUOpenCanvas(_cmuSelected);
        var delete = new Button { Text = Loc.GetString("cmu-crayon-custom-delete"), Disabled = _cmuSelected == null };
        delete.OnPressed += _ =>
        {
            if (_cmuSelected == null)
                return;

            _cmuStorage.Delete(_cmuSelected);
            _cmuSelected = null;
            RefreshList();
        };

        Grids.AddChild(new Label { Text = Loc.GetString("cmu-crayon-custom-category") });
        Grids.AddChild(grid);
        Grids.AddChild(new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 4,
            Margin = new Thickness(0, 0, 0, 16),
            Children = { create, edit, delete },
        });
    }

    private void CMUSelect(string name, CMUCrayonSymbol symbol)
    {
        _cmuSelected = name;
        _selected = null;
        OnCustomSelected?.Invoke(symbol);
        RefreshList();
    }

    private void CMUOpenCanvas(string? name)
    {
        var canvas = new CMUCrayonCanvasWindow();
        if (name != null && _cmuStorage.Symbols.Find(s => s.Name == name) is { Symbol: { } symbol })
            canvas.Load(name, symbol);

        canvas.OnSaved += (saved, symbol) =>
        {
            _cmuStorage.Save(saved, symbol);
            CMUSelect(saved, symbol);
        };
        canvas.OpenCentered();
    }

    private void CMUReleaseTextures()
    {
        foreach (var texture in _cmuTextures)
        {
            texture.Dispose();
        }

        _cmuTextures.Clear();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            CMUReleaseTextures();
    }
}
