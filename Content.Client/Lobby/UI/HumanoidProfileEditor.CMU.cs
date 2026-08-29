using System.Linq;
using Content.Client._RMC14.NamedItems;
using Content.Client.Lobby.UI.Roles;
using Content.Client.Message;
using Content.Client.Stylesheets;
using Content.Shared._CMU14.CharacterDescription;
using Content.Shared._CMU14.Roles;
using Content.Shared._RMC14.LinkAccount;
using Content.Shared._RMC14.Marines.Squads;
using Content.Shared._RMC14.NamedItems;
using Content.Shared._RMC14.Prototypes;
using Content.Shared.AU14.Allegiance;
using Content.Shared.AU14.Origin;
using Content.Shared.Body;
using Content.Shared._CMU14.Threats;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private const int CharacterDescriptionTabIndex = 1;
    private const int RegulationAppearanceTabIndex = 2;
    private const int InsurgencyTabIndex = 3;
    private const int ColonyFallTabIndex = 4;
    private const int DistressSignalTabIndex = 5;
    private const int TraitsTabIndex = 6;
    private const int MarkingsTabIndex = 7;
    private const int NamedItemsTabIndex = 8;

    private const float HighJobPreviewScrollDelay = 2.75f;
    private const string GamemodeInsurgency = "Insurgency";
    private const string GamemodeColonyFall = "ColonyFall";
    private const string GamemodeDistressSignal = "DistressSignal";

    private readonly List<AllegiancePrototype> _allegiances = new();
    private readonly List<OriginPrototype> _origins = new();
    private readonly List<(string Gamemode, string Id, Button Yes, Button No)> _threatPreferenceButtons = new();
    private readonly List<LobbyHighJobPreviewEntry> _previewJobs = new();

    private IComponentFactory _componentFactory = default!;
    private bool _allowCharacterDescription;
    private bool _loadingHeightControls;
    private int _previewJobIndex;
    private float _previewJobTimer;
    private string _previewJobSignature = string.Empty;
    private bool _previewJobsDirty = true;

    private void InitializeCmu()
    {
        _componentFactory = IoCManager.Resolve<IComponentFactory>();

        TabContainer.SetTabTitle(CharacterDescriptionTabIndex,
            Loc.GetString("humanoid-profile-editor-character-description-tab"));
        TabContainer.SetTabVisible(CharacterDescriptionTabIndex, _allowCharacterDescription);
        TabContainer.SetTabTitle(RegulationAppearanceTabIndex,
            Loc.GetString("humanoid-profile-editor-regulation-appearance-tab"));
        TabContainer.SetTabTitle(InsurgencyTabIndex, Loc.GetString("humanoid-profile-editor-insurgency-tab"));
        TabContainer.SetTabTitle(ColonyFallTabIndex, Loc.GetString("humanoid-profile-editor-colony-fall-tab"));
        TabContainer.SetTabTitle(DistressSignalTabIndex,
            Loc.GetString("humanoid-profile-editor-distress-signal-tab"));
        TabContainer.SetTabTitle(TraitsTabIndex, Loc.GetString("humanoid-profile-editor-traits-tab"));
        TabContainer.SetTabTitle(MarkingsTabIndex, Loc.GetString("humanoid-profile-editor-markings-tab"));
        SetupGamemodeTabTitles();
        TabContainer.OnTabChanged += _ => ReloadPreview(false);

        RefreshAllegiances();
        AllegianceButton.OnItemSelected += args =>
        {
            AllegianceButton.SelectId(args.Id);
            SetAllegiance(args.Id == 0 ? null : _allegiances[args.Id - 1].ID);
        };

        RefreshOrigins();
        OriginButton.OnItemSelected += args =>
        {
            OriginButton.SelectId(args.Id);
            SetOrigin(args.Id == 0 ? null : _origins[args.Id - 1].ID);
        };

        InitializeCharacterDescription();
        InitializeRegulationAppearance();

        RefreshSynthetic();

        foreach (var value in Enum.GetValues<ArmorPreference>())
            ArmorPreferenceButton.AddItem(value.ToString(), (int)value);

        ArmorPreferenceButton.OnItemSelected += args =>
        {
            ArmorPreferenceButton.SelectId(args.Id);
            SetArmorPreference((ArmorPreference)args.Id);
        };

        SquadPreferenceButton.AddItem(Loc.GetString("loadout-none"), 0);
        var squad = _entManager.System<SquadSystem>();
        for (var i = 0; i < squad.SquadPrototypes.Length; i++)
        {
            var squadProto = squad.SquadPrototypes[i];
            if (!squadProto.TryComp(out SquadTeamComponent? team, _componentFactory) || !team.RoundStart)
                continue;

            SquadPreferenceButton.AddItem(squadProto.Name, i + 1);
        }

        SquadPreferenceButton.OnItemSelected += args =>
        {
            SquadPreferenceButton.SelectId(args.Id);
            if (args.Id == 0)
            {
                SetSquadPreference(null);
                return;
            }

            if (squad.SquadPrototypes.TryGetValue(args.Id - 1, out var proto))
                SetSquadPreference(proto.ID);
        };

        PlaytimePerksButton.OnPressed += args => SetPlaytimePerks(args.Button.Pressed);
        XenoPrefix.OnTextChanged += args => SetXenoPrefix(args.Text);
        XenoPostfix.OnTextChanged += args => SetXenoPostfix(args.Text);

        RefreshThreatPreferences();
        InitializeNamedItems();
        CrtLobbyTheme.Apply(this);
    }

    private void InitializeCharacterDescription()
    {
        ShortExamineEdit.OnTextChanged += args => SetShortExamine(args.Text);

        HeightFeetEdit.IsValid = text =>
            text.Length <= 1 && (text.Length == 0 || text[0] is >= '4' and <= '6');
        HeightInchesEdit.IsValid = text =>
            text.Length <= 2 && text.All(char.IsDigit) && (text.Length == 0 || int.Parse(text) <= 11);
        HeightFeetEdit.OnTextChanged += _ => UpdateHeightFromEdits();
        HeightInchesEdit.OnTextChanged += _ => UpdateHeightFromEdits();

        WeightEdit.OnTextChanged += args =>
        {
            if (int.TryParse(args.Text, out var newWeight))
                SetWeight(newWeight);
        };

        FullDescriptionEdit.OnTextChanged += _ => SetFullDescription(Rope.Collapse(FullDescriptionEdit.TextRope));
        MedicalRecordEdit.OnTextChanged += _ => SetMedicalRecord(Rope.Collapse(MedicalRecordEdit.TextRope));
        CriminalRecordEdit.OnTextChanged += _ => SetCriminalRecord(Rope.Collapse(CriminalRecordEdit.TextRope));
        GeneralRecordEdit.OnTextChanged += _ => SetGeneralRecord(Rope.Collapse(GeneralRecordEdit.TextRope));

        foreach (var build in Enum.GetValues<BuildType>())
            BuildButton.AddItem(Loc.GetString($"build-type-{build.ToString().ToLowerInvariant()}"), (int)build);

        BuildButton.OnItemSelected += args =>
        {
            BuildButton.SelectId(args.Id);
            SetBuild((BuildType)args.Id);
        };

        HideMetaInformationButton.OnToggled += args =>
        {
            SetHideMetaInformation(args.Button.Pressed);
            UpdateHideMetaInformationButtonText();
        };
    }

    private void InitializeRegulationAppearance()
    {
        RegulationHairStylePicker.MarkingWhitelist = HairStyles.RegulationHairStyles.Select(style => style.Id).ToHashSet();
        RegulationHairStylePicker.DropdownColors = HairStyles.RegulationHairColors;
        RegulationHairStylePicker.DefaultMarkingId = HairStyles.DefaultHairStyle;
        RegulationFacialHairPicker.MarkingWhitelist = HairStyles.RegulationFacialHairStyles.Select(style => style.Id).ToHashSet();
        RegulationFacialHairPicker.DropdownColors = HairStyles.RegulationHairColors;
        RegulationFacialHairPicker.DefaultMarkingId = HairStyles.DefaultFacialHairStyle;
        RegulationAppearanceInfo.SetMarkup(Loc.GetString("humanoid-profile-editor-regulation-appearance-info"));

        RegulationHairStylePicker.OnMarkingChanged += style =>
        {
            if (Profile is null)
                return;

            Profile = Profile.WithCharacterAppearance(Profile.Appearance.WithRegulationHairStyleName(style));
            ReloadPreview();
        };
        RegulationHairStylePicker.OnColorChanged += color =>
        {
            if (Profile is null)
                return;

            Profile = Profile.WithCharacterAppearance(
                Profile.Appearance.WithRegulationHairColor(color));
            ReloadPreview();
        };
        RegulationFacialHairPicker.OnMarkingChanged += style =>
        {
            if (Profile is null)
                return;

            Profile = Profile.WithCharacterAppearance(
                Profile.Appearance.WithRegulationFacialHairStyleName(style));
            ReloadPreview();
        };
        RegulationFacialHairPicker.OnColorChanged += color =>
        {
            if (Profile is null)
                return;

            Profile = Profile.WithCharacterAppearance(
                Profile.Appearance.WithRegulationFacialHairColor(color));
            ReloadPreview();
        };
    }

    private void InitializeNamedItems()
    {
        void SetItemName(RMCNamedItemType type, string itemName)
        {
            if (Profile is null)
                return;

            Profile = Profile.WithNamedItems(new SharedRMCNamedItems
            {
                PrimaryGunName = type == RMCNamedItemType.PrimaryGun ? itemName : Profile.NamedItems.PrimaryGunName,
                SidearmName = type == RMCNamedItemType.Sidearm ? itemName : Profile.NamedItems.SidearmName,
                HelmetName = type == RMCNamedItemType.Helmet ? itemName : Profile.NamedItems.HelmetName,
                ArmorName = type == RMCNamedItemType.Armor ? itemName : Profile.NamedItems.ArmorName,
                SentryName = type == RMCNamedItemType.Sentry ? itemName : Profile.NamedItems.SentryName,
            });
            SetDirty();
        }

        var namedItems = UserInterfaceManager.GetUIController<NamedItemsUIController>();
        TabContainer.SetTabTitle(NamedItemsTabIndex, Loc.GetString("rmc-ui-named-items"));
        TabContainer.SetTabVisible(NamedItemsTabIndex, namedItems.Available);
        NamedItems.PrimaryGun.OnTextChanged += args => SetItemName(RMCNamedItemType.PrimaryGun, args.Text);
        NamedItems.Sidearm.OnTextChanged += args => SetItemName(RMCNamedItemType.Sidearm, args.Text);
        NamedItems.Helmet.OnTextChanged += args => SetItemName(RMCNamedItemType.Helmet, args.Text);
        NamedItems.Armor.OnTextChanged += args => SetItemName(RMCNamedItemType.Armor, args.Text);
        NamedItems.Sentry.OnTextChanged += args => SetItemName(RMCNamedItemType.Sentry, args.Text);
    }

    private void SetupGamemodeTabTitles()
    {
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
    }

    private void UpdateCmuControls()
    {
        MarkPreviewJobsDirty();
        UpdateRegulationHairPickers();
        UpdateAllegianceControls();
        UpdateOriginControls();
        UpdateArmorPreferenceControls();
        UpdateSquadPreferenceControls();
        UpdateNamedItems();
        UpdatePlaytimePerks();
        UpdateCharacterDescriptionControls();
        UpdateXenoPrefix();
        UpdateXenoPostfix();
        RefreshSynthetic();
        RefreshThreatPreferences();
    }

    public void RefreshRMC(SharedRMCPatronTier? tier)
    {
        TabContainer.SetTabVisible(NamedItemsTabIndex, tier is { NamedItems: true });
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        UpdatePreviewJobRotation(args.DeltaSeconds);
    }

    private HumanoidCharacterProfile GetPreviewProfile(HumanoidCharacterProfile profile)
    {
        if (TabContainer.CurrentTab != RegulationAppearanceTabIndex)
            return profile;

        return GetRegulationPreviewProfile(profile, _markingManager);
    }

    internal static HumanoidCharacterProfile GetRegulationPreviewProfile(
        HumanoidCharacterProfile profile,
        MarkingManager markingManager)
    {
        var markings = profile.Appearance.Markings.ToDictionary(
            organ => organ.Key,
            organ => organ.Value.ToDictionary(
                layer => layer.Key,
                layer => layer.Value.Select(CloneMarking).ToList()));
        var organData = markingManager.GetMarkingData(profile.Species);

        SubstituteRegulationLayer(
            markings,
            organData,
            HumanoidVisualLayers.Hair,
            profile.Appearance.RegulationHairStyleId,
            HairStyles.DefaultHairStyle,
            profile.Appearance.RegulationHairColor);
        SubstituteRegulationLayer(
            markings,
            organData,
            HumanoidVisualLayers.FacialHair,
            profile.Appearance.RegulationFacialHairStyleId,
            HairStyles.DefaultFacialHairStyle,
            profile.Appearance.RegulationFacialHairColor);

        return profile.WithCharacterAppearance(profile.Appearance.WithMarkings(markings));
    }

    private static Marking CloneMarking(Marking marking)
    {
        return new Marking(marking.MarkingId, new List<Color>(marking.MarkingColors))
        {
            Forced = marking.Forced,
        };
    }

    private static void SubstituteRegulationLayer(
        Dictionary<ProtoId<OrganCategoryPrototype>, Dictionary<HumanoidVisualLayers, List<Marking>>> markings,
        Dictionary<ProtoId<OrganCategoryPrototype>, OrganMarkingData> organData,
        HumanoidVisualLayers layer,
        string markingId,
        string defaultMarkingId,
        Color color)
    {
        foreach (var (category, data) in organData)
        {
            if (!data.Layers.Contains(layer))
                continue;

            if (!markings.TryGetValue(category, out var layers))
                markings[category] = layers = new Dictionary<HumanoidVisualLayers, List<Marking>>();

            if (markingId == defaultMarkingId)
                layers.Remove(layer);
            else
            {
                var colors = new List<Color>();
                colors.Add(color);

                var regulationMarkings = new List<Marking>();
                regulationMarkings.Add(new Marking(markingId, colors));
                layers[layer] = regulationMarkings;
            }

            return;
        }
    }

    private void UpdatePreviewJobRotation(float deltaSeconds)
    {
        if (Profile == null || JobOverride != null || !ShowClothes.Pressed)
        {
            ResetPreviewJobRotation();
            UpdatePreviewJobLabel(null);
            return;
        }

        if (_previewJobsDirty && RefreshPreviewJobs())
        {
            ReloadPreview(false);
            return;
        }

        if (_previewJobs.Count <= 1)
        {
            if (_previewJobs.Count == 1)
                UpdatePreviewJobLabel(_previewJobs[0]);
            return;
        }

        _previewJobTimer += deltaSeconds;
        if (_previewJobTimer < HighJobPreviewScrollDelay)
            return;

        _previewJobTimer -= HighJobPreviewScrollDelay;
        _previewJobIndex = (_previewJobIndex + 1) % _previewJobs.Count;
        ReloadPreview(false);
    }

    private LobbyHighJobPreviewEntry? GetCurrentPreviewJob()
    {
        if (Profile == null || JobOverride != null || !ShowClothes.Pressed)
            return null;

        if (_previewJobsDirty)
            RefreshPreviewJobs();
        if (_previewJobs.Count == 0)
        {
            _previewJobIndex = 0;
            return null;
        }

        _previewJobIndex %= _previewJobs.Count;
        return _previewJobs[_previewJobIndex];
    }

    private bool RefreshPreviewJobs()
    {
        var previousSignature = _previewJobSignature;
        _previewJobs.Clear();
        if (Profile != null)
            _previewJobs.AddRange(LobbyHighJobPreview.GetHighPriorityJobs(Profile, _prototypeManager));

        _previewJobSignature = LobbyHighJobPreview.GetSignature(_previewJobs);
        _previewJobsDirty = false;
        if (previousSignature == _previewJobSignature)
            return false;

        _previewJobIndex = 0;
        _previewJobTimer = 0;
        return true;
    }

    private void ResetPreviewJobRotation()
    {
        _previewJobIndex = 0;
        _previewJobTimer = 0;
        _previewJobSignature = string.Empty;
        _previewJobs.Clear();
        _previewJobsDirty = true;
    }

    private void MarkPreviewJobsDirty()
    {
        _previewJobsDirty = true;
    }

    private void UpdatePreviewJobLabel(LobbyHighJobPreviewEntry? entry)
    {
        if (!ShowClothes.Pressed)
        {
            PreviewJobLabel.Visible = false;
            PreviewJobLabel.Text = string.Empty;
            return;
        }

        if (JobOverride != null)
        {
            PreviewJobLabel.Text = JobOverride.LocalizedName;
            PreviewJobLabel.Visible = true;
            return;
        }

        PreviewJobLabel.Text = entry?.DisplayName ?? string.Empty;
        PreviewJobLabel.Visible = entry != null;
    }

    private void SetArmorPreference(ArmorPreference value)
    {
        Profile = Profile?.WithArmorPreference(value);
        SetDirty();
    }

    private void SetSquadPreference(EntProtoId<SquadTeamComponent>? value)
    {
        Profile = Profile?.WithSquadPreference(value);
        SetDirty();
    }

    private void SetPlaytimePerks(bool value)
    {
        Profile = Profile?.WithPlaytimePerks(value);
        SetDirty();
    }

    private void SetXenoPrefix(string value)
    {
        Profile = Profile?.WithXenoPrefix(value);
        SetDirty();
    }

    private void SetXenoPostfix(string value)
    {
        Profile = Profile?.WithXenoPostfix(value);
        SetDirty();
    }

    private void SetAllegiance(string? value)
    {
        Profile = Profile?.WithAllegiance(value is null
            ? (ProtoId<AllegiancePrototype>?) null
            : new ProtoId<AllegiancePrototype>(value));
        SetDirty();
    }

    private void SetOrigin(string? value)
    {
        Profile = Profile?.WithOrigin(value is null
            ? (ProtoId<OriginPrototype>?) null
            : new ProtoId<OriginPrototype>(value));
        SetDirty();
    }

    private void SetShortExamine(string value)
    {
        Profile = Profile?.WithShortExamine(value);
        SetDirty();
    }

    private void SetFullDescription(string value)
    {
        Profile = Profile?.WithFullDescription(value);
        SetDirty();
    }

    private void SetMedicalRecord(string value)
    {
        Profile = Profile?.WithMedicalRecord(value);
        SetDirty();
    }

    private void SetCriminalRecord(string value)
    {
        Profile = Profile?.WithCriminalRecord(value);
        SetDirty();
    }

    private void SetGeneralRecord(string value)
    {
        Profile = Profile?.WithGeneralRecord(value);
        SetDirty();
    }

    private void SetCharacterHeight(string value)
    {
        Profile = Profile?.WithHeight(value);
        SetDirty();
    }

    private void UpdateHeightFromEdits()
    {
        if (_loadingHeightControls)
            return;

        var feet = HeightFeetEdit.Text;
        var inches = HeightInchesEdit.Text;
        SetCharacterHeight(feet.Length == 1 && inches.Length is 1 or 2 ? $"{feet}'{inches}" : string.Empty);
    }

    private void SetWeight(int value)
    {
        Profile = Profile?.WithWeight(value);
        SetDirty();
    }

    private void SetBuild(BuildType value)
    {
        Profile = Profile?.WithBuild(value);
        SetDirty();
    }

    private void SetHideMetaInformation(bool value)
    {
        Profile = Profile?.WithHideMetaInformation(value);
        SetDirty();
    }

    private void UpdateRegulationHairPickers()
    {
        if (Profile is null)
            return;

        RegulationHairStylePicker.UpdateData(
            Profile.Appearance.RegulationHairStyleId,
            Profile.Appearance.RegulationHairColor,
            Profile.Species,
            Profile.Sex);
        RegulationFacialHairPicker.UpdateData(
            Profile.Appearance.RegulationFacialHairStyleId,
            Profile.Appearance.RegulationFacialHairColor,
            Profile.Species,
            Profile.Sex);
    }

    private void UpdateAllegianceControls()
    {
        var selected = Profile?.Allegiance;
        AllegianceButton.SelectId(selected is null
            ? 0
            : _allegiances.FindIndex(allegiance => allegiance.ID == selected.Value.Id) + 1);
    }

    private void UpdateOriginControls()
    {
        var selected = Profile?.Origin;
        OriginButton.SelectId(selected is null
            ? 0
            : _origins.FindIndex(origin => origin.ID == selected.Value.Id) + 1);
    }

    private void UpdateArmorPreferenceControls()
    {
        if (Profile != null)
            ArmorPreferenceButton.SelectId((int)Profile.ArmorPreference);
    }

    private void UpdateSquadPreferenceControls()
    {
        if (Profile is null)
            return;

        var index = 0;
        if (Profile.SquadPreference is { } preference)
        {
            var squads = new List<EntityPrototype>(_entManager.System<SquadSystem>().SquadPrototypes)
                .Select(squad => squad.ID)
                .ToList();
            index = squads.IndexOf(preference.Id) + 1;
        }

        SquadPreferenceButton.SelectId(index);
    }

    private void UpdateNamedItems()
    {
        NamedItems.PrimaryGun.Text = Profile?.NamedItems.PrimaryGunName ?? string.Empty;
        NamedItems.Sidearm.Text = Profile?.NamedItems.SidearmName ?? string.Empty;
        NamedItems.Helmet.Text = Profile?.NamedItems.HelmetName ?? string.Empty;
        NamedItems.Armor.Text = Profile?.NamedItems.ArmorName ?? string.Empty;
        NamedItems.Sentry.Text = Profile?.NamedItems.SentryName ?? string.Empty;
    }

    private void UpdatePlaytimePerks()
    {
        PlaytimePerksButton.Pressed = Profile?.PlaytimePerks ?? true;
    }

    private void UpdateCharacterDescriptionControls()
    {
        ShortExamineEdit.Text = Profile?.ShortExamine ?? string.Empty;
        var heightParts = (Profile?.Height ?? string.Empty).Split('\'');
        _loadingHeightControls = true;
        HeightFeetEdit.Text = heightParts.Length == 2 ? heightParts[0] : string.Empty;
        HeightInchesEdit.Text = heightParts.Length == 2 ? heightParts[1] : string.Empty;
        _loadingHeightControls = false;

        WeightEdit.Text = (Profile?.Weight ?? 160).ToString();
        FullDescriptionEdit.TextRope = new Rope.Leaf(Profile?.FullDescription ?? string.Empty);
        MedicalRecordEdit.TextRope = new Rope.Leaf(Profile?.MedicalRecord ?? string.Empty);
        CriminalRecordEdit.TextRope = new Rope.Leaf(Profile?.CriminalRecord ?? string.Empty);
        GeneralRecordEdit.TextRope = new Rope.Leaf(Profile?.GeneralRecord ?? string.Empty);
        BuildButton.SelectId((int)(Profile?.Build ?? BuildType.Average));
        HideMetaInformationButton.Pressed = Profile?.HideMetaInformation ?? false;
        UpdateHideMetaInformationButtonText();

        SkinToneNameLabel.Text = Profile is null
            ? string.Empty
            : NamedColorHelper.NearestColorName(Profile.Appearance.SkinColor);
        HairColorNameLabel.Text = GetNormalHairColorName(Profile);
        EyeColorNameLabel.Text = Profile is null
            ? string.Empty
            : NamedColorHelper.NearestColorName(Profile.Appearance.EyeColor);
    }

    private static string GetNormalHairColorName(HumanoidCharacterProfile? profile)
    {
        if (profile is null)
            return string.Empty;

        foreach (var layers in profile.Appearance.Markings.Values)
        {
            if (layers.TryGetValue(HumanoidVisualLayers.Hair, out var markings) &&
                markings.Count > 0 &&
                markings[0].MarkingColors.Count > 0)
            {
                return NamedColorHelper.NearestColorName(markings[0].MarkingColors[0]);
            }
        }

        return string.Empty;
    }

    private void UpdateHideMetaInformationButtonText()
    {
        HideMetaInformationButton.Text = Loc.GetString(HideMetaInformationButton.Pressed
            ? "humanoid-profile-editor-hide-meta-true"
            : "humanoid-profile-editor-hide-meta-false");
    }

    private void UpdateXenoPrefix()
    {
        XenoPrefix.Text = Profile?.XenoPrefix ?? string.Empty;
    }

    private void UpdateXenoPostfix()
    {
        XenoPostfix.Text = Profile?.XenoPostfix ?? string.Empty;
    }

    public void RefreshSynthetic()
    {
        SyntheticContainer.DisposeAllChildren();
        var selector = new RequirementsSelector { Margin = new Thickness(3f, 3f, 3f, 0f) };
        selector.Setup(
            [("humanoid-profile-editor-synthetic-yes-button", 0),
             ("humanoid-profile-editor-synthetic-no-button", 1)],
            Loc.GetString("humanoid-profile-editor-synthetic-title"),
            250,
            Loc.GetString("humanoid-profile-editor-synthetic-description"));
        selector.Select(Profile?.Synthetic == true ? 0 : 1);

        var whitelisted = _prototypeManager.TryIndex<JobPrototype>(CMUSyntheticRoles.SyntheticWhitelistJob, out var marker)
            && _requirements.CheckWhitelist(marker, out _);
        if (!whitelisted)
        {
            selector.LockRequirements(FormattedMessage.FromUnformatted(
                Loc.GetString("humanoid-profile-editor-synthetic-locked")));
            if (Profile?.Synthetic == true)
            {
                Profile = Profile.WithSynthetic(false);
                SetDirty();
            }
        }
        else
        {
            selector.UnlockRequirements();
        }

        selector.OnSelected += preference =>
        {
            Profile = Profile?.WithSynthetic(preference == 0);
            SetDirty();
            RefreshJobs();
        };
        SyntheticContainer.AddChild(selector);
    }

    public void RefreshAllegiances()
    {
        AllegianceButton.Clear();
        _allegiances.Clear();
        AllegianceButton.AddItem(Loc.GetString("humanoid-profile-editor-allegiance-none"), 0);
        _allegiances.AddRange(_prototypeManager.EnumeratePrototypes<AllegiancePrototype>()
            .Where(allegiance => allegiance.RoundStart)
            .OrderBy(allegiance => Loc.GetString(allegiance.Name)));
        for (var i = 0; i < _allegiances.Count; i++)
            AllegianceButton.AddItem(Loc.GetString(_allegiances[i].Name), i + 1);
        UpdateAllegianceControls();
    }

    public void RefreshOrigins()
    {
        OriginButton.Clear();
        _origins.Clear();
        OriginButton.AddItem(Loc.GetString("humanoid-profile-editor-origin-none"), 0);
        _origins.AddRange(_prototypeManager.EnumeratePrototypes<OriginPrototype>()
            .Where(origin => origin.RoundStart)
            .OrderBy(origin => Loc.GetString(origin.Name)));
        for (var i = 0; i < _origins.Count; i++)
            OriginButton.AddItem(Loc.GetString(_origins[i].Name), i + 1);
        UpdateOriginControls();
    }

    public void RefreshThreatPreferences()
    {
        ColonyThreatPreferenceList.DisposeAllChildren();
        DistressThreatPreferenceList.DisposeAllChildren();
        _threatPreferenceButtons.Clear();
        PopulateThreatPreferenceList(ColonyThreatPreferenceList, GamemodeColonyFall);
        PopulateThreatPreferenceList(DistressThreatPreferenceList, GamemodeDistressSignal);
        SyncThreatPreferenceButtons();
        CrtLobbyTheme.Apply(ColonyThreatPreferenceList);
        CrtLobbyTheme.Apply(DistressThreatPreferenceList);
    }

    private void PopulateThreatPreferenceList(BoxContainer target, string gamemode)
    {
        target.AddChild(new Label
        {
            Text = "THREATS",
            Margin = new Thickness(6f, 4f, 0f, 6f),
            StyleClasses = { StyleNano.StyleClassCrtHeading },
        });

        foreach (var threat in _prototypeManager.EnumeratePrototypes<ThreatPrototype>()
                     .Where(threat => IsThreatVisibleForGamemode(threat, gamemode))
                     .OrderBy(GetThreatDisplayName))
        {
            var row = new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal,
                HorizontalExpand = true,
                Margin = new Thickness(6f, 0f, 6f, 4f),
                SeparationOverride = 4,
            };
            row.AddChild(new Label
            {
                Text = GetThreatDisplayName(threat),
                HorizontalExpand = true,
                ClipText = true,
                VerticalAlignment = VAlignment.Center,
                ToolTip = threat.ID,
                StyleClasses = { StyleNano.StyleClassCrtText },
            });

            var yes = MakeThreatPreferenceButton(Loc.GetString("humanoid-profile-editor-antag-preference-yes-button"));
            var no = MakeThreatPreferenceButton(Loc.GetString("humanoid-profile-editor-antag-preference-no-button"));
            yes.OnPressed += _ =>
            {
                SetThreatPreference(gamemode, threat.ID, true);
                SyncThreatPreferenceButtons();
            };
            no.OnPressed += _ =>
            {
                SetThreatPreference(gamemode, threat.ID, false);
                SyncThreatPreferenceButtons();
            };
            _threatPreferenceButtons.Add((gamemode, threat.ID, yes, no));
            row.AddChild(yes);
            row.AddChild(no);
            target.AddChild(row);
        }
    }

    private static Button MakeThreatPreferenceButton(string text)
    {
        return new Button
        {
            Text = text,
            ToggleMode = true,
            MinWidth = 54,
            StyleClasses = { StyleNano.StyleClassCrtButton },
        };
    }

    private void SetThreatPreference(string gamemode, string threat, bool value)
    {
        Profile = Profile?.WithGamemodeThreatPreference(gamemode, new ProtoId<ThreatPrototype>(threat), value);
        SetDirty();
    }

    private void SyncThreatPreferenceButtons()
    {
        foreach (var (gamemode, threat, yes, no) in _threatPreferenceButtons)
        {
            var selected = Profile?.GetThreatPreferencesForGamemode(gamemode).Any(id => id.Id == threat) == true;
            yes.Pressed = selected;
            no.Pressed = !selected;
        }
    }

    private static bool IsThreatVisibleForGamemode(ThreatPrototype threat, string gamemode)
    {
        if (threat.BlacklistedGamemodes.Any(mode => mode.Equals(gamemode, StringComparison.OrdinalIgnoreCase)))
            return false;
        return threat.whitelistedgamemodes.Count == 0 ||
               threat.whitelistedgamemodes.Any(mode => mode.Equals(gamemode, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetThreatDisplayName(ThreatPrototype threat)
    {
        var id = threat.ID;
        var suffix = string.Empty;
        if (id.EndsWith("OnMarker", StringComparison.OrdinalIgnoreCase))
        {
            id = id[..^"OnMarker".Length];
            suffix = " (Marker)";
        }

        if (id.EndsWith("CF", StringComparison.OrdinalIgnoreCase))
            id = id[..^2];
        if (id.EndsWith("Threat", StringComparison.OrdinalIgnoreCase))
            id = id[..^"Threat".Length];

        return id.ToLowerInvariant() switch
        {
            "xeno" => "Xenomorph" + suffix,
            "ape" => "Apes" + suffix,
            "cultist" => "Cultists" + suffix,
            "wendigo" => "Wendigo" + suffix,
            _ => HumanizePrototypeId(id) + suffix,
        };
    }

    private static string HumanizePrototypeId(string id)
    {
        var builder = new System.Text.StringBuilder(id.Length + 8);
        for (var i = 0; i < id.Length; i++)
        {
            var current = id[i];
            if (i > 0)
            {
                var previous = id[i - 1];
                var nextIsLower = i + 1 < id.Length && char.IsLower(id[i + 1]);
                if ((char.IsUpper(current) && (char.IsLower(previous) || nextIsLower)) ||
                    (char.IsDigit(current) && !char.IsDigit(previous)))
                {
                    builder.Append(' ');
                }
            }
            builder.Append(current);
        }
        return builder.ToString().Trim();
    }
}
