using System.Numerics;
using Content.Client.Body;
using Content.Client.CMU14.Interface;
using Content.Shared.CMU14.CriminalRecords;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Lobby;
using Content.Shared.Preferences;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Client.CMU14.CriminalRecords;

/// <summary>
///     A mugshot drawn from a record's portrait: the person's body and appearance from when they arrived, in their
///     underwear and facing forward. The body is a local preview dummy, removed when the photo leaves the screen.
/// </summary>
public sealed partial class CMURecordPortraitView : PanelContainer
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    private readonly SpriteView _view;
    private readonly Label _missing;
    private EntityUid? _dummy;

    public CMURecordPortraitView(Vector2 size, Font font)
    {
        IoCManager.InjectDependencies(this);
        MinSize = size;
        SetSize = size;
        PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = CrtTerminalPalette.Surface1,
            BorderColor = CrtTerminalPalette.Line,
            BorderThickness = new Thickness(1),
        };

        _view = new SpriteView
        {
            OverrideDirection = Direction.South,
            Stretch = SpriteView.StretchMode.Fill,
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        AddChild(_view);

        _missing = new Label
        {
            Text = Loc.GetString("cmu-records-no-photo"),
            FontOverride = font,
            FontColorOverride = CrtTerminalPalette.TextDim,
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
        };
        AddChild(_missing);
    }

    public void SetPortrait(CMURecordPortrait? portrait)
    {
        RemoveDummy();
        _missing.Visible = portrait == null;
        _view.Visible = portrait != null;
        if (portrait == null)
            return;

        var species = _prototypes.TryIndex<SpeciesPrototype>(portrait.Species, out var found)
            ? found
            : _prototypes.Index<SpeciesPrototype>(HumanoidCharacterProfile.DefaultSpecies);

        var dummy = _entities.SpawnEntity(species.DollPrototype, MapCoordinates.Nullspace);
        _entities.EnsureComponent<LobbyPreviewEntityComponent>(dummy);
        var profile = HumanoidCharacterProfile.DefaultWithSpecies(species.ID, portrait.Sex)
            .WithCharacterAppearance(portrait.Appearance);
        _entities.System<VisualBodySystem>().ApplyProfileTo(dummy, profile);
        _entities.System<HumanoidProfileSystem>().ApplyProfileTo(dummy, profile);

        _dummy = dummy;
        _view.SetEntity(dummy);
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();
        RemoveDummy();
    }

    private void RemoveDummy()
    {
        _view.SetEntity((EntityUid?) null);
        if (_dummy is { } dummy && !_entities.Deleted(dummy))
            _entities.DeleteEntity(dummy);

        _dummy = null;
    }
}
