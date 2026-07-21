using Content.Client._RMC14.NamedItems;
using Content.Shared._RMC14.Marines.Squads;
using Content.Shared._RMC14.NamedItems;
using Content.Shared.Humanoid;
using Content.Shared.Preferences;
using Robust.Shared.Prototypes;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private readonly List<EntityPrototype> _rmcSquadPreferences = [];

    private void InitializeRmcControls()
    {
        Markings.SetIgnoredLayers(HumanoidVisualLayers.Hair, HumanoidVisualLayers.FacialHair);
        HairStylePicker.SetIncludedLayers(HumanoidVisualLayers.Hair);
        HairStylePicker.SetModel(_markingsModel);
        FacialHairPicker.SetIncludedLayers(HumanoidVisualLayers.FacialHair);
        FacialHairPicker.SetModel(_markingsModel);

        foreach (var value in Enum.GetValues<ArmorPreference>())
        {
            ArmorPreferenceButton.AddItem(value.ToString(), (int) value);
        }

        ArmorPreferenceButton.OnItemSelected += args =>
        {
            ArmorPreferenceButton.SelectId(args.Id);
            Profile = Profile?.WithArmorPreference((ArmorPreference) args.Id);
            ReloadPreview();
        };

        SquadPreferenceButton.AddItem(Loc.GetString("loadout-none"), 0);
        foreach (var squadProto in _entManager.System<SquadSystem>().SquadPrototypes)
        {
            if (!squadProto.TryComp(out SquadTeamComponent? team, _entManager.ComponentFactory) ||
                !team.RoundStart)
            {
                continue;
            }

            _rmcSquadPreferences.Add(squadProto);
            SquadPreferenceButton.AddItem(squadProto.Name, _rmcSquadPreferences.Count);
        }

        SquadPreferenceButton.OnItemSelected += args =>
        {
            SquadPreferenceButton.SelectId(args.Id);
            Profile = args.Id == 0
                ? Profile?.WithSquadPreference(null)
                : Profile?.WithSquadPreference(_rmcSquadPreferences[args.Id - 1].ID);
            SetDirty();
        };

        PlaytimePerksButton.OnPressed += args =>
        {
            Profile = Profile?.WithPlaytimePerks(args.Button.Pressed);
            SetDirty();
        };

        XenoPrefix.OnTextChanged += args =>
        {
            Profile = Profile?.WithXenoPrefix(args.Text);
            SetDirty();
        };
        XenoPostfix.OnTextChanged += args =>
        {
            Profile = Profile?.WithXenoPostfix(args.Text);
            SetDirty();
        };

        NamedItems.PrimaryGun.OnTextChanged += args => SetRmcNamedItem(RMCNamedItemType.PrimaryGun, args.Text);
        NamedItems.Sidearm.OnTextChanged += args => SetRmcNamedItem(RMCNamedItemType.Sidearm, args.Text);
        NamedItems.Helmet.OnTextChanged += args => SetRmcNamedItem(RMCNamedItemType.Helmet, args.Text);
        NamedItems.Armor.OnTextChanged += args => SetRmcNamedItem(RMCNamedItemType.Armor, args.Text);
        NamedItems.Sentry.OnTextChanged += args => SetRmcNamedItem(RMCNamedItemType.Sentry, args.Text);

        var namedItems = UserInterfaceManager.GetUIController<NamedItemsUIController>();
        TabContainer.SetTabTitle(5, Loc.GetString("rmc-ui-named-items"));
        TabContainer.SetTabVisible(5, namedItems.Available);

        // RMC does not use upstream antagonist preferences.
        TabContainer.SetTabVisible(2, false);
    }

    private void UpdateRmcControls()
    {
        if (Profile is null)
            return;

        ArmorPreferenceButton.SelectId((int) Profile.ArmorPreference);

        var squadIndex = 0;
        if (Profile.SquadPreference is { } preference)
        {
            var index = _rmcSquadPreferences.FindIndex(squad => squad.ID == preference.Id);
            if (index >= 0)
                squadIndex = index + 1;
        }
        SquadPreferenceButton.SelectId(squadIndex);

        PlaytimePerksButton.Pressed = Profile.PlaytimePerks;
        XenoPrefix.Text = Profile.XenoPrefix;
        XenoPostfix.Text = Profile.XenoPostfix;

        NamedItems.PrimaryGun.Text = Profile.NamedItems.PrimaryGunName ?? string.Empty;
        NamedItems.Sidearm.Text = Profile.NamedItems.SidearmName ?? string.Empty;
        NamedItems.Helmet.Text = Profile.NamedItems.HelmetName ?? string.Empty;
        NamedItems.Armor.Text = Profile.NamedItems.ArmorName ?? string.Empty;
        NamedItems.Sentry.Text = Profile.NamedItems.SentryName ?? string.Empty;
    }

    private void SetRmcNamedItem(RMCNamedItemType type, string itemName)
    {
        if (Profile is null)
            return;

        var namedItems = Profile.NamedItems;
        Profile = Profile.WithNamedItems(new SharedRMCNamedItems(
            type == RMCNamedItemType.PrimaryGun ? itemName : namedItems.PrimaryGunName,
            type == RMCNamedItemType.Sidearm ? itemName : namedItems.SidearmName,
            type == RMCNamedItemType.Helmet ? itemName : namedItems.HelmetName,
            type == RMCNamedItemType.Armor ? itemName : namedItems.ArmorName,
            type == RMCNamedItemType.Sentry ? itemName : namedItems.SentryName));
        SetDirty();
    }
}
