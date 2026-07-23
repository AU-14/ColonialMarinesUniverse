using System.Linq;
using System.Numerics;
using Content.Client.Humanoid;
using Content.Client.Stylesheets;
using Content.Shared._CMU14.Yautja;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Lobby;
using Content.Shared.Preferences;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.Utility;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._CMU14.Yautja.Lobby;

public sealed partial class YautjaProfileEditor : ScrollContainer
{
    private const int VisualButtonSize = 108;
    private const int VisualSpriteSize = 102;
    private const int LabeledVisualButtonSize = VisualButtonSize;
    private const int LabeledVisualSpriteSize = 86;
    private static readonly ProtoId<SpeciesPrototype> YautjaSpecies = "Yautja";
    private static readonly SoundPathSpecifier ModernCloakPreviewSound = new("/Audio/_CMU14/Yautja/pred_cloakon_modern.ogg");
    private static readonly SoundPathSpecifier RetroCloakPreviewSound = new("/Audio/_CMU14/Yautja/Equipment/pred_cloakon.wav");
    private static readonly ResPath BracerRsi = new("/Textures/_CMU14/Yautja/bracer.rsi");

    private readonly LineEdit _name = new();
    private readonly LineEdit _age = new();
    private readonly CheckBox _previewWithoutGear = new();
    private readonly OptionButton _translatorType = new();
    private readonly OptionButton _invisibilitySound = new();
    private readonly Label _translatorHelp = new();
    private readonly Label _invisibilityHelp = new();
    private readonly Label _flavorLimit = new();
    private readonly TextEdit _flavorText = new()
    {
        MinHeight = 90,
        HorizontalExpand = true,
        MaxLength = YautjaCharacterProfile.MaxFlavorTextLength,
    };

    private readonly GridContainer _skinGrid = new() { Columns = 6 };
    private readonly GridContainer _eyeGrid = new() { Columns = 7 };
    private readonly GridContainer _quillGrid = new() { Columns = 6 };
    private readonly GridContainer _legacyGrid = new() { Columns = 4 };
    private readonly GridContainer _uniqueGrid = new() { Columns = 4 };
    private readonly BoxContainer _armorSections = EquipmentSectionContainer();
    private readonly BoxContainer _maskSections = EquipmentSectionContainer();
    private readonly GridContainer _maskAccessoryGrid = new() { Columns = 4 };
    private readonly BoxContainer _greavesSections = EquipmentSectionContainer();
    private readonly BoxContainer _bracerSections = EquipmentSectionContainer();
    private readonly BoxContainer _casterSections = EquipmentSectionContainer();
    private readonly GridContainer _capeGrid = new() { Columns = 4 };
    private readonly TabContainer _categoryTabs = new()
    {
        HorizontalExpand = true,
        VerticalExpand = true,
        MinSize = new Vector2(720, 440),
    };

    private readonly SpriteView _preview = new()
    {
        MinSize = new Vector2(190, 230),
        Scale = new Vector2(4, 4),
        OverrideDirection = Direction.South,
        Stretch = SpriteView.StretchMode.Fit,
    };

    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;

    private readonly List<EntityUid> _selectorDummies = new();
    private HumanoidCharacterProfile? _profile;
    private EntityUid _previewDummy = EntityUid.Invalid;
    private Direction _previewRotation = Direction.South;
    private YautjaBracerMaterial? _bracerFilter;
    private YautjaBracerMaterial? _casterFilter;
    private bool _updating;

    public event Action<HumanoidCharacterProfile>? OnProfileChanged;

    public YautjaProfileEditor()
    {
        IoCManager.InjectDependencies(this);
        _previewWithoutGear.Text = Loc.GetString("cmu-yautja-lobby-preview-without-gear");
        _flavorText.Placeholder = new Rope.Leaf(Loc.GetString("cmu-yautja-lobby-flavor-placeholder"));
        _flavorText.ToolTip = Loc.GetString("cmu-yautja-lobby-flavor-limit-tooltip", ("max", YautjaCharacterProfile.MaxFlavorTextLength));
        _flavorLimit.FontColorOverride = Color.FromHex("#b8aaa0");
        UpdateFlavorLimit(0);

        HorizontalExpand = true;
        VerticalExpand = true;

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(10),
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 8,
        };
        AddChild(root);

        var identity = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            SeparationOverride = 6,
        };
        root.AddChild(identity);
        identity.AddChild(Row("cmu-yautja-lobby-name", _name));
        identity.AddChild(Row("cmu-yautja-lobby-age", _age));

        var colors = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            SeparationOverride = 12,
            Margin = new Thickness(0, 0, 0, 4),
        };
        root.AddChild(colors);
        colors.AddChild(VisualBlock("cmu-yautja-lobby-skin-color", _skinGrid));
        colors.AddChild(VisualBlock("cmu-yautja-lobby-eyes", _eyeGrid));

        var workArea = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 12,
        };
        root.AddChild(workArea);

        workArea.AddChild(new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            MinWidth = 210,
            Children =
            {
                new PanelContainer
                {
                    MinSize = new Vector2(210, 250),
                    Children = { _preview },
                },
                PreviewRotationControls(),
                _previewWithoutGear,
            },
        });

        workArea.AddChild(_categoryTabs);

        AddTab("cmu-yautja-lobby-quills", CategoryScroll(_quillGrid));
        AddTab("cmu-yautja-lobby-armor", CategoryScroll(_armorSections));
        AddTab("cmu-yautja-lobby-mask", CategoryScroll(_maskSections));
        AddTab("cmu-yautja-lobby-mask-accessory", CategoryScroll(_maskAccessoryGrid));
        AddTab("cmu-yautja-lobby-greaves", CategoryScroll(_greavesSections));
        AddTab("cmu-yautja-lobby-bracer", CategoryScroll(_bracerSections));
        AddTab("cmu-yautja-lobby-caster", CategoryScroll(_casterSections));
        AddTab("cmu-yautja-lobby-cape", CategoryScroll(_capeGrid));
        AddTab("cmu-yautja-lobby-sets", CategoryScroll(new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            Children =
            {
                VisualBlock("cmu-yautja-lobby-legacy", _legacyGrid),
                VisualBlock("cmu-yautja-lobby-unique", _uniqueGrid),
            },
        }));
        AddTab("cmu-yautja-lobby-tech", CategoryScroll(new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            Children =
            {
                TechOptionBlock(
                    "cmu-yautja-lobby-translator-type",
                    _translatorType,
                    _translatorHelp,
                    null),
                TechOptionBlock(
                    "cmu-yautja-lobby-invisibility-sound",
                    _invisibilitySound,
                    _invisibilityHelp,
                    () => PlayPreviewSound(GetInvisibilityPreviewSound(_invisibilitySound.SelectedId))),
            },
        }));
        AddTab("cmu-yautja-lobby-flavor", CategoryScroll(FlavorBlock()));

        AddTranslatorTypeOptions(_translatorType);
        AddInvisibilitySoundOptions(_invisibilitySound);

        _name.OnTextChanged += args => Mutate(profile => profile.WithName(args.Text));
        _age.OnTextChanged += args =>
        {
            if (int.TryParse(args.Text, out var age))
                Mutate(profile => profile.WithAge(age));
        };
        _previewWithoutGear.OnPressed += _ =>
        {
            if (_profile != null)
                ReloadPreview(_profile.YautjaProfile);
        };
        _flavorText.OnTextChanged += args => OnFlavorTextChanged(args.Control);
        _translatorType.OnItemSelected += args =>
        {
            _translatorType.SelectId(args.Id);
            UpdateTechHelp((YautjaTranslatorType) args.Id, (YautjaInvisibilitySound) _invisibilitySound.SelectedId);
            Mutate(profile => profile.WithTranslatorType((YautjaTranslatorType) args.Id));
        };
        _invisibilitySound.OnItemSelected += args =>
        {
            _invisibilitySound.SelectId(args.Id);
            UpdateTechHelp((YautjaTranslatorType) _translatorType.SelectedId, (YautjaInvisibilitySound) args.Id);
            PlayPreviewSound(GetInvisibilityPreviewSound(args.Id));
            Mutate(profile => profile.WithInvisibilitySound((YautjaInvisibilitySound) args.Id));
        };
    }

    public void SetProfile(HumanoidCharacterProfile? profile)
    {
        _profile = profile;
        _updating = true;

        var yautja = profile?.YautjaProfile ?? YautjaCharacterProfile.Default;
        _name.Text = yautja.Name;
        _age.Text = yautja.Age.ToString();
        _flavorText.TextRope = new Rope.Leaf(yautja.FlavorText);
        UpdateFlavorLimit(yautja.FlavorText.Length);
        _translatorType.SelectId((int) yautja.TranslatorType);
        _invisibilitySound.SelectId((int) yautja.InvisibilitySound);
        UpdateTechHelp(yautja.TranslatorType, yautja.InvisibilitySound);
        RebuildVisualSelectors(yautja);

        _updating = false;
        ReloadPreview(yautja);
    }

    private void Mutate(Func<YautjaCharacterProfile, YautjaCharacterProfile> update, bool rebuildSelectors = false)
    {
        if (_updating || _profile == null)
            return;

        var profile = _profile.WithYautjaProfile(update(_profile.YautjaProfile));
        _profile = profile;

        if (rebuildSelectors)
            RebuildVisualSelectors(profile.YautjaProfile);

        ReloadPreview(profile.YautjaProfile);
        OnProfileChanged?.Invoke(profile);
    }

    private void RebuildVisualSelectors(YautjaCharacterProfile yautja)
    {
        DisposeSelectorDummies();

        RebuildSkinSelector(yautja);
        RebuildEyeSelector(yautja);
        RebuildQuillSelector(yautja);
        RebuildLegacySelector(yautja);
        RebuildUniqueSelector(yautja);
        RebuildArmorSelector(yautja);
        RebuildMaskSelector(yautja);
        RebuildMaskAccessorySelector(yautja);
        RebuildGreavesSelector(yautja);
        RebuildBracerSelector(yautja);
        RebuildCasterSelector(yautja);
        RebuildCapeSelector(yautja);
    }

    private void RebuildSkinSelector(YautjaCharacterProfile yautja)
    {
        _skinGrid.RemoveAllChildren();
        var group = new ButtonGroup();

        foreach (var skinColor in YautjaCharacterProfile.SkinColorOrder)
        {
            var button = BuildSelectorButton(
                YautjaCharacterProfile.GetSkinColorDisplayName(skinColor),
                yautja.SkinColor == skinColor,
                group,
                new Vector2(42, 30));

            button.OnPressed += _ => Mutate(profile => profile.WithSkinColor(skinColor), true);
            button.AddChild(new PanelContainer
            {
                MinSize = new Vector2(30, 18),
                HorizontalAlignment = HAlignment.Center,
                VerticalAlignment = VAlignment.Center,
                PanelOverride = new StyleBoxFlat
                {
                    BackgroundColor = YautjaCharacterProfile.GetSkinColorColor(skinColor),
                    BorderColor = Color.FromHex("#1f1f1f"),
                    BorderThickness = new Thickness(1),
                },
            });
            _skinGrid.AddChild(button);
        }
    }

    private void RebuildEyeSelector(YautjaCharacterProfile yautja)
    {
        _eyeGrid.RemoveAllChildren();
        var group = new ButtonGroup();

        foreach (var eyeColor in YautjaCharacterProfile.EyeColorOrder)
        {
            var button = BuildSwatchButton(
                YautjaCharacterProfile.GetEyeColorDisplayName(eyeColor),
                yautja.EyeColor == eyeColor,
                group,
                YautjaCharacterProfile.GetEyeColorColor(eyeColor));

            button.OnPressed += _ => Mutate(profile => profile.WithEyeColor(eyeColor), true);
            _eyeGrid.AddChild(button);
        }
    }

    private void RebuildQuillSelector(YautjaCharacterProfile yautja)
    {
        _quillGrid.RemoveAllChildren();
        var group = new ButtonGroup();

        foreach (var quill in YautjaCharacterProfile.QuillStyleOrder)
        {
            var button = BuildSelectorButton(
                YautjaCharacterProfile.GetQuillStyleDisplayName(quill),
                yautja.QuillStyle == quill,
                group);

            button.OnPressed += _ => Mutate(profile => profile.WithQuillStyle(quill), true);
            if (BuildSelectorDoll(yautja.WithQuillStyle(quill)) is { } doll)
            {
                var view = new SpriteView
                {
                    MinSize = new Vector2(VisualSpriteSize, VisualSpriteSize),
                    OverrideDirection = Direction.South,
                    Scale = new Vector2(2.8f, 2.8f),
                    Stretch = SpriteView.StretchMode.Fill,
                };
                view.SetEntity(doll);
                button.AddChild(view);
            }
            else
            {
                button.Text = YautjaCharacterProfile.GetQuillStyleDisplayName(quill);
            }

            _quillGrid.AddChild(button);
        }
    }

    private void RebuildLegacySelector(YautjaCharacterProfile yautja)
    {
        _legacyGrid.RemoveAllChildren();
        var group = new ButtonGroup();

        foreach (var legacy in YautjaCharacterProfile.LegacyOrder)
        {
            var selected = yautja.Legacy == legacy;
            if (legacy == YautjaLegacySet.None)
            {
                AddTextSelector(_legacyGrid,
                    group,
                    YautjaCharacterProfile.GetLegacyDisplayName(legacy),
                    selected,
                    () => Mutate(profile => profile.WithLegacy(YautjaLegacySet.None), true));
                continue;
            }

            var preview = YautjaCharacterProfile.Default.WithLegacy(legacy).ArmorPrototype;
            AddEntitySelector(_legacyGrid,
                group,
                preview,
                selected,
                YautjaCharacterProfile.GetLegacyDisplayName(legacy),
                () => Mutate(profile => profile.WithLegacy(legacy).WithUnique(YautjaUniqueSet.None), true));
        }
    }

    private void RebuildUniqueSelector(YautjaCharacterProfile yautja)
    {
        _uniqueGrid.RemoveAllChildren();
        var group = new ButtonGroup();

        foreach (var unique in YautjaCharacterProfile.UniqueOrder)
        {
            var selected = yautja.Unique == unique;
            if (unique == YautjaUniqueSet.None)
            {
                AddTextSelector(_uniqueGrid,
                    group,
                    YautjaCharacterProfile.GetUniqueDisplayName(unique),
                    selected,
                    () => Mutate(profile => profile.WithUnique(YautjaUniqueSet.None), true));
                continue;
            }

            var preview = YautjaCharacterProfile.Default.WithUnique(unique).ArmorPrototype;
            AddEntitySelector(_uniqueGrid,
                group,
                preview,
                selected,
                YautjaCharacterProfile.GetUniqueDisplayName(unique),
                () => Mutate(profile => profile.WithUnique(unique).WithLegacy(YautjaLegacySet.None), true));
        }
    }

    private void RebuildArmorSelector(YautjaCharacterProfile yautja)
    {
        _armorSections.RemoveAllChildren();
        var group = new ButtonGroup();

        foreach (var material in YautjaCharacterProfile.MaterialOrder)
        {
            var grid = EquipmentGrid();
            for (var style = 1; style <= 8; style++)
            {
                var capturedStyle = style;
                var prototype = YautjaCharacterProfile.Default.WithArmor(material, style).ArmorPrototype;
                AddEntitySelector(grid,
                    group,
                    prototype,
                    yautja.Legacy == YautjaLegacySet.None &&
                    yautja.Unique == YautjaUniqueSet.None &&
                    yautja.ArmorMaterial == material &&
                    yautja.ArmorStyle == style,
                    YautjaCharacterProfile.GetArmorStyleDisplayName(material, style),
                    () => Mutate(profile => profile
                        .WithArmor(material, capturedStyle)
                        .WithLegacy(YautjaLegacySet.None)
                        .WithUnique(YautjaUniqueSet.None), true));
            }

            _armorSections.AddChild(EquipmentMaterialSection(MaterialTitle(material), grid));
        }
    }

    private void RebuildMaskSelector(YautjaCharacterProfile yautja)
    {
        _maskSections.RemoveAllChildren();
        var group = new ButtonGroup();

        foreach (var material in YautjaCharacterProfile.MaterialOrder)
        {
            var grid = EquipmentGrid();
            for (var style = 1; style <= 20; style++)
            {
                var capturedStyle = style;
                var prototype = YautjaCharacterProfile.Default.WithMask(material, style).MaskPrototype;
                AddEntitySelector(grid,
                    group,
                    prototype,
                    yautja.Legacy == YautjaLegacySet.None &&
                    yautja.Unique == YautjaUniqueSet.None &&
                    yautja.MaskMaterial == material &&
                    yautja.MaskStyle == style,
                    YautjaCharacterProfile.GetMaskStyleDisplayName(material, style),
                    () => Mutate(profile => profile
                        .WithMask(material, capturedStyle)
                        .WithLegacy(YautjaLegacySet.None)
                        .WithUnique(YautjaUniqueSet.None), true));
            }

            _maskSections.AddChild(EquipmentMaterialSection(MaterialTitle(material), grid));
        }
    }

    private void RebuildMaskAccessorySelector(YautjaCharacterProfile yautja)
    {
        _maskAccessoryGrid.RemoveAllChildren();
        var group = new ButtonGroup();

        AddTextSelector(_maskAccessoryGrid,
            group,
            YautjaCharacterProfile.GetMaskAccessoryDisplayName(0, yautja.MaskMaterial),
            yautja.MaskAccessoryStyle == 0,
            () => Mutate(profile => profile.WithMaskAccessory(0), true));

        for (var style = 1; style <= 3; style++)
        {
            var capturedStyle = style;
            var prototype = YautjaCharacterProfile.Default
                .WithMask(yautja.MaskMaterial, yautja.MaskStyle)
                .WithMaskAccessory(style)
                .MaskAccessoryPrototype;

            if (prototype == null)
                continue;

            AddEntitySelector(_maskAccessoryGrid,
                group,
                prototype,
                yautja.MaskAccessoryStyle == style,
                YautjaCharacterProfile.GetMaskAccessoryDisplayName(style, yautja.MaskMaterial),
                () => Mutate(profile => profile.WithMaskAccessory(capturedStyle), true));
        }
    }

    private void RebuildGreavesSelector(YautjaCharacterProfile yautja)
    {
        _greavesSections.RemoveAllChildren();
        var group = new ButtonGroup();

        foreach (var material in YautjaCharacterProfile.MaterialOrder)
        {
            var grid = EquipmentGrid();
            for (var style = 1; style <= 4; style++)
            {
                var capturedStyle = style;
                var prototype = YautjaCharacterProfile.Default.WithGreaves(material, style).GreavesPrototype;
                AddEntitySelector(grid,
                    group,
                    prototype,
                    yautja.Legacy == YautjaLegacySet.None &&
                    yautja.Unique == YautjaUniqueSet.None &&
                    yautja.GreavesMaterial == material &&
                    yautja.GreavesStyle == style,
                    YautjaCharacterProfile.GetGreavesStyleDisplayName(material, style),
                    () => Mutate(profile => profile
                        .WithGreaves(material, capturedStyle)
                        .WithLegacy(YautjaLegacySet.None)
                        .WithUnique(YautjaUniqueSet.None), true));
            }

            _greavesSections.AddChild(EquipmentMaterialSection(MaterialTitle(material), grid));
        }
    }

    private void RebuildBracerSelector(YautjaCharacterProfile yautja)
    {
        _bracerSections.RemoveAllChildren();
        var group = new ButtonGroup();
        _bracerSections.AddChild(BuildMaterialFilterSelector(
            _bracerFilter,
            YautjaCharacterProfile.BracerMaterialOrder,
            SetBracerFilter));

        var rows = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = false,
            SeparationOverride = 8,
        };

        foreach (var section in BracerSections())
        {
            var grid = EquipmentGrid(horizontalExpand: false);
            var materials = section.Materials
                .Where(material => _bracerFilter == null || material == _bracerFilter)
                .ToArray();
            if (materials.Length == 0)
                continue;

            foreach (var material in materials)
            {
                var capturedMaterial = material;
                AddStaticBracerSelector(grid,
                    group,
                    material,
                    yautja.Legacy == YautjaLegacySet.None && yautja.BracerMaterial == material,
                    YautjaCharacterProfile.GetBracerDisplayName(material),
                    () => Mutate(profile => profile.WithBracer(capturedMaterial).WithLegacy(YautjaLegacySet.None), true),
                    YautjaCharacterProfile.GetBracerMaterialDisplayName(material));
            }

            if (_bracerFilter == null)
                PadEquipmentGrid(grid, materials.Length);

            rows.AddChild(new Label
            {
                Text = section.Title,
                FontColorOverride = Color.FromHex("#d6bf94"),
            });
            rows.AddChild(grid);
        }

        _bracerSections.AddChild(EquipmentMaterialSection(
            Loc.GetString("cmu-yautja-lobby-bracer").ToUpperInvariant(),
            rows,
            true));
    }

    private void RebuildCasterSelector(YautjaCharacterProfile yautja)
    {
        _casterSections.RemoveAllChildren();
        var group = new ButtonGroup();
        var grid = EquipmentGrid();

        _casterSections.AddChild(BuildMaterialFilterSelector(
            _casterFilter,
            YautjaCharacterProfile.CasterMaterialOrder,
            SetCasterFilter));

        foreach (var material in YautjaCharacterProfile.CasterMaterialOrder)
        {
            if (_casterFilter != null && material != _casterFilter)
                continue;

            var capturedMaterial = material;
            var prototype = YautjaCharacterProfile.Default.WithCaster(material).CasterPrototype;
            AddEntitySelector(grid,
                group,
                prototype,
                yautja.CasterMaterial == material,
                YautjaCharacterProfile.GetCasterDisplayName(material),
                () => Mutate(profile => profile.WithCaster(capturedMaterial), true),
                YautjaCharacterProfile.GetBracerMaterialDisplayName(material));
        }

        _casterSections.AddChild(EquipmentMaterialSection(
            Loc.GetString("cmu-yautja-lobby-caster").ToUpperInvariant(),
            grid));
    }

    private void RebuildCapeSelector(YautjaCharacterProfile yautja)
    {
        _capeGrid.RemoveAllChildren();
        var group = new ButtonGroup();

        foreach (var style in YautjaCharacterProfile.CapeStyleOrder)
        {
            var prototype = YautjaCharacterProfile.Default.WithCapeStyle(style).CapePrototype;
            AddEntitySelector(_capeGrid,
                group,
                prototype,
                yautja.CapeStyle == style,
                YautjaCharacterProfile.GetCapeDisplayName(style),
                () => Mutate(profile => profile.WithCapeStyle(style), true));
        }
    }

    private void ReloadPreview(YautjaCharacterProfile yautja)
    {
        DeletePreview();

        if (!_prototypeManager.TryIndex(YautjaSpecies, out var species))
            return;

        _previewDummy = _entManager.SpawnEntity(species.DollPrototype, MapCoordinates.Nullspace);
        _entManager.EnsureComponent<LobbyPreviewEntityComponent>(_previewDummy);
        _entManager.System<HumanoidAppearanceSystem>().LoadProfile(_previewDummy, BuildPreviewProfile(yautja));
        _entManager.System<MetaDataSystem>().SetEntityName(_previewDummy, yautja.Name);

        if (!_previewWithoutGear.Pressed)
        {
            EquipPreview("outerClothing", yautja.ArmorPrototype);
            EquipPreview("mask", yautja.MaskPrototype, mask => AddMaskAccessoryPreview(mask, yautja));

            EquipPreview("shoes", yautja.GreavesPrototype);
            EquipPreview("gloves", yautja.BracerPrototype);
            EquipPreview("back", yautja.CapePrototype);
            EquipPreview("suitStorage", yautja.CasterPrototype);
        }

        _preview.SetEntity(_previewDummy);
    }

    private EntityUid? EquipPreview(string slot, string prototype, Action<EntityUid>? beforeEquip = null)
    {
        if (_previewDummy == EntityUid.Invalid ||
            !_prototypeManager.HasIndex<EntityPrototype>(prototype))
        {
            return null;
        }

        var inventory = _entManager.System<InventorySystem>();
        if (inventory.TryUnequip(_previewDummy, slot, out var unequippedItem, silent: true, force: true, reparent: false))
            _entManager.DeleteEntity(unequippedItem.Value);

        var item = _entManager.SpawnEntity(prototype, MapCoordinates.Nullspace);
        _entManager.EnsureComponent<LobbyPreviewEntityComponent>(item);
        beforeEquip?.Invoke(item);
        if (inventory.TryEquip(_previewDummy, item, slot, true, true))
        {
            _entManager.System<SharedItemSystem>().VisualsChanged(item);
            return item;
        }

        _entManager.DeleteEntity(item);
        return null;
    }

    private void AddMaskAccessoryPreview(EntityUid mask, YautjaCharacterProfile yautja)
    {
        if (yautja.MaskAccessoryPrototype is not { } prototype ||
            !_prototypeManager.HasIndex<EntityPrototype>(prototype) ||
            !_entManager.TryGetComponent(mask, out YautjaMaskAccessoryHolderComponent? holder))
        {
            return;
        }

        var containers = _entManager.System<SharedContainerSystem>();
        var container = containers.EnsureContainer<ContainerSlot>(mask, holder.ContainerId);
        if (container.ContainedEntity is { } oldAccessory)
            _entManager.DeleteEntity(oldAccessory);

        var accessory = _entManager.SpawnEntity(prototype, MapCoordinates.Nullspace);
        _entManager.EnsureComponent<LobbyPreviewEntityComponent>(accessory);
        if (!containers.Insert(accessory, container, force: true))
        {
            _entManager.DeleteEntity(accessory);
            return;
        }

        _entManager.System<SharedItemSystem>().VisualsChanged(mask);
    }

    private EntityUid? BuildSelectorDoll(YautjaCharacterProfile yautja)
    {
        if (!_prototypeManager.TryIndex(YautjaSpecies, out var species))
            return null;

        var dummy = _entManager.SpawnEntity(species.DollPrototype, MapCoordinates.Nullspace);
        _selectorDummies.Add(dummy);
        _entManager.EnsureComponent<LobbyPreviewEntityComponent>(dummy);
        _entManager.System<HumanoidAppearanceSystem>().LoadProfile(dummy, BuildPreviewProfile(yautja));
        return dummy;
    }

    private static HumanoidCharacterProfile BuildPreviewProfile(YautjaCharacterProfile yautja)
    {
        return HumanoidCharacterProfile.DefaultWithSpecies(YautjaSpecies)
            .WithName(yautja.Name)
            .WithAge(yautja.Age)
            .WithSex(Sex.Male)
            .WithGender(Gender.Male)
            .WithCharacterAppearance(yautja.Appearance);
    }

    private void AddEntitySelector(
        GridContainer grid,
        ButtonGroup group,
        string prototype,
        bool selected,
        string tooltip,
        Action onPressed,
        string? label = null)
    {
        if (!_prototypeManager.TryIndex<EntityPrototype>(prototype, out var entityPrototype))
            return;

        var labeled = label != null;
        var button = BuildSelectorButton(
            tooltip,
            selected,
            group,
            labeled ? new Vector2(LabeledVisualButtonSize, LabeledVisualButtonSize) : null);
        button.OnPressed += _ => onPressed();
        var view = new EntityPrototypeView
        {
            MinSize = labeled
                ? new Vector2(LabeledVisualSpriteSize, LabeledVisualSpriteSize)
                : new Vector2(VisualSpriteSize, VisualSpriteSize),
            Stretch = SpriteView.StretchMode.Fill,
        };
        view.SetPrototype(entityPrototype);
        AddSelectorVisual(button, view, label);
        grid.AddChild(button);
    }

    private static void AddStaticBracerSelector(
        GridContainer grid,
        ButtonGroup group,
        YautjaBracerMaterial material,
        bool selected,
        string tooltip,
        Action onPressed,
        string? label = null)
    {
        var labeled = label != null;
        var button = BuildSelectorButton(
            tooltip,
            selected,
            group,
            labeled ? new Vector2(LabeledVisualButtonSize, LabeledVisualButtonSize) : null);
        button.OnPressed += _ => onPressed();

        var view = new AnimatedTextureRect
        {
            MinSize = labeled
                ? new Vector2(LabeledVisualSpriteSize, LabeledVisualSpriteSize)
                : new Vector2(VisualSpriteSize, VisualSpriteSize),
        };
        view.DisplayRect.MinSize = labeled
            ? new Vector2(LabeledVisualSpriteSize, LabeledVisualSpriteSize)
            : new Vector2(VisualSpriteSize, VisualSpriteSize);
        view.DisplayRect.Stretch = TextureRect.StretchMode.Scale;
        view.SetFromSpriteSpecifier(new SpriteSpecifier.Rsi(BracerRsi, GetBracerStaticState(material)));
        AddSelectorVisual(button, view, label);
        grid.AddChild(button);
    }

    private static void AddSelectorVisual(Button button, Control visual, string? label)
    {
        if (label == null)
        {
            button.AddChild(visual);
            return;
        }

        visual.HorizontalAlignment = HAlignment.Center;
        visual.VerticalAlignment = VAlignment.Center;

        button.AddChild(new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            SeparationOverride = 2,
            Children =
            {
                new Label
                {
                    Text = label,
                    MinSize = new Vector2(LabeledVisualButtonSize - 8, 18),
                    MaxSize = new Vector2(LabeledVisualButtonSize - 8, 18),
                    Align = Label.AlignMode.Center,
                    ClipText = true,
                    FontColorOverride = Color.FromHex("#d6bf94"),
                },
                visual,
            },
        });
    }

    private static string GetBracerStaticState(YautjaBracerMaterial material)
    {
        return material switch
        {
            YautjaBracerMaterial.Retro => "bracer1_retro",
            YautjaBracerMaterial.Silver => "bracer1_silver",
            YautjaBracerMaterial.Bronze => "bracer1_bronze",
            YautjaBracerMaterial.Crimson => "bracer1_crimson",
            YautjaBracerMaterial.Bone => "bracer1_bone",
            YautjaBracerMaterial.Dragon => "bracer1_dragon",
            YautjaBracerMaterial.Swamp => "bracer1_swamp",
            YautjaBracerMaterial.Enforcer => "bracer1_enforcer",
            YautjaBracerMaterial.Collector => "bracer1_collector",
            _ => "bracer1_ebony",
        };
    }

    private static void AddTextSelector(
        GridContainer grid,
        ButtonGroup group,
        string text,
        bool selected,
        Action onPressed)
    {
        var button = BuildSelectorButton(text, selected, group);
        button.Text = text;
        button.OnPressed += _ => onPressed();
        grid.AddChild(button);
    }

    private static Button BuildSelectorButton(
        string tooltip,
        bool selected,
        ButtonGroup group,
        Vector2? size = null)
    {
        var actualSize = size ?? new Vector2(VisualButtonSize, VisualButtonSize);
        return new Button
        {
            MinSize = actualSize,
            MaxSize = actualSize,
            ToggleMode = true,
            Pressed = selected,
            Group = group,
            ToolTip = tooltip,
            StyleClasses = { StyleBase.ButtonSquare },
        };
    }

    private static Button BuildSwatchButton(
        string tooltip,
        bool selected,
        ButtonGroup group,
        Color color)
    {
        var button = BuildSelectorButton(tooltip, selected, group, new Vector2(42, 30));
        button.AddChild(new PanelContainer
        {
            MinSize = new Vector2(30, 18),
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = color,
                BorderColor = Color.FromHex("#1f1f1f"),
                BorderThickness = new Thickness(1),
            },
        });

        return button;
    }

    private static Control Row(string label, Control control)
    {
        control.HorizontalAlignment = HAlignment.Right;
        if (control is OptionButton option)
            option.MinWidth = 180;
        if (control is LineEdit line)
            line.MinWidth = 180;

        return new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            Margin = new Thickness(0, 0, 0, 6),
            Children =
            {
                new Label
                {
                    Text = Loc.GetString(label),
                    MinWidth = 110,
                    VerticalAlignment = VAlignment.Center,
                },
                control,
            },
        };
    }

    private static Control VisualBlock(string label, Control control)
    {
        control.HorizontalExpand = true;
        return new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            Margin = new Thickness(0, 2, 0, 8),
            Children =
            {
                new Label { Text = Loc.GetString(label) },
                control,
            },
        };
    }

    private static BoxContainer EquipmentSectionContainer()
    {
        return new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            SeparationOverride = 10,
        };
    }

    private static GridContainer EquipmentCompactSectionContainer()
    {
        return new GridContainer
        {
            Columns = 3,
            HorizontalExpand = true,
        };
    }

    private static GridContainer EquipmentGrid(int columns = 4, bool horizontalExpand = true)
    {
        return new GridContainer
        {
            Columns = Math.Clamp(columns, 1, 4),
            HorizontalExpand = horizontalExpand,
        };
    }

    private static void PadEquipmentGrid(GridContainer grid, int itemCount, int columns = 4)
    {
        var missing = (columns - itemCount % columns) % columns;
        for (var i = 0; i < missing; i++)
        {
            grid.AddChild(new Control
            {
                MinSize = new Vector2(VisualButtonSize, VisualButtonSize),
                MaxSize = new Vector2(VisualButtonSize, VisualButtonSize),
            });
        }
    }

    private static Control EquipmentMaterialSection(string title, Control content, bool compact = false)
    {
        content.HorizontalExpand = !compact;

        var inner = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = !compact,
            Margin = new Thickness(8, 6, 8, 8),
            SeparationOverride = 6,
            Children =
            {
                new Label
                {
                    Text = title,
                    FontColorOverride = Color.FromHex("#d6bf94"),
                },
                content,
            },
        };

        return new PanelContainer
        {
            HorizontalExpand = !compact,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Color.FromHex("#14100e"),
                BorderColor = Color.FromHex("#4b3c2a"),
                BorderThickness = new Thickness(1),
            },
            Children = { inner },
        };
    }

    private static string MaterialTitle(YautjaGearMaterial material)
    {
        return YautjaCharacterProfile.GetMaterialDisplayName(material).ToUpperInvariant();
    }

    private void SetBracerFilter(YautjaBracerMaterial? material)
    {
        _bracerFilter = material;
        if (_profile != null)
            RebuildBracerSelector(_profile.YautjaProfile);
    }

    private void SetCasterFilter(YautjaBracerMaterial? material)
    {
        _casterFilter = material;
        if (_profile != null)
            RebuildCasterSelector(_profile.YautjaProfile);
    }

    private static BoxContainer BuildMaterialFilterSelector(
        YautjaBracerMaterial? selected,
        IReadOnlyCollection<YautjaBracerMaterial> materials,
        Action<YautjaBracerMaterial?> onSelected)
    {
        var selector = new OptionButton
        {
            MinWidth = 180,
            ToolTip = "Filter",
        };
        selector.AddItem("ALL", -1);
        foreach (var material in materials)
            selector.AddItem(YautjaCharacterProfile.GetBracerMaterialDisplayName(material), (int) material);

        selector.SelectId(selected is { } materialFilter ? (int) materialFilter : -1);
        selector.OnItemSelected += args =>
        {
            selector.SelectId(args.Id);
            onSelected(args.Id < 0 ? null : (YautjaBracerMaterial) args.Id);
        };

        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            SeparationOverride = 8,
            Margin = new Thickness(0, 0, 0, 4),
            Children =
            {
                new Label
                {
                    Text = "Filter",
                    MinWidth = 52,
                    VerticalAlignment = VAlignment.Center,
                    FontColorOverride = Color.FromHex("#d6bf94"),
                },
                selector,
            },
        };

        return row;
    }

    private static (string Title, YautjaBracerMaterial[] Materials)[] BracerSections()
    {
        return
        [
            ("RETRO / EBONY / SILVER", [
                YautjaBracerMaterial.Retro,
                YautjaBracerMaterial.Ebony,
                YautjaBracerMaterial.Silver,
            ]),
            ("BRONZE / CRIMSON / BONE", [
                YautjaBracerMaterial.Bronze,
                YautjaBracerMaterial.Crimson,
                YautjaBracerMaterial.Bone,
            ]),
            ("LEGACY", [
                YautjaBracerMaterial.Dragon,
                YautjaBracerMaterial.Swamp,
                YautjaBracerMaterial.Enforcer,
                YautjaBracerMaterial.Collector,
            ]),
        ];
    }

    private void AddTab(string label, Control control)
    {
        _categoryTabs.AddChild(control);
        TabContainer.SetTabTitle(control, Loc.GetString(label));
    }

    private static Control CategoryScroll(Control control)
    {
        control.HorizontalExpand = true;
        return new ScrollContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            MinSize = new Vector2(720, 440),
            Children = { control },
        };
    }

    private Control PreviewRotationControls()
    {
        var left = new Button
        {
            Text = "<",
            MinWidth = 32,
            ToolTip = Loc.GetString("cmu-yautja-lobby-preview-rotate-left"),
        };
        var right = new Button
        {
            Text = ">",
            MinWidth = 32,
            ToolTip = Loc.GetString("cmu-yautja-lobby-preview-rotate-right"),
        };
        left.OnPressed += _ =>
        {
            _previewRotation = _previewRotation.TurnCw();
            SetPreviewRotation(_previewRotation);
        };
        right.OnPressed += _ =>
        {
            _previewRotation = _previewRotation.TurnCcw();
            SetPreviewRotation(_previewRotation);
        };

        return new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalAlignment = HAlignment.Center,
            SeparationOverride = 4,
            Margin = new Thickness(0, 4, 0, 2),
            Children =
            {
                left,
                right,
            },
        };
    }

    private void SetPreviewRotation(Direction direction)
    {
        _preview.OverrideDirection = (Direction) ((int) direction % 4 * 2);
    }

    private Control FlavorBlock()
    {
        _flavorText.HorizontalExpand = true;
        _flavorLimit.HorizontalExpand = true;
        _flavorLimit.ToolTip = Loc.GetString("cmu-yautja-lobby-flavor-limit-tooltip", ("max", YautjaCharacterProfile.MaxFlavorTextLength));

        return new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            Margin = new Thickness(0, 2, 0, 8),
            Children =
            {
                new Label { Text = Loc.GetString("cmu-yautja-lobby-flavor") },
                _flavorText,
                _flavorLimit,
            },
        };
    }

    private void OnFlavorTextChanged(TextEdit input)
    {
        var text = Rope.Collapse(input.TextRope);
        UpdateFlavorLimit(text.Length);
        Mutate(profile => profile.WithFlavorText(text));
    }

    private void UpdateFlavorLimit(int length)
    {
        _flavorLimit.Text = Loc.GetString(
            "cmu-yautja-lobby-flavor-limit",
            ("count", Math.Min(length, YautjaCharacterProfile.MaxFlavorTextLength)),
            ("max", YautjaCharacterProfile.MaxFlavorTextLength));
    }

    private Control TechOptionBlock(string label, OptionButton option, Label help, Action? preview)
    {
        Button? previewButton = null;
        if (preview != null)
        {
            previewButton = new Button
            {
                Text = Loc.GetString("cmu-yautja-lobby-preview-sound"),
                MinWidth = 92,
            };
            previewButton.OnPressed += _ => preview();
        }

        help.HorizontalExpand = true;
        help.FontColorOverride = Color.FromHex("#b8aaa0");

        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            SeparationOverride = 8,
            Children =
            {
                new Label
                {
                    Text = Loc.GetString(label),
                    MinWidth = 160,
                    VerticalAlignment = VAlignment.Center,
                },
                option,
            },
        };

        if (previewButton != null)
            row.AddChild(previewButton);

        return new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            Margin = new Thickness(0, 0, 0, 10),
            Children =
            {
                row,
                help,
            },
        };
    }

    private void UpdateTechHelp(YautjaTranslatorType translatorType, YautjaInvisibilitySound invisibilitySound)
    {
        _translatorHelp.Text = Loc.GetString(translatorType switch
        {
            YautjaTranslatorType.Retro => "cmu-yautja-lobby-translator-help-retro",
            YautjaTranslatorType.Combo => "cmu-yautja-lobby-translator-help-combo",
            _ => "cmu-yautja-lobby-translator-help-modern",
        });
        _invisibilityHelp.Text = Loc.GetString(invisibilitySound == YautjaInvisibilitySound.Retro
            ? "cmu-yautja-lobby-invisibility-help-retro"
            : "cmu-yautja-lobby-invisibility-help-modern");
    }

    private static SoundPathSpecifier GetInvisibilityPreviewSound(int id)
    {
        return (YautjaInvisibilitySound) id == YautjaInvisibilitySound.Retro
            ? RetroCloakPreviewSound
            : ModernCloakPreviewSound;
    }

    private void PlayPreviewSound(SoundSpecifier sound)
    {
        _entManager.System<SharedAudioSystem>().PlayGlobal(sound, Filter.Local(), false, AudioParams.Default.WithVolume(-4f));
    }

    private static void AddTranslatorTypeOptions(OptionButton button)
    {
        foreach (var value in YautjaCharacterProfile.TranslatorTypeOrder)
            button.AddItem(YautjaCharacterProfile.GetTranslatorTypeDisplayName(value), (int) value);
    }

    private static void AddInvisibilitySoundOptions(OptionButton button)
    {
        foreach (var value in YautjaCharacterProfile.InvisibilitySoundOrder)
            button.AddItem(YautjaCharacterProfile.GetInvisibilitySoundDisplayName(value), (int) value);
    }

    private void DeletePreview()
    {
        _preview.SetEntity(null);
        if (_entManager.EntityExists(_previewDummy))
            _entManager.DeleteEntity(_previewDummy);
        _previewDummy = EntityUid.Invalid;
    }

    private void DisposeSelectorDummies()
    {
        foreach (var dummy in _selectorDummies)
        {
            if (_entManager.EntityExists(dummy))
                _entManager.DeleteEntity(dummy);
        }

        _selectorDummies.Clear();
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();
        DeletePreview();
        DisposeSelectorDummies();
    }
}
