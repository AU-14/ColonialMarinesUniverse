using Content.Shared.Preferences;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private void InitializeForceOnForcePreferences()
    {
        TabContainer.SetTabTitle(ForceOnForceTabIndex, Loc.GetString("cmu-fof-tab"));
        ForceOnForceTabs.SetTabTitle(0, Loc.GetString("cmu-fof-roles"));
        foreach (var side in Enum.GetValues<ForceOnForceSide>())
            FoFSideButton.AddItem(Loc.GetString($"cmu-fof-side-{side.ToString().ToLowerInvariant()}"), (int) side);
        foreach (var fallback in Enum.GetValues<ForceOnForceFallback>())
            FoFFallbackButton.AddItem(Loc.GetString($"cmu-fof-fallback-{fallback.ToString().ToLowerInvariant()}"), (int) fallback);

        FoFSideButton.OnItemSelected += args =>
        {
            FoFSideButton.SelectId(args.Id);
            Profile = Profile?.WithForceOnForcePreferences((ForceOnForceSide) args.Id, Profile.FoFFallback);
            SetDirty();
        };
        FoFFallbackButton.OnItemSelected += args =>
        {
            FoFFallbackButton.SelectId(args.Id);
            Profile = Profile?.WithForceOnForcePreferences(Profile.FoFSide, (ForceOnForceFallback) args.Id);
            SetDirty();
        };
    }
}
