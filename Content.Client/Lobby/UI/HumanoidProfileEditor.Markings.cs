namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private void UpdateMarkings()
    {
        if (Profile == null)
        {
            return;
        }

        _markingsModel.OrganData = _markingManager.GetMarkingData(Profile.Species);
        _markingsModel.OrganProfileData = _markingManager.GetProfileData(Profile.Species, Profile.Sex, Profile.Appearance.SkinColor, Profile.Appearance.EyeColor);
        _markingsModel.Markings = Profile.Appearance.Markings;
    }

    private void OnMarkingChange()
    {
        if (Profile is null)
            return;

        var appearance = Profile.Appearance.WithMarkings(_markingsModel.Markings); // CMU14: Build may override an edited torso marking.
        Profile = Profile.WithCharacterAppearance(appearance); // CMU14
        if (!ReferenceEquals(Profile.Appearance, appearance))
            _markingsModel.Markings = Profile.Appearance.Markings; // CMU14: refresh only when the Build overlay replaced the edit.
        ReloadProfilePreview();
        SetDirty();
    }
}
