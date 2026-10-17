using System.Numerics;
using Content.Client.CMU14.Crayon;
using Content.Client.Stylesheets;
using Content.Shared.CMU14.Crayon;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client.Crayon.UI;

public sealed partial class CrayonWindow
{
    [Dependency] private IClyde _cmuClyde = default!;
    [Dependency] private IPlayerManager _cmuPlayer = default!;

    private readonly CMUCrayonSymbolStorage _cmuStorage = new();
    private readonly List<OwnedTexture> _cmuTextures = new();

    // The player's stencil as this window last heard it from the server, and as it stands after
    // what was picked here since. The server has the final say whenever it sends a change.
    private CMUCrayonSymbol? _cmuSeen;
    private CMUCrayonSymbol? _cmuStencil;

    // The saved symbol that draws the stencil, if one does.
    private string? _cmuSelected;

    public event Action<CMUCrayonSymbol>? OnCustomSelected;

    private void CMUAddCustomSymbols()
    {
        CMUSyncStencil();
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
            // The selected symbol is the one being drawn, so the crayon goes back to its stock decal
            // rather than stamping a symbol no longer listed.
            if (_selected != null)
            {
                CMUClear();
                OnSelected?.Invoke(_selected);
            }

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
        _cmuStencil = symbol;
        // A search left in the box would otherwise pick its stock decal again on the next refresh.
        _autoSelected = null;
        OnCustomSelected?.Invoke(symbol);
        RefreshList();
    }

    /// <summary>
    /// A stock decal was picked here, which the server also takes as clearing the stencil.
    /// </summary>
    private void CMUClear()
    {
        _cmuStencil = null;
        _cmuSelected = null;
    }

    /// <summary>
    /// Takes the stencil the server last sent if it changed, from this window or any other crayon's.
    /// </summary>
    private bool CMUSyncStencil()
    {
        CMUCrayonSymbol? stencil = null;
        if (_cmuPlayer.LocalEntity is { } player)
            _entitySystem.GetEntitySystem<CMUCrayonStencilSystem>().TryGetStencil(player, out stencil);

        if (ReferenceEquals(stencil, _cmuSeen))
            return false;

        _cmuSeen = stencil;
        _cmuStencil = stencil;
        _cmuSelected = CMUNameOf(stencil);
        return true;
    }

    private string? CMUNameOf(CMUCrayonSymbol? stencil)
    {
        if (stencil == null)
            return null;

        // Two saved symbols can draw the same; keep the one picked.
        if (_cmuSelected != null && _cmuStorage.Symbols.Find(s => s.Name == _cmuSelected).Symbol?.SameAs(stencil) == true)
            return _cmuSelected;

        foreach (var (name, symbol) in _cmuStorage.Symbols)
        {
            if (symbol.SameAs(stencil))
                return name;
        }

        return null;
    }

    private void CMUOpenCanvas(string? name)
    {
        var canvas = new CMUCrayonCanvasWindow(_color);
        if (name != null && _cmuStorage.Symbols.Find(s => s.Name == name) is { Symbol: { } symbol })
            canvas.Load(name, symbol);

        canvas.OnSaved += (saved, symbol) =>
        {
            _cmuStorage.Save(saved, symbol);
            // This window goes when its crayon leaves the hand, but the drawing stays open: the symbol is
            // still saved, there is just nothing left to select it on.
            if (!Disposed)
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

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (CMUSyncStencil())
            RefreshList();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            CMUReleaseTextures();
    }
}
