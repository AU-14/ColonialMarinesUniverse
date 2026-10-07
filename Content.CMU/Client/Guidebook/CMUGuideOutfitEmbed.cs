using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Content.Client.Guidebook.Richtext;
using Content.Client.Lobby;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Inventory;
using Content.Shared.Lobby;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Client.CMU14.Guidebook;

/// <summary>
///     Guidebook tag that shows a randomly generated human wearing a starting gear set,
///     e.g. &lt;CMUGuideOutfitEmbed Gear="AU14GearICRCDoctor" Caption="Doctor"/&gt;.
///     Third parties only get their gear when a player takes them over, so their spawn entities can't be embedded directly.
///     The human gets the same military haircuts, hair colours and black eyes that military third parties spawn with,
///     the species' default skin tone and no facial hair.
/// </summary>
[UsedImplicitly]
public sealed partial class CMUGuideOutfitEmbed : BoxContainer, IDocumentTag
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;
    [Dependency] private MarkingManager _markings = default!;
    [Dependency] private IRobustRandom _random = default!;

    // Mirrors the RandomHumanoidAppearanceWhitelisted lists on the military third party bases.
    private static readonly string[] MaleHair =
    [
        "RMCHumanHairCrew", "RMCHumanHairAviator", "RMCHumanHairJensen", "RMCHumanHairAverageJoe", "AU14HumanHairCIA5",
        "RMCHumanHairCombover", "RMCHumanHairCombover2", "RMCHumanHairCplDietrich", "RMCHumanHairCut",
        "RMCHumanHairHeadStubble", "RMCHumanHairHighAndTight", "RMCHumanHairHighfade", "RMCHumanHairHighlight",
        "AU14HumanHairIRS", "RMCHumanHairLtRasczak", "RMCHumanHairMarineFade", "RMCHumanHairMarineFlatTop",
        "RMCHumanHairMedfade", "RMCHumanHairNofade", "RMCHumanHairShavedpart", "RMCHumanHairShavedHead",
        "RMCHumanHairShort", "RMCHumanHairSideUndercutHang", "RMCHumanHairSkinhead", "RMCHumanHairTaper",
        "RMCHumanHairWardaddy", "RMCHumanHairUndercut", "RMCHumanHairUndercutTop",
    ];

    private static readonly string[] FemaleHair =
    [
        "RMCHumanHairShortbangs", "RMCHumanHairBobcurl", "RMCHumanHairMarineBun", "RMCHumanHairBun",
        "RMCHumanHairFringetail", "AU14HumanHairLowBun", "AU14HumanHairLowPonyTail", "AU14HumanHairLowPonyTailAlt",
        "RMCHumanHairPonytail1", "RMCHumanHairPonytail7", "RMCHumanHairPvtRedding", "RMCHumanHairPvtVasquez",
        "RMCHumanHairLong", "RMCHumanHairPixieCutLeft", "RMCHumanHairPixieCutRight",
    ];

    private static readonly string[] HairColors =
    [
        "#0B0B0B", "#1A1A1A", "#222222", "#2B1B0F", "#3B2314", "#4A2E1A",
        "#5A3A22", "#6B4A2F", "#9C8651", "#B79A5A", "#4A1F12", "#7A5E42",
    ];

    private EntityUid? _dummy;

    public CMUGuideOutfitEmbed()
    {
        IoCManager.InjectDependencies(this);
        Orientation = LayoutOrientation.Vertical;
        Margin = new Thickness(4, 8);
        HorizontalAlignment = HAlignment.Center;
    }

    public bool TryParseTag(Dictionary<string, string> args, [NotNullWhen(true)] out Control? control)
    {
        control = null;
        if (!args.TryGetValue("Gear", out var gearId) ||
            !_prototypes.TryIndex<StartingGearPrototype>(gearId, out var gear))
        {
            return false;
        }

        var lobby = _ui.GetUIController<LobbyUIController>();
        var dummy = lobby.LoadProfileEntity(MilitaryProfile(), null, false);
        _dummy = dummy;
        Equip(dummy, gear);

        var view = new SpriteView
        {
            Scale = new Vector2(2, 2),
            OverrideDirection = Direction.South,
            SetSize = new Vector2(96, 96),
        };
        view.SetEntity(dummy);
        AddChild(view);

        if (args.TryGetValue("Caption", out var caption) && caption.Length > 0)
        {
            AddChild(new Label
            {
                Text = caption,
                HorizontalAlignment = HAlignment.Center,
            });
        }

        control = this;
        return true;
    }

    private HumanoidCharacterProfile MilitaryProfile()
    {
        var profile = HumanoidCharacterProfile.RandomWithSpecies();
        var hair = _random.Pick(profile.Sex == Sex.Female ? FemaleHair : MaleHair);
        var hairColor = Color.FromHex(_random.Pick(HairColors));

        profile = WithMarking(profile, HumanoidVisualLayers.Hair, hair, hairColor);
        profile = WithoutLayer(profile, HumanoidVisualLayers.FacialHair);
        profile = profile.WithCharacterAppearance(profile.Appearance.WithEyeColor(Color.Black));

        // Generic default skin tone for the species, so the showcase focuses on the gear.
        var defaultSkin = HumanoidCharacterAppearance.DefaultWithSpecies(profile.Species, profile.Sex).SkinColor;
        profile = profile.WithCharacterAppearance(profile.Appearance.WithSkinColor(defaultSkin));

        var appearance = HumanoidCharacterAppearance.EnsureValid(profile.Appearance, profile.Species, profile.Sex);
        return profile.WithCharacterAppearance(appearance);
    }

    private static HumanoidCharacterProfile WithoutLayer(HumanoidCharacterProfile profile, HumanoidVisualLayers layer)
    {
        var markings = profile.Appearance.Markings.ToDictionary(
            organPair => organPair.Key,
            organPair => organPair.Value
                .Where(layerPair => layerPair.Key != layer)
                .ToDictionary(layerPair => layerPair.Key, layerPair => layerPair.Value.ToList()));

        return profile.WithCharacterAppearance(profile.Appearance.WithMarkings(markings));
    }

    private HumanoidCharacterProfile WithMarking(HumanoidCharacterProfile profile, HumanoidVisualLayers layer, string markingId, Color color)
    {
        if (!_prototypes.TryIndex<MarkingPrototype>(markingId, out var prototype) || prototype.BodyPart != layer)
            return profile;

        var (organ, data) = _markings.GetMarkingData(profile.Species).FirstOrDefault(pair => pair.Value.Layers.Contains(layer));
        if (organ == default || !_markings.CanBeApplied(data.Group, profile.Sex, prototype))
            return profile;

        var markings = profile.Appearance.Markings.ToDictionary(
            organPair => organPair.Key,
            organPair => organPair.Value.ToDictionary(layerPair => layerPair.Key, layerPair => layerPair.Value.ToList()));

        var organMarkings = markings.GetValueOrDefault(organ) ?? new();
        markings[organ] = organMarkings;
        organMarkings[layer] = new List<Marking> { prototype.AsMarking().WithColor(color) };

        return profile.WithCharacterAppearance(profile.Appearance.WithMarkings(markings));
    }

    private void Equip(EntityUid dummy, StartingGearPrototype gear)
    {
        var inventory = _entities.System<InventorySystem>();
        if (!inventory.TryGetSlots(dummy, out var slots))
            return;

        foreach (var slot in slots)
        {
            var itemType = ((IEquipmentLoadout) gear).GetGear(slot.Name);
            if (itemType == string.Empty)
                continue;

            if (inventory.TryUnequip(dummy, slot.Name, out var old, silent: true, force: true, reparent: false))
                _entities.DeleteEntity(old.Value);

            var item = _entities.SpawnEntity(itemType, MapCoordinates.Nullspace);
            _entities.EnsureComponent<LobbyPreviewEntityComponent>(item);
            inventory.TryEquip(dummy, item, slot.Name, true, true);
        }
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();
        if (_dummy is { } dummy && _entities.EntityExists(dummy))
            _entities.DeleteEntity(dummy);

        _dummy = null;
    }
}
