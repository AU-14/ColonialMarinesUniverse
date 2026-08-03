using System.Linq;
using Content.Client.Humanoid;
using Content.Client.Message;
using Content.Shared.AU14.Allegiance;
using Content.Shared.AU14.Origin;
using Content.Shared.Body;
using Content.Shared.CCVar;
using Content.Shared._CMU14.CharacterDescription;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Preferences;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private readonly List<AllegiancePrototype> _allegiances = [];
    private readonly List<OriginPrototype> _origins = [];
    private bool _loadingCmuControls;
    private bool _loadingHeightControls;

    private void InitializeCmuTabs()
    {
        TabContainer.SetTabTitle(CharacterDescriptionTabIndex,
            Loc.GetString("humanoid-profile-editor-character-description-tab"));
        TabContainer.SetTabVisible(CharacterDescriptionTabIndex, _cfgManager.GetCVar(CCVars.CharacterDescription));
        TabContainer.SetTabTitle(RegulationAppearanceTabIndex,
            Loc.GetString("humanoid-profile-editor-regulation-appearance-tab"));
        TabContainer.SetTabTitle(InsurgencyTabIndex,
            Loc.GetString("humanoid-profile-editor-insurgency-tab"));
        TabContainer.SetTabTitle(ColonyFallTabIndex,
            Loc.GetString("humanoid-profile-editor-colony-fall-tab"));
        TabContainer.SetTabTitle(DistressSignalTabIndex,
            Loc.GetString("humanoid-profile-editor-distress-signal-tab"));
        TabContainer.SetTabTitle(TraitsTabIndex,
            Loc.GetString("humanoid-profile-editor-traits-tab"));
        TabContainer.SetTabTitle(MarkingsTabIndex,
            Loc.GetString("humanoid-profile-editor-markings-tab"));

        InsurgencyTabs.SetTabTitle(0, Loc.GetString("humanoid-profile-editor-government-jobs-tab"));
        InsurgencyTabs.SetTabTitle(1, Loc.GetString("humanoid-profile-editor-insurgency-jobs-tab"));
        InsurgencyTabs.SetTabTitle(2, Loc.GetString("humanoid-profile-editor-civilian-jobs-tab"));
        InsurgencyTabs.SetTabTitle(3, Loc.GetString("humanoid-profile-editor-antags-tab"));
        ColonyFallTabs.SetTabTitle(0, Loc.GetString("humanoid-profile-editor-civilian-jobs-tab"));
        ColonyFallTabs.SetTabTitle(1, Loc.GetString("humanoid-profile-editor-threat-roles-tab"));
        ColonyFallTabs.SetTabTitle(2, Loc.GetString("humanoid-profile-editor-antags-tab"));
        DistressSignalTabs.SetTabTitle(0, Loc.GetString("humanoid-profile-editor-government-jobs-tab"));
        DistressSignalTabs.SetTabTitle(1, Loc.GetString("humanoid-profile-editor-threat-roles-tab"));
        DistressSignalTabs.SetTabTitle(2, Loc.GetString("humanoid-profile-editor-antags-tab"));

        RefreshAllegiances();
        AllegianceButton.OnItemSelected += args =>
        {
            if (_loadingCmuControls)
                return;
            AllegianceButton.SelectId(args.Id);
            Profile = Profile?.WithAllegiance(args.Id == 0 ? null : _allegiances[args.Id - 1].ID);
            SetDirty();
        };

        RefreshOrigins();
        OriginButton.OnItemSelected += args =>
        {
            if (_loadingCmuControls)
                return;
            OriginButton.SelectId(args.Id);
            Profile = Profile?.WithOrigin(args.Id == 0 ? null : _origins[args.Id - 1].ID);
            SetDirty();
        };

        ShortExamineEdit.OnTextChanged += args =>
        {
            if (_loadingCmuControls)
                return;
            Profile = Profile?.WithShortExamine(args.Text);
            SetDirty();
        };

        static bool ValidFeet(string value) =>
            value.Length <= 1 && (value.Length == 0 || value[0] is >= '4' and <= '6');
        static bool ValidInches(string value) =>
            value.Length <= 2 && value.All(char.IsDigit) && (value.Length == 0 || int.Parse(value) <= 11);
        HeightFeetEdit.IsValid = ValidFeet;
        HeightInchesEdit.IsValid = ValidInches;
        HeightFeetEdit.OnTextChanged += _ => UpdateHeightFromEdits();
        HeightInchesEdit.OnTextChanged += _ => UpdateHeightFromEdits();
        WeightEdit.OnTextChanged += args =>
        {
            if (_loadingCmuControls || !int.TryParse(args.Text, out var weight))
                return;
            Profile = Profile?.WithWeight(weight);
            SetDirty();
        };
        FullDescriptionEdit.OnTextChanged += _ => UpdateDescriptionField(
            profile => profile.WithFullDescription(Rope.Collapse(FullDescriptionEdit.TextRope)));
        MedicalRecordEdit.OnTextChanged += _ => UpdateDescriptionField(
            profile => profile.WithMedicalRecord(Rope.Collapse(MedicalRecordEdit.TextRope)));
        CriminalRecordEdit.OnTextChanged += _ => UpdateDescriptionField(
            profile => profile.WithCriminalRecord(Rope.Collapse(CriminalRecordEdit.TextRope)));
        GeneralRecordEdit.OnTextChanged += _ => UpdateDescriptionField(
            profile => profile.WithGeneralRecord(Rope.Collapse(GeneralRecordEdit.TextRope)));

        foreach (var build in Enum.GetValues<BuildType>())
            BuildButton.AddItem(Loc.GetString($"build-type-{build.ToString().ToLowerInvariant()}"), (int) build);
        BuildButton.OnItemSelected += args =>
        {
            if (_loadingCmuControls)
                return;
            BuildButton.SelectId(args.Id);
            Profile = Profile?.WithBuild((BuildType) args.Id);
            SetDirty();
        };
        HideMetaInformationButton.OnToggled += args =>
        {
            if (!_loadingCmuControls)
                Profile = Profile?.WithHideMetaInformation(args.Button.Pressed);
            UpdateHideMetaInformationButtonText();
            SetDirty();
        };

        SkinToneColorSelector.SelectorType = ColorSelectorSliders.ColorSelectorType.Hsv;
        HairColorSelector.SelectorType = ColorSelectorSliders.ColorSelectorType.Hsv;
        CharacterEyeColorSelector.SelectorType = ColorSelectorSliders.ColorSelectorType.Hsv;
        SkinToneColorButton.OnPressed += _ =>
            SkinToneColorSelector.Visible = !SkinToneColorSelector.Visible;
        HairColorButton.OnPressed += _ =>
            HairColorSelector.Visible = !HairColorSelector.Visible;
        EyeColorButton.OnPressed += _ =>
            CharacterEyeColorSelector.Visible = !CharacterEyeColorSelector.Visible;
        SkinToneColorSelector.OnColorChanged += color =>
        {
            if (_loadingCmuControls || Profile == null)
                return;

            var coloration = _prototypeManager.Index(Profile.Species).SkinColoration;
            var strategy = _prototypeManager.Index(coloration).Strategy;
            var skinColor = strategy.ClosestSkinColor(color);
            _markingsModel.SetOrganSkinColor(skinColor);
            Profile = Profile.WithCharacterAppearance(Profile.Appearance.WithSkinColor(skinColor));
            UpdateSkinColor();
            UpdateCmuColorControls();
            ReloadProfilePreview();
            SetDirty();
        };
        HairColorSelector.OnColorChanged += color =>
        {
            if (_loadingCmuControls || Profile == null ||
                !TryGetLayerMarkings(HumanoidVisualLayers.Hair, out _, out var hair) ||
                hair.Count == 0 ||
                hair[0].MarkingColors.Count == 0)
                return;

            hair[0].SetColor(0, color);
            OnMarkingChange();
            UpdateHairPicker(HairStylePicker, HumanoidVisualLayers.Hair);
            UpdateCmuColorControls();
        };
        CharacterEyeColorSelector.OnColorChanged += color =>
        {
            if (_loadingCmuControls || Profile == null)
                return;

            Profile = Profile.WithCharacterAppearance(Profile.Appearance.WithEyeColor(color));
            _markingsModel.SetOrganEyeColor(color);
            EyeColorPicker.SetData(color);
            UpdateCmuColorControls();
            ReloadProfilePreview();
            SetDirty();
        };

        InitializeHairPicker(HairStylePicker, HumanoidVisualLayers.Hair);
        InitializeHairPicker(FacialHairPicker, HumanoidVisualLayers.FacialHair);

        RegulationHairStylePicker.MarkingWhitelist = HairStyles.RegulationHairStyles.Select(id => id.Id).ToHashSet();
        RegulationHairStylePicker.DropdownColors = HairStyles.RegulationHairColors;
        RegulationFacialHairPicker.MarkingWhitelist = HairStyles.RegulationFacialHairStyles.Select(id => id.Id).ToHashSet();
        RegulationFacialHairPicker.DropdownColors = HairStyles.RegulationHairColors;
        RegulationAppearanceInfo.SetMarkup(Loc.GetString("humanoid-profile-editor-regulation-appearance-info"));
        InitializeRegulationPicker(RegulationHairStylePicker, false);
        InitializeRegulationPicker(RegulationFacialHairPicker, true);
    }

    private void InitializeRegulationPicker(SingleMarkingPicker picker, bool facial)
    {
        picker.OnMarkingSelect += selected =>
        {
            if (Profile == null)
                return;
            var appearance = facial
                ? Profile.Appearance.WithRegulationFacialHairStyleName(selected.id)
                : Profile.Appearance.WithRegulationHairStyleName(selected.id);
            Profile = Profile.WithCharacterAppearance(appearance);
            UpdateRegulationPickers();
            ReloadPreview();
            SetDirty();
        };
        picker.OnColorChanged += selected =>
        {
            if (Profile == null || selected.marking.MarkingColors.Count == 0)
                return;
            var appearance = facial
                ? Profile.Appearance.WithRegulationFacialHairColor(selected.marking.MarkingColors[0])
                : Profile.Appearance.WithRegulationHairColor(selected.marking.MarkingColors[0]);
            Profile = Profile.WithCharacterAppearance(appearance);
            ReloadPreview();
            SetDirty();
        };
        picker.OnSlotRemove += _ =>
        {
            if (Profile == null)
                return;
            var appearance = facial
                ? Profile.Appearance.WithRegulationFacialHairStyleName(HairStyles.DefaultFacialHairStyle)
                : Profile.Appearance.WithRegulationHairStyleName(HairStyles.DefaultHairStyle);
            Profile = Profile.WithCharacterAppearance(appearance);
            UpdateRegulationPickers();
            ReloadPreview();
            SetDirty();
        };
        picker.OnSlotAdd += () =>
        {
            if (Profile == null)
                return;
            var first = (facial ? HairStyles.RegulationFacialHairStyles : HairStyles.RegulationHairStyles)
                .FirstOrDefault(id => _markingManager.Markings.ContainsKey(id));
            if (first.Id == null)
                return;
            var appearance = facial
                ? Profile.Appearance.WithRegulationFacialHairStyleName(first.Id)
                : Profile.Appearance.WithRegulationHairStyleName(first.Id);
            Profile = Profile.WithCharacterAppearance(appearance);
            UpdateRegulationPickers();
            ReloadPreview();
            SetDirty();
        };
    }

    private void InitializeHairPicker(SingleMarkingPicker picker, HumanoidVisualLayers layer)
    {
        picker.OnMarkingSelect += _ =>
        {
            OnMarkingChange();
            UpdateCmuColorControls();
        };
        picker.OnColorChanged += _ =>
        {
            OnMarkingChange();
            UpdateCmuColorControls();
        };
        picker.OnSlotRemove += slot =>
        {
            if (TryGetLayerMarkings(layer, out var organ, out var markings) && slot >= 0 && slot < markings.Count)
            {
                _markingsModel.TryDeselectMarking(organ, layer, markings[slot].MarkingId);
                OnMarkingChange();
                UpdateHairPicker(picker, layer);
                UpdateCmuColorControls();
            }
        };
        picker.OnSlotAdd += () =>
        {
            if (!TryGetLayerMarkings(layer, out var organ, out _))
                return;
            var first = _markingManager.MarkingsByLayerAndGroupAndSex(
                    layer,
                    _markingsModel.OrganData[organ].Group,
                    Profile?.Sex ?? Sex.Unsexed)
                .Keys.FirstOrDefault();
            if (first != null)
            {
                _markingsModel.TrySelectMarking(organ, layer, first);
                OnMarkingChange();
                UpdateHairPicker(picker, layer);
                UpdateCmuColorControls();
            }
        };
    }

    private bool TryGetLayerMarkings(
        HumanoidVisualLayers layer,
        out ProtoId<OrganCategoryPrototype> organ,
        out List<Marking> markings)
    {
        foreach (var (candidate, data) in _markingsModel.OrganData)
        {
            if (!data.Layers.Contains(layer))
                continue;

            organ = candidate;
            var byLayer = _markingsModel.Markings.GetOrNew(candidate);
            markings = byLayer.GetOrNew(layer);
            return true;
        }

        organ = default;
        markings = [];
        return false;
    }

    private void UpdateCmuControls()
    {
        _loadingCmuControls = true;
        RefreshAllegiances();
        RefreshOrigins();
        ShortExamineEdit.Text = Profile?.ShortExamine ?? string.Empty;
        var height = (Profile?.Height ?? string.Empty).Split('\'');
        _loadingHeightControls = true;
        HeightFeetEdit.Text = height.Length == 2 ? height[0] : string.Empty;
        HeightInchesEdit.Text = height.Length == 2 ? height[1] : string.Empty;
        _loadingHeightControls = false;
        WeightEdit.Text = (Profile?.Weight ?? 160).ToString();
        FullDescriptionEdit.TextRope = new Rope.Leaf(Profile?.FullDescription ?? string.Empty);
        MedicalRecordEdit.TextRope = new Rope.Leaf(Profile?.MedicalRecord ?? string.Empty);
        CriminalRecordEdit.TextRope = new Rope.Leaf(Profile?.CriminalRecord ?? string.Empty);
        GeneralRecordEdit.TextRope = new Rope.Leaf(Profile?.GeneralRecord ?? string.Empty);
        BuildButton.SelectId((int) (Profile?.Build ?? BuildType.Average));
        HideMetaInformationButton.Pressed = Profile?.HideMetaInformation ?? false;
        UpdateHideMetaInformationButtonText();
        UpdateHairPicker(HairStylePicker, HumanoidVisualLayers.Hair);
        UpdateHairPicker(FacialHairPicker, HumanoidVisualLayers.FacialHair);
        UpdateRegulationPickers();
        UpdateCmuColorControls();
        _loadingCmuControls = false;
    }

    private void UpdateCmuColorControls()
    {
        if (Profile == null)
            return;

        SkinToneColorButton.Text = NamedColorHelper.NearestColorName(Profile.Appearance.SkinColor);
        EyeColorButton.Text = NamedColorHelper.NearestColorName(Profile.Appearance.EyeColor);
        SkinToneColorSelector.Color = Profile.Appearance.SkinColor;
        CharacterEyeColorSelector.Color = Profile.Appearance.EyeColor;

        HairColorButton.Text = Loc.GetString("humanoid-profile-editor-color-unavailable");
        HairColorButton.Disabled = true;
        if (!TryGetLayerMarkings(HumanoidVisualLayers.Hair, out _, out var hair) ||
            hair.Count == 0 ||
            hair[0].MarkingColors.Count == 0)
            return;

        HairColorButton.Disabled = false;
        HairColorButton.Text = NamedColorHelper.NearestColorName(hair[0].MarkingColors[0]);
        HairColorSelector.Color = hair[0].MarkingColors[0];
    }

    private void UpdateRegulationPickers()
    {
        if (Profile == null)
            return;
        var appearance = Profile.Appearance;
        var hair = appearance.RegulationHairStyleId == HairStyles.DefaultHairStyle
            ? new List<Marking>()
            : new List<Marking>
            {
                new(appearance.RegulationHairStyleId, new List<Color> { appearance.RegulationHairColor }),
            };
        var facial = appearance.RegulationFacialHairStyleId == HairStyles.DefaultFacialHairStyle
            ? new List<Marking>()
            : new List<Marking>
            {
                new(appearance.RegulationFacialHairStyleId,
                    new List<Color> { appearance.RegulationFacialHairColor }),
            };
        RegulationHairStylePicker.UpdateData(hair, Profile.Species, 1);
        RegulationFacialHairPicker.UpdateData(facial, Profile.Species, 1);
    }

    private void UpdateHairPicker(SingleMarkingPicker picker, HumanoidVisualLayers layer)
    {
        if (Profile == null || !TryGetLayerMarkings(layer, out _, out var markings))
            return;
        picker.UpdateData(markings, Profile.Species, 1);
    }

    private void UpdateDescriptionField(Func<HumanoidCharacterProfile, HumanoidCharacterProfile> update)
    {
        if (_loadingCmuControls || Profile == null)
            return;
        Profile = update(Profile);
        SetDirty();
    }

    private void UpdateHeightFromEdits()
    {
        if (_loadingHeightControls || _loadingCmuControls || Profile == null)
            return;
        var feet = HeightFeetEdit.Text;
        var inches = HeightInchesEdit.Text;
        Profile = Profile.WithHeight(feet.Length == 1 && inches.Length is 1 or 2
            ? $"{feet}'{inches}"
            : string.Empty);
        SetDirty();
    }

    private void UpdateHideMetaInformationButtonText()
    {
        HideMetaInformationButton.Text = Loc.GetString(HideMetaInformationButton.Pressed
            ? "humanoid-profile-editor-hide-meta-true"
            : "humanoid-profile-editor-hide-meta-false");
    }

    public void RefreshAllegiances()
    {
        AllegianceButton.Clear();
        _allegiances.Clear();
        AllegianceButton.AddItem(Loc.GetString("humanoid-profile-editor-allegiance-none"), 0);
        _allegiances.AddRange(_prototypeManager.EnumeratePrototypes<AllegiancePrototype>()
            .Where(proto => proto.RoundStart)
            .OrderBy(proto => Loc.GetString(proto.Name)));
        for (var i = 0; i < _allegiances.Count; i++)
            AllegianceButton.AddItem(Loc.GetString(_allegiances[i].Name), i + 1);
        var selected = _allegiances.FindIndex(proto => proto.ID == Profile?.Allegiance?.Id);
        AllegianceButton.SelectId(selected + 1);
    }

    public void RefreshOrigins()
    {
        OriginButton.Clear();
        _origins.Clear();
        OriginButton.AddItem(Loc.GetString("humanoid-profile-editor-origin-none"), 0);
        _origins.AddRange(_prototypeManager.EnumeratePrototypes<OriginPrototype>()
            .Where(proto => proto.RoundStart)
            .OrderBy(proto => Loc.GetString(proto.Name)));
        for (var i = 0; i < _origins.Count; i++)
            OriginButton.AddItem(Loc.GetString(_origins[i].Name), i + 1);
        var selected = _origins.FindIndex(proto => proto.ID == Profile?.Origin?.Id);
        OriginButton.SelectId(selected + 1);
    }
}
