using System.Linq;
using System.Numerics;
using Content.Client.Lobby;
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
    private static readonly ResPath RankRsi = new("/Textures/_CMU14/Yautja/hud_yautja.rsi");

    private readonly LineEdit _name = new();
    private readonly LineEdit _age = new();
    private readonly OptionButton _gender = new();
    private readonly AnimatedTextureRect _rankIcon = new();
    private readonly Label _rankName = new();
    private readonly OptionButton _status = new();
    private readonly CheckBox _previewWithoutGear = new();
    private readonly Label _summarySet = new();
    private readonly Label _summaryArmor = new();
    private readonly Label _summaryMask = new();
    private readonly Label _summaryGreaves = new();
    private readonly Label _summaryCape = new();
    private readonly Label _summaryBracer = new();
    private readonly Label _summaryCaster = new();
    private readonly OptionButton _translatorType = new();
    private readonly OptionButton _invisibilitySound = new();
    private readonly Label _translatorHelp = new();
    private readonly Label _invisibilityHelp = new();
    private readonly Label _flavorLimit = new();
    private readonly TextEdit _flavorText = new()
    {
        MinHeight = 90,
        HorizontalExpand = true,
        // MaxLength = YautjaCharacterProfile.MaxFlavorTextLength, выдает ошибку.
    };

    private readonly GridContainer _skinGrid = new() { Columns = 6 };
    private readonly GridContainer _eyeGrid = new() { Columns = 7 };
    private readonly GridContainer _dreadGrid = new() { Columns = 7 };
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
    private readonly ButtonGroup _categoryButtonGroup = new();
    private readonly BoxContainer _categoryNavigation = new()
    {
        Orientation = BoxContainer.LayoutOrientation.Vertical,
        SeparationOverride = 4,
    };
    private readonly BoxContainer _categoryPages = new()
    {
        Orientation = BoxContainer.LayoutOrientation.Vertical,
        HorizontalExpand = true,
        VerticalExpand = true,
    };
    private readonly Dictionary<YautjaProfileEditorCategory, Control> _categoryPageControls = new();
    private readonly Dictionary<YautjaProfileEditorCategory, Button> _categoryButtons = new();
    private readonly Dictionary<GridContainer, int> _responsiveGrids = new();
    private readonly List<GridContainer> _bracerResponsiveGrids = new();
    private readonly List<GridContainer> _casterResponsiveGrids = new();
    private readonly BoxContainer _workArea;
    private readonly BoxContainer _previewColumn;
    private readonly BoxContainer _categoryWorkspace;
    private YautjaProfileEditorCategory _activeCategory = YautjaProfileEditorCategory.Appearance;

    private readonly SpriteView _preview = new()
    {
        MinSize = new Vector2(190, 230),
        Scale = new Vector2(4, 4),
        OverrideDirection = Direction.South,
        Stretch = SpriteView.StretchMode.Fit,
    };

    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IClientPreferencesManager _preferencesManager = default!;

    private readonly List<EntityUid> _selectorDummies = new();
    private HumanoidCharacterProfile? _profile;
    private EntityUid _previewDummy = EntityUid.Invalid;
    private Direction _previewRotation = Direction.South;
    private YautjaBracerMaterial? _bracerFilter;
    private YautjaBracerMaterial? _casterFilter;
    private bool _updating;
    private YautjaProfileCapabilities _capabilities = YautjaProfileCapabilities.Default;
    private YautjaProfileCapabilities _effectiveCapabilities = YautjaProfileCapabilities.Default;

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

        _rankIcon.MinSize = new Vector2(32, 32);
        _rankIcon.DisplayRect.MinSize = new Vector2(32, 32);
        _rankIcon.DisplayRect.Stretch = TextureRect.StretchMode.Scale;

        _workArea = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 12,
        };
        root.AddChild(_workArea);

        var previewColumn = new BoxContainer
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
                Row("cmu-yautja-lobby-name", _name),
                Row("cmu-yautja-lobby-age", _age),
                Row("cmu-yautja-lobby-gender", _gender),
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Horizontal,
                    SeparationOverride = 8,
                    Margin = new Thickness(0, 0, 0, 6),
                    Children =
                    {
                        new Label
                        {
                            Text = Loc.GetString("cmu-yautja-rank"),
                            MinWidth = 110,
                            VerticalAlignment = VAlignment.Center,
                        },
                        _rankIcon,
                        _rankName,
                    },
                },
                Row("cmu-yautja-lobby-status", _status),
                PreviewRotationControls(),
                _previewWithoutGear,
                new PanelContainer
                {
                    Children =
                    {
                        new BoxContainer
                        {
                            Orientation = BoxContainer.LayoutOrientation.Vertical,
                            Margin = new Thickness(6),
                            Children =
                            {
                                _summarySet,
                                _summaryArmor,
                                _summaryMask,
                                _summaryGreaves,
                                _summaryCape,
                                _summaryBracer,
                                _summaryCaster,
                            },
                        },
                    },
                },
            },
        };
        _previewColumn = previewColumn;
        _workArea.AddChild(_previewColumn);

        var categoryWorkspace = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 8,
        };
        categoryWorkspace.AddChild(new PanelContainer
        {
            MinWidth = 176,
            Children = { _categoryNavigation },
        });
        categoryWorkspace.AddChild(new PanelContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            Children = { _categoryPages },
        });
        _categoryWorkspace = categoryWorkspace;
        _workArea.AddChild(_categoryWorkspace);

        AddCategory(YautjaProfileEditorCategory.Appearance, new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            Children =
            {
                VisualBlock("cmu-yautja-lobby-skin-color", _skinGrid),
                VisualBlock("cmu-yautja-lobby-eyes", _eyeGrid),
                VisualBlock("cmu-yautja-lobby-dread-color", _dreadGrid),
                VisualBlock("cmu-yautja-lobby-quills", _quillGrid),
            },
        });
        AddCategory(YautjaProfileEditorCategory.Equipment, BuildEquipmentPage());
        AddCategory(YautjaProfileEditorCategory.Sets, BuildSetsPage());
        AddCategory(YautjaProfileEditorCategory.Technology, BuildTechnologyPage());
        AddCategory(YautjaProfileEditorCategory.Description, FlavorBlock());
        SelectCategory(_activeCategory);
        _categoryPages.OnResized += UpdateResponsiveGridColumns;
        _workArea.OnResized += UpdateWorkAreaLayout;

        AddGenderOptions(_gender);
        AddTranslatorTypeOptions(_translatorType);
        AddInvisibilitySoundOptions(_invisibilitySound);

        _name.OnTextChanged += args => Mutate(profile => profile.WithName(args.Text));
        _age.OnTextChanged += args =>
        {
            if (int.TryParse(args.Text, out var age))
                Mutate(profile => profile.WithAge(age));
        };
        _gender.OnItemSelected += args =>
        {
            _gender.SelectId(args.Id);
            Mutate(profile => profile.WithGender((Gender) args.Id));
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
        _status.OnItemSelected += args =>
        {
            _status.SelectId(args.Id);
            Mutate(profile => profile.WithStatus((YautjaProfileStatus) args.Id), true);
        };

        UpdateWorkAreaLayout();
    }

    public void SetProfile(HumanoidCharacterProfile? profile)
    {
        _profile = profile;
        _updating = true;

        var yautja = profile?.YautjaProfile ?? YautjaCharacterProfile.Default;
        _capabilities = _preferencesManager.YautjaCapabilities;
        _effectiveCapabilities = _capabilities.ForStatus(yautja.Status);
        RebuildStatusSelector(yautja);
        _name.Text = yautja.Name;
        _age.Text = yautja.Age.ToString();
        _gender.SelectId((int) yautja.Gender);
        UpdateRankPresentation();
        _flavorText.TextRope = new Rope.Leaf(yautja.FlavorText);
        UpdateFlavorLimit(yautja.FlavorText.Length);
        _translatorType.SelectId((int) yautja.TranslatorType);
        _invisibilitySound.SelectId((int) yautja.InvisibilitySound);
        UpdateTechHelp(yautja.TranslatorType, yautja.InvisibilitySound);
        RebuildVisualSelectors(yautja);
        UpdateSelectionSummary(yautja);

        _updating = false;
        ReloadPreview(yautja);
    }

    private void RebuildStatusSelector(YautjaCharacterProfile yautja)
    {
        _status.Clear();
        foreach (var status in YautjaCharacterProfile.StatusOrder)
        {
            if (!_capabilities.CanUseStatus(status))
                continue;

            _status.AddItem(Loc.GetString(StatusLocalizationKey(status)), (int) status);
        }

        var selectedStatus = _capabilities.SanitizeStatus(yautja.Status);
        _status.SelectId((int) selectedStatus);
    }

    private static string StatusLocalizationKey(YautjaProfileStatus status)
    {
        return status switch
        {
            YautjaProfileStatus.Council => "cmu-yautja-lobby-status-council",
            YautjaProfileStatus.Leader => "cmu-yautja-lobby-status-leader",
            _ => "cmu-yautja-lobby-status-normal",
        };
    }

    private void Mutate(Func<YautjaCharacterProfile, YautjaCharacterProfile> update, bool rebuildSelectors = false)
    {
        if (_updating || _profile == null)
            return;

        var yautja = update(_profile.YautjaProfile);
        if (rebuildSelectors)
            yautja = yautja.SanitizeForCapabilities(_capabilities);

        var profile = _profile.WithYautjaProfile(yautja);
        _profile = profile;
        _effectiveCapabilities = _capabilities.ForStatus(profile.YautjaProfile.Status);
        UpdateRankPresentation();
        UpdateSelectionSummary(profile.YautjaProfile);

        if (rebuildSelectors)
            RebuildVisualSelectors(profile.YautjaProfile);

        ReloadPreview(profile.YautjaProfile);
        OnProfileChanged?.Invoke(profile);
    }

    private void UpdateSelectionSummary(YautjaCharacterProfile yautja)
    {
        var summary = YautjaProfileEditorLayout.BuildSummary(yautja);
        _summarySet.Text = Loc.GetString("cmu-yautja-lobby-summary-set", ("value", summary.Set == "—"
            ? Loc.GetString("cmu-yautja-lobby-summary-custom")
            : Loc.GetString(summary.Set)));
        _summaryArmor.Text = Loc.GetString(
            "cmu-yautja-lobby-summary-armor",
            ("value", Loc.GetString(summary.Armor)));
        _summaryMask.Text = Loc.GetString(
            "cmu-yautja-lobby-summary-mask",
            ("value", Loc.GetString(summary.Mask)));
        _summaryGreaves.Text = Loc.GetString(
            "cmu-yautja-lobby-summary-greaves",
            ("value", Loc.GetString(summary.Greaves)));
        _summaryCape.Text = Loc.GetString(
            "cmu-yautja-lobby-summary-cape",
            ("value", Loc.GetString(summary.Cape)));
        _summaryBracer.Text = Loc.GetString(
            "cmu-yautja-lobby-summary-bracer",
            ("value", Loc.GetString(summary.Bracer)));
        _summaryCaster.Text = Loc.GetString(
            "cmu-yautja-lobby-summary-caster",
            ("value", Loc.GetString(summary.Caster)));
    }

    private void UpdateRankPresentation()
    {
        var rankInfo = YautjaRankMetadata.For(_effectiveCapabilities.Rank);
        _rankName.Text = Loc.GetString(rankInfo.LocalizedName);
        _rankIcon.SetFromSpriteSpecifier(new SpriteSpecifier.Rsi(RankRsi, rankInfo.IconState));
    }

    private void RebuildVisualSelectors(YautjaCharacterProfile yautja)
    {
        _effectiveCapabilities = _capabilities.ForStatus(yautja.Status);
        DisposeSelectorDummies();
        ResetResponsiveGrids();

        RebuildSkinSelector(yautja);
        RebuildEyeSelector(yautja);
        RebuildDreadSelector(yautja);
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
        UpdateResponsiveGridColumns();
    }

    private void RebuildSkinSelector(YautjaCharacterProfile yautja)
    {
        _skinGrid.RemoveAllChildren();
        var group = new ButtonGroup();

        foreach (var skinColor in YautjaCharacterProfile.SkinColorOrder)
        {
            var button = BuildSelectorButton(
                Loc.GetString(YautjaCharacterProfile.GetSkinColorDisplayName(skinColor)),
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
                Loc.GetString(YautjaCharacterProfile.GetEyeColorDisplayName(eyeColor)),
                yautja.EyeColor == eyeColor,
                group,
                YautjaCharacterProfile.GetEyeColorColor(eyeColor));

            button.OnPressed += _ => Mutate(profile => profile.WithEyeColor(eyeColor), true);
            _eyeGrid.AddChild(button);
        }
    }

    private void RebuildDreadSelector(YautjaCharacterProfile yautja)
    {
        _dreadGrid.RemoveAllChildren();
        var group = new ButtonGroup();

        foreach (var dreadColor in YautjaCharacterProfile.DreadColorOrder)
        {
            var button = BuildSwatchButton(
                Loc.GetString(DreadColorLocalizationKey(dreadColor)),
                yautja.DreadColor == dreadColor,
                group,
                YautjaCharacterProfile.GetDreadColorColor(dreadColor, yautja.Appearance.SkinColor));

            button.OnPressed += _ => Mutate(profile => profile.WithDreadColor(dreadColor), true);
            _dreadGrid.AddChild(button);
        }
    }

    private static string DreadColorLocalizationKey(YautjaDreadColor color)
    {
        return color switch
        {
            YautjaDreadColor.Black => "cmu-yautja-dread-color-black",
            YautjaDreadColor.DarkBrown => "cmu-yautja-dread-color-dark-brown",
            YautjaDreadColor.Brown => "cmu-yautja-dread-color-brown",
            YautjaDreadColor.Auburn => "cmu-yautja-dread-color-auburn",
            YautjaDreadColor.Ash => "cmu-yautja-dread-color-ash",
            YautjaDreadColor.Bone => "cmu-yautja-dread-color-bone",
            _ => "cmu-yautja-dread-color-match-skin",
        };
    }

    private void RebuildQuillSelector(YautjaCharacterProfile yautja)
    {
        _quillGrid.RemoveAllChildren();
        var group = new ButtonGroup();

        foreach (var quill in YautjaCharacterProfile.QuillStyleOrder)
        {
            var button = BuildSelectorButton(
                Loc.GetString(YautjaCharacterProfile.GetQuillStyleDisplayName(quill)),
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
                button.Text = Loc.GetString(YautjaCharacterProfile.GetQuillStyleDisplayName(quill));
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
                    Loc.GetString(YautjaCharacterProfile.GetLegacyDisplayName(legacy)),
                    selected,
                    () => Mutate(profile => profile.WithLegacy(YautjaLegacySet.None), true));
                continue;
            }

            var preview = YautjaCharacterProfile.Default.WithLegacy(legacy).ArmorPrototype;
            var locked = YautjaProfileEditorLayout.IsLegacySetLocked(_capabilities, legacy);
            var tooltip = locked
                ? Loc.GetString("cmu-yautja-lobby-locked-legacy")
                : Loc.GetString(YautjaCharacterProfile.GetLegacyDisplayName(legacy));
            AddEntitySelector(_legacyGrid,
                group,
                preview,
                selected,
                tooltip,
                () => Mutate(profile => profile.WithLegacy(legacy).WithUnique(YautjaUniqueSet.None), true),
                Loc.GetString(YautjaCharacterProfile.GetLegacyDisplayName(legacy)),
                locked);
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
                    Loc.GetString(YautjaCharacterProfile.GetUniqueDisplayName(unique)),
                    selected,
                    () => Mutate(profile => profile.WithUnique(YautjaUniqueSet.None), true));
                continue;
            }

            var preview = YautjaCharacterProfile.Default.WithUnique(unique).ArmorPrototype;
            var locked = YautjaProfileEditorLayout.IsUniqueSetLocked(_capabilities, unique);
            var tooltip = locked
                ? Loc.GetString(
                    "cmu-yautja-lobby-locked-rank",
                    ("rank", Loc.GetString(YautjaRankMetadata.For(YautjaRank.Elite).LocalizedName)))
                : Loc.GetString(YautjaCharacterProfile.GetUniqueDisplayName(unique));
            AddEntitySelector(_uniqueGrid,
                group,
                preview,
                selected,
                tooltip,
                () => Mutate(profile => profile.WithUnique(unique).WithLegacy(YautjaLegacySet.None), true),
                Loc.GetString(YautjaCharacterProfile.GetUniqueDisplayName(unique)),
                locked);
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
                    Loc.GetString(YautjaCharacterProfile.GetArmorStyleDisplayName(material, style)),
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
                    Loc.GetString(YautjaCharacterProfile.GetMaskStyleDisplayName(material, style)),
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
            Loc.GetString(YautjaCharacterProfile.GetMaskAccessoryDisplayName(0, yautja.MaskMaterial)),
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
                Loc.GetString(YautjaCharacterProfile.GetMaskAccessoryDisplayName(style, yautja.MaskMaterial)),
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
                    Loc.GetString(YautjaCharacterProfile.GetGreavesStyleDisplayName(material, style)),
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
        UnregisterResponsiveGrids(_bracerResponsiveGrids);
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
            var materials = section.Materials
                .Where(material => _bracerFilter == null || material == _bracerFilter)
                .ToArray();
            if (materials.Length == 0)
                continue;

            var grid = RegisterSectionResponsiveGrid(_bracerResponsiveGrids, EquipmentGrid(horizontalExpand: false));

            foreach (var material in materials)
            {
                var capturedMaterial = material;
                var locked = YautjaProfileEditorLayout.IsBracerLocked(_capabilities, material);
                var tooltip = locked && material is
                    YautjaBracerMaterial.Dragon or
                    YautjaBracerMaterial.Swamp or
                    YautjaBracerMaterial.Enforcer or
                    YautjaBracerMaterial.Collector
                    ? Loc.GetString("cmu-yautja-lobby-locked-legacy")
                    : locked
                        ? Loc.GetString(
                            "cmu-yautja-lobby-locked-rank",
                            ("rank", Loc.GetString(YautjaRankMetadata.For(YautjaRank.Elite).LocalizedName)))
                        : Loc.GetString(YautjaCharacterProfile.GetBracerDisplayName(material));
                AddStaticBracerSelector(grid,
                    group,
                    material,
                    yautja.Legacy == YautjaLegacySet.None && yautja.BracerMaterial == material,
                    tooltip,
                    () => Mutate(profile => profile.WithBracer(capturedMaterial).WithLegacy(YautjaLegacySet.None), true),
                    Loc.GetString(YautjaCharacterProfile.GetBracerMaterialDisplayName(material)),
                    locked);
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
        UnregisterResponsiveGrids(_casterResponsiveGrids);
        _casterSections.RemoveAllChildren();
        var group = new ButtonGroup();
        var grid = RegisterSectionResponsiveGrid(_casterResponsiveGrids, EquipmentGrid());

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
                Loc.GetString(YautjaCharacterProfile.GetCasterDisplayName(material)),
                () => Mutate(profile => profile.WithCaster(capturedMaterial), true),
                Loc.GetString(YautjaCharacterProfile.GetBracerMaterialDisplayName(material)));
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
            var locked = YautjaProfileEditorLayout.IsCapeLocked(_capabilities, style);
            var tooltip = locked
                ? Loc.GetString("cmu-yautja-lobby-locked-leader-ancient")
                : Loc.GetString(YautjaCharacterProfile.GetCapeDisplayName(style));
            AddEntitySelector(_capeGrid,
                group,
                prototype,
                yautja.CapeStyle == style,
                tooltip,
                () => Mutate(profile => profile.WithCapeStyle(style), true),
                disabled: locked);
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
            .WithSex(yautja.Sex)
            .WithGender(yautja.Gender)
            .WithCharacterAppearance(yautja.Appearance);
    }

    private void AddEntitySelector(
        GridContainer grid,
        ButtonGroup group,
        string prototype,
        bool selected,
        string tooltip,
        Action onPressed,
        string? label = null,
        bool disabled = false)
    {
        if (!_prototypeManager.TryIndex<EntityPrototype>(prototype, out var entityPrototype))
            return;

        label ??= tooltip;
        var labeled = label != null;
        var button = BuildSelectorButton(
            tooltip,
            selected,
            group,
            labeled ? new Vector2(LabeledVisualButtonSize, LabeledVisualButtonSize) : null);
        button.Disabled = disabled;
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
        string? label = null,
        bool disabled = false)
    {
        var labeled = label != null;
        var button = BuildSelectorButton(
            tooltip,
            selected,
            group,
            labeled ? new Vector2(LabeledVisualButtonSize, LabeledVisualButtonSize) : null);
        button.Disabled = disabled;
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

    private GridContainer EquipmentGrid(int columns = 4, bool horizontalExpand = true)
    {
        return RegisterResponsiveGrid(new GridContainer
        {
            Columns = Math.Clamp(columns, 1, 4),
            HorizontalExpand = horizontalExpand,
        }, columns);
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
        return Loc.GetString(YautjaCharacterProfile.GetMaterialDisplayName(material)).ToUpperInvariant();
    }

    private void SetBracerFilter(YautjaBracerMaterial? material)
    {
        _bracerFilter = material;
        if (_profile != null)
        {
            RebuildBracerSelector(_profile.YautjaProfile);
            UpdateResponsiveGridColumns();
        }
    }

    private void SetCasterFilter(YautjaBracerMaterial? material)
    {
        _casterFilter = material;
        if (_profile != null)
        {
            RebuildCasterSelector(_profile.YautjaProfile);
            UpdateResponsiveGridColumns();
        }
    }

    private static BoxContainer BuildMaterialFilterSelector(
        YautjaBracerMaterial? selected,
        IReadOnlyCollection<YautjaBracerMaterial> materials,
        Action<YautjaBracerMaterial?> onSelected)
    {
        var selector = new OptionButton
        {
            MinWidth = 180,
            ToolTip = Loc.GetString("cmu-yautja-lobby-filter-tooltip"),
        };
        selector.AddItem(Loc.GetString("cmu-yautja-lobby-filter-all"), -1);
        foreach (var material in materials)
            selector.AddItem(Loc.GetString(YautjaCharacterProfile.GetBracerMaterialDisplayName(material)), (int) material);

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
                    Text = Loc.GetString("cmu-yautja-lobby-filter-label"),
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
            ("cmu-yautja-profile-material-group-core", [
                YautjaBracerMaterial.Retro,
                YautjaBracerMaterial.Ebony,
                YautjaBracerMaterial.Silver,
            ]),
            ("cmu-yautja-profile-material-group-colored", [
                YautjaBracerMaterial.Bronze,
                YautjaBracerMaterial.Crimson,
                YautjaBracerMaterial.Bone,
            ]),
            ("cmu-yautja-profile-material-group-legacy", [
                YautjaBracerMaterial.Dragon,
                YautjaBracerMaterial.Swamp,
                YautjaBracerMaterial.Enforcer,
                YautjaBracerMaterial.Collector,
            ]),
        ];
    }

    private void AddCategory(YautjaProfileEditorCategory category, Control content)
    {
        var page = CategoryScroll(content);
        page.Visible = _categoryPageControls.Count == 0;
        _categoryPages.AddChild(page);
        _categoryPageControls[category] = page;

        var definition = YautjaProfileEditorLayout.Categories.Single(info => info.Id == category);
        var button = new Button
        {
            Text = Loc.GetString(definition.LocalizationKey),
            ToggleMode = true,
            Group = _categoryButtonGroup,
            HorizontalExpand = true,
            Pressed = _categoryPageControls.Count == 1,
        };
        button.OnPressed += _ => SelectCategory(category);
        _categoryNavigation.AddChild(button);
        _categoryButtons[category] = button;
    }

    private void ResetResponsiveGrids()
    {
        _responsiveGrids.Clear();
        _bracerResponsiveGrids.Clear();
        _casterResponsiveGrids.Clear();
        RegisterResponsiveGrid(_skinGrid, 6);
        RegisterResponsiveGrid(_eyeGrid, 7);
        RegisterResponsiveGrid(_dreadGrid, 7);
        RegisterResponsiveGrid(_quillGrid, 6);
        RegisterResponsiveGrid(_legacyGrid, 4);
        RegisterResponsiveGrid(_uniqueGrid, 4);
        RegisterResponsiveGrid(_maskAccessoryGrid, 4);
        RegisterResponsiveGrid(_capeGrid, 4);
    }

    private GridContainer RegisterResponsiveGrid(GridContainer grid, int preferredColumns)
    {
        grid.HSeparationOverride = 8;
        _responsiveGrids[grid] = preferredColumns;
        return grid;
    }

    private static GridContainer RegisterSectionResponsiveGrid(List<GridContainer> sectionGrids, GridContainer grid)
    {
        sectionGrids.Add(grid);
        return grid;
    }

    private void UnregisterResponsiveGrids(List<GridContainer> grids)
    {
        foreach (var grid in grids)
            _responsiveGrids.Remove(grid);

        grids.Clear();
    }

    private void UpdateResponsiveGridColumns()
    {
        var availableWidth = MathF.Max(0, _categoryPages.Width - 16);
        foreach (var (grid, preferredColumns) in _responsiveGrids)
        {
            grid.Columns = YautjaProfileEditorLayout.GetResponsiveColumnCount(availableWidth, preferredColumns);
        }
    }

    private void UpdateWorkAreaLayout()
    {
        var stacked = YautjaProfileEditorLayout.ShouldStackWorkArea(_workArea.Width);
        _workArea.Orientation = stacked
            ? BoxContainer.LayoutOrientation.Vertical
            : BoxContainer.LayoutOrientation.Horizontal;
        _previewColumn.HorizontalExpand = stacked;
        _categoryWorkspace.HorizontalExpand = true;
        UpdateResponsiveGridColumns();
    }

    private void SelectCategory(YautjaProfileEditorCategory category)
    {
        _activeCategory = category;
        foreach (var (id, page) in _categoryPageControls)
            page.Visible = YautjaProfileEditorLayout.IsCategoryActive(category, id);

        foreach (var (id, button) in _categoryButtons)
            button.Pressed = YautjaProfileEditorLayout.IsCategoryActive(category, id);
    }

    private Control BuildEquipmentPage()
    {
        return new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            Children =
            {
                VisualBlock("cmu-yautja-lobby-armor", _armorSections),
                VisualBlock("cmu-yautja-lobby-mask", _maskSections),
                VisualBlock("cmu-yautja-lobby-mask-accessory", _maskAccessoryGrid),
                VisualBlock("cmu-yautja-lobby-greaves", _greavesSections),
                VisualBlock("cmu-yautja-lobby-bracer", _bracerSections),
                VisualBlock("cmu-yautja-lobby-caster", _casterSections),
                VisualBlock("cmu-yautja-lobby-cape", _capeGrid),
            },
        };
    }

    private Control BuildSetsPage()
    {
        return new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            Children =
            {
                VisualBlock("cmu-yautja-lobby-legacy", _legacyGrid),
                VisualBlock("cmu-yautja-lobby-unique", _uniqueGrid),
            },
        };
    }

    private Control BuildTechnologyPage()
    {
        return new BoxContainer
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
        };
    }

    private static Control CategoryScroll(Control control)
    {
        control.HorizontalExpand = true;
        return new ScrollContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            MinSize = new Vector2(0, 440),
            HScrollEnabled = true,
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
        option.HorizontalExpand = true;

        Button? previewButton = null;
        if (preview != null)
        {
            previewButton = new Button
            {
                Text = Loc.GetString("cmu-yautja-lobby-preview-sound"),
                HorizontalExpand = true,
            };
            previewButton.OnPressed += _ => preview();
        }

        help.HorizontalExpand = true;
        help.FontColorOverride = Color.FromHex("#b8aaa0");

        var block = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            SeparationOverride = YautjaProfileEditorLayout.TechOptionSpacing,
            Margin = new Thickness(0, 0, 0, YautjaProfileEditorLayout.TechOptionBottomMargin),
            Children =
            {
                new Label { Text = Loc.GetString(label), HorizontalExpand = true },
                option,
            },
        };

        if (previewButton != null)
            block.AddChild(previewButton);

        block.AddChild(help);
        return block;
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
            button.AddItem(Loc.GetString(YautjaCharacterProfile.GetTranslatorTypeDisplayName(value)), (int) value);
    }

    private static void AddInvisibilitySoundOptions(OptionButton button)
    {
        foreach (var value in YautjaCharacterProfile.InvisibilitySoundOrder)
            button.AddItem(Loc.GetString(YautjaCharacterProfile.GetInvisibilitySoundDisplayName(value)), (int) value);
    }

    private static void AddGenderOptions(OptionButton button)
    {
        button.AddItem(Loc.GetString("humanoid-profile-editor-sex-male-text"), (int) Gender.Male);
        button.AddItem(Loc.GetString("humanoid-profile-editor-sex-female-text"), (int) Gender.Female);
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
