using System.Linq;
using Content.Client.Lobby.UI;
using Content.Shared.Body;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Client.Utility;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Humanoid;

/// <summary>
/// A narrow organ-layer picker for the separate CMU regulation appearance fields.
/// Normal profile markings continue to use <see cref="MarkingsViewModel"/>.
/// </summary>
public sealed class RegulationMarkingPicker : BoxContainer
{
    [Dependency] private MarkingManager _markingManager = default!;
    [Dependency] private IEntityManager _entityManager = default!;

    private readonly SpriteSystem _sprite;
    private readonly Label _layerLabel;
    private readonly LineEdit _markingSearch;
    private readonly ItemList _markingList;
    private readonly LineEdit _colorSearch;
    private readonly ItemList _colorList;

    private IReadOnlyDictionary<string, MarkingPrototype> _markings =
        new Dictionary<string, MarkingPrototype>();
    private string _selectedMarking = string.Empty;
    private Color _selectedColor;
    private bool _updating;

    private HumanoidVisualLayers _layer;
    public HumanoidVisualLayers Layer
    {
        get => _layer;
        set
        {
            _layer = value;
            _layerLabel.Text = Loc.GetString($"markings-layer-{value}");
        }
    }

    public HashSet<string> MarkingWhitelist { get; set; } = [];
    public IReadOnlyList<(string Name, Color Color)> DropdownColors { get; set; } = [];
    public string DefaultMarkingId { get; set; } = string.Empty;

    public event Action<string>? OnMarkingChanged;
    public event Action<Color>? OnColorChanged;

    public RegulationMarkingPicker()
    {
        Orientation = LayoutOrientation.Vertical;
        HorizontalExpand = true;

        IoCManager.InjectDependencies(this);
        _sprite = _entityManager.System<SpriteSystem>();

        _layerLabel = new Label();
        _markingSearch = new LineEdit
        {
            PlaceHolder = Loc.GetString("markings-search"),
            HorizontalExpand = true,
        };
        _markingList = new ItemList
        {
            MinHeight = 250,
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        _colorSearch = new LineEdit
        {
            PlaceHolder = Loc.GetString("markings-search"),
            HorizontalExpand = true,
        };
        _colorList = new ItemList
        {
            MinHeight = 250,
            HorizontalExpand = true,
            VerticalExpand = true,
        };

        AddChild(_layerLabel);
        AddChild(_markingSearch);
        AddChild(_markingList);
        AddChild(_colorSearch);
        AddChild(_colorList);

        _markingSearch.OnTextChanged += args => PopulateMarkings(args.Text);
        _colorSearch.OnTextChanged += args => PopulateColors(args.Text);
        _markingList.OnItemSelected += SelectMarking;
        _colorList.OnItemSelected += SelectColor;

        CrtLobbyTheme.Apply(this);
    }

    public void UpdateData(
        string markingId,
        Color color,
        ProtoId<SpeciesPrototype> species,
        Sex sex)
    {
        _selectedMarking = markingId;
        _selectedColor = color;

        OrganMarkingData? organData = _markingManager.GetMarkingData(species)
            .Values
            .FirstOrNull(data => data.Layers.Contains(Layer));

        if (organData is not { } data)
        {
            _markings = new Dictionary<string, MarkingPrototype>();
        }
        else
        {
            _markings = _markingManager.MarkingsByLayerAndGroupAndSex(Layer, data.Group, sex)
                .Where(entry => MarkingWhitelist.Contains(entry.Key))
                .ToDictionary(entry => entry.Key, entry => entry.Value);
        }

        Visible = _markings.Count > 0;
        PopulateMarkings(_markingSearch.Text);
        PopulateColors(_colorSearch.Text);
    }

    private void PopulateMarkings(string filter)
    {
        _updating = true;
        _markingList.Clear();

        var defaultName = Loc.GetString($"marking-{DefaultMarkingId}");
        if (defaultName.Contains(filter, StringComparison.OrdinalIgnoreCase))
        {
            var item = _markingList.AddItem(defaultName);
            item.Metadata = DefaultMarkingId;
            item.Selected = _selectedMarking == DefaultMarkingId;
        }

        foreach (var marking in _markings.Values
                     .Where(marking => Loc.GetString($"marking-{marking.ID}")
                         .Contains(filter, StringComparison.OrdinalIgnoreCase))
                     .OrderBy(marking => Loc.GetString($"marking-{marking.ID}")))
        {
            var item = _markingList.AddItem(
                Loc.GetString($"marking-{marking.ID}"),
                _sprite.Frame0(marking.Sprites[0]));
            item.Metadata = marking.ID;
            item.Selected = _selectedMarking == marking.ID;
        }

        _updating = false;
    }

    private void PopulateColors(string filter)
    {
        _updating = true;
        _colorList.Clear();

        var showColors = _selectedMarking != DefaultMarkingId;
        _colorSearch.Visible = showColors;
        _colorList.Visible = showColors;

        if (showColors)
        {
            foreach (var (name, color) in DropdownColors
                         .Where(entry => entry.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                         .OrderBy(entry => entry.Name))
            {
                var item = _colorList.AddItem(name, Texture.White);
                item.IconModulate = color;
                item.Metadata = color;
                item.Selected = color == _selectedColor;
            }
        }

        _updating = false;
    }

    private void SelectMarking(ItemList.ItemListSelectedEventArgs args)
    {
        if (_updating)
            return;

        _selectedMarking = (string) _markingList[args.ItemIndex].Metadata!;
        PopulateColors(_colorSearch.Text);
        OnMarkingChanged?.Invoke(_selectedMarking);
    }

    private void SelectColor(ItemList.ItemListSelectedEventArgs args)
    {
        if (_updating)
            return;

        _selectedColor = (Color) _colorList[args.ItemIndex].Metadata!;
        OnColorChanged?.Invoke(_selectedColor);
    }
}
