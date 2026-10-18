using System.Linq;
using Content.Client.Humanoid;
using Content.Shared.Body;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Preferences;
using System.Numerics;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;

namespace Content.Client.CMU14.MagicMirror;

/// <summary>
///     The regulation scissors' menu: the same regulation haircut and colour lists as the character editor's
///     Regulation Appearance tab, instead of the full marking editor.
/// </summary>
public sealed class CMURegulationMirrorWindow : DefaultWindow
{
    private readonly RegulationMarkingPicker _hair;
    private readonly RegulationMarkingPicker _facialHair;

    private Dictionary<ProtoId<OrganCategoryPrototype>, OrganMarkingData> _organs = new();
    private Dictionary<ProtoId<OrganCategoryPrototype>, Dictionary<HumanoidVisualLayers, List<Marking>>> _applied = new();
    private ProtoId<SpeciesPrototype> _species = HumanoidCharacterProfile.DefaultSpecies;
    private Sex _sex;

    private string _hairStyle = HairStyles.DefaultHairStyle;
    private Color _hairColor = HairStyles.RegulationHairColors[0].Color;
    private string _facialHairStyle = HairStyles.DefaultFacialHairStyle;
    private Color _facialHairColor = HairStyles.RegulationHairColors[0].Color;

    /// <summary>Raised with the whole set of markings to apply.</summary>
    public event Action<Dictionary<ProtoId<OrganCategoryPrototype>, Dictionary<HumanoidVisualLayers, List<Marking>>>>? OnCut;

    public CMURegulationMirrorWindow()
    {
        MinSize = new Vector2(700, 600);

        _hair = new RegulationMarkingPicker
        {
            Layer = HumanoidVisualLayers.Hair,
            MarkingWhitelist = HairStyles.RegulationHairStyles.Select(style => style.Id).ToHashSet(),
            DropdownColors = HairStyles.RegulationHairColors,
            DefaultMarkingId = HairStyles.DefaultHairStyle,
        };
        _facialHair = new RegulationMarkingPicker
        {
            Layer = HumanoidVisualLayers.FacialHair,
            MarkingWhitelist = HairStyles.RegulationFacialHairStyles.Select(style => style.Id).ToHashSet(),
            DropdownColors = HairStyles.RegulationHairColors,
            DefaultMarkingId = HairStyles.DefaultFacialHairStyle,
        };

        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 8,
        };
        row.AddChild(_hair);
        row.AddChild(_facialHair);
        Contents.AddChild(row);

        _hair.OnMarkingChanged += style => { _hairStyle = style; Cut(); };
        _hair.OnColorChanged += color => { _hairColor = color; Cut(); };
        _facialHair.OnMarkingChanged += style => { _facialHairStyle = style; Cut(); };
        _facialHair.OnColorChanged += color => { _facialHairColor = color; Cut(); };
    }

    public void Update(
        Dictionary<ProtoId<OrganCategoryPrototype>, OrganMarkingData> organs,
        Dictionary<ProtoId<OrganCategoryPrototype>, Dictionary<HumanoidVisualLayers, List<Marking>>> applied,
        ProtoId<SpeciesPrototype> species,
        Sex sex)
    {
        _organs = organs;
        _applied = applied;
        _species = species;
        _sex = sex;

        (_hairStyle, _hairColor) = Current(HumanoidVisualLayers.Hair, HairStyles.DefaultHairStyle, _hairColor);
        (_facialHairStyle, _facialHairColor) =
            Current(HumanoidVisualLayers.FacialHair, HairStyles.DefaultFacialHairStyle, _facialHairColor);

        _hair.UpdateData(_hairStyle, _hairColor, _species, _sex);
        _facialHair.UpdateData(_facialHairStyle, _facialHairColor, _species, _sex);
    }

    private (string Style, Color Color) Current(HumanoidVisualLayers layer, string fallback, Color fallbackColor)
    {
        foreach (var layers in _applied.Values)
        {
            if (!layers.TryGetValue(layer, out var markings) || markings.Count == 0)
                continue;

            var marking = markings[0];
            var color = marking.MarkingColors.Count > 0 ? marking.MarkingColors[0] : fallbackColor;
            return (marking.MarkingId.Id, color);
        }

        return (fallback, fallbackColor);
    }

    private void Cut()
    {
        var markings = _applied.ToDictionary(
            organ => organ.Key,
            organ => organ.Value.ToDictionary(layer => layer.Key, layer => layer.Value.ToList()));

        SetLayer(markings, HumanoidVisualLayers.Hair, _hairStyle, HairStyles.DefaultHairStyle, _hairColor);
        SetLayer(markings, HumanoidVisualLayers.FacialHair, _facialHairStyle, HairStyles.DefaultFacialHairStyle, _facialHairColor);
        OnCut?.Invoke(markings);
    }

    private void SetLayer(
        Dictionary<ProtoId<OrganCategoryPrototype>, Dictionary<HumanoidVisualLayers, List<Marking>>> markings,
        HumanoidVisualLayers layer,
        string style,
        string none,
        Color color)
    {
        foreach (var (organ, data) in _organs)
        {
            if (!data.Layers.Contains(layer))
                continue;

            if (!markings.TryGetValue(organ, out var layers))
                markings[organ] = layers = new Dictionary<HumanoidVisualLayers, List<Marking>>();

            layers[layer] = style == none
                ? new List<Marking>()
                : new List<Marking> { new(style, new[] { color }) };
            return;
        }
    }
}
