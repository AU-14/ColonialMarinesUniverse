
using Content.Shared.Preferences;
// cmu edit start
using System.Linq;
using System.Text.RegularExpressions;
using Content.Shared.CMU14.Preferences;
// cmu edit end

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    // cmu edit start
    private static readonly Regex CMUSingleNamePart = new(@"^[A-Za-z0-9\-\.']*$");
    private static readonly Regex CMUMultiNamePart = new(@"^[A-Za-z0-9 \-\.']*$");
    private static readonly Regex CMUNicknamePart = new(@"^[A-Za-z0-9 \-\.]*$");

    private bool _cmuUpdatingNameEdits;

    private void CMUSetupNameEdits()
    {
        FirstNameEdit.IsValid = text => text.Length <= _maxNameLength && CMUSingleNamePart.IsMatch(text);
        LastNameEdit.IsValid = text => text.Length <= _maxNameLength && CMUMultiNamePart.IsMatch(text);
        NicknameEdit.IsValid = text => text.Length <= _maxNameLength &&
                                       CMUNicknamePart.IsMatch(text) &&
                                       text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= CMUCharacterName.MaxNicknameWords &&
                                       text.Count(c => c == ' ') < CMUCharacterName.MaxNicknameWords;

        FirstNameEdit.OnTextChanged += _ => CMURecomposeName();
        LastNameEdit.OnTextChanged += _ => CMURecomposeName();
        NicknameEdit.OnTextChanged += _ => CMURecomposeName();
    }

    private void CMURecomposeName()
    {
        if (_cmuUpdatingNameEdits || Profile == null)
            return;

        if (Profile.Synthetic)
        {
            SetName(FirstNameEdit.Text.Trim());
            return;
        }

        var artificialWomb = CMUCharacterName.IsArtificialWomb(Profile);
        SetName(CMUCharacterName.Compose(FirstNameEdit.Text, LastNameEdit.Text, NicknameEdit.Text, artificialWomb));
    }

    private void CMUUpdateSyntheticNameFields()
    {
        var synthetic = Profile?.Synthetic == true;
        LastNameEdit.Editable = !synthetic;
        NicknameEdit.Editable = !synthetic;
        LastNameEdit.PlaceHolder = synthetic ? Loc.GetString("cmu-profile-editor-synthetic-single-name") : string.Empty;
        NicknameEdit.PlaceHolder = synthetic ? Loc.GetString("cmu-profile-editor-synthetic-single-name") : string.Empty;

        if (!synthetic || LastNameEdit.Text.Length == 0 && NicknameEdit.Text.Length == 0)
            return;

        _cmuUpdatingNameEdits = true;
        LastNameEdit.Text = string.Empty;
        NicknameEdit.Text = string.Empty;
        _cmuUpdatingNameEdits = false;
        CMURecomposeName();
    }

    private bool CMUHasRequiredName()
    {
        if (Profile == null)
            return true;

        var valid = CMUCharacterName.HasRequiredParts(Profile.Name, Profile.Synthetic);
        NameRequiredLabel.Visible = !valid;
        NameRequiredLabel.Text = Loc.GetString(Profile.Synthetic
            ? "cmu-profile-editor-single-name-required"
            : "cmu-profile-editor-full-name-required");
        return valid;
    }
    // cmu edit end

    private void SetName(string newName)
    {
        Profile = Profile?.WithName(newName);
        SetDirty();
        // cmu edit start
        UpdateSaveButton();
        // cmu edit end

        if (!IsDirty)
            return;

        SpriteView.SetName(newName);
    }

    private void UpdateNameEdit()
    {
        // cmu edit start
        var parts = CMUCharacterName.Parse(Profile?.Name ?? "");
        _cmuUpdatingNameEdits = true;
        FirstNameEdit.Text = parts.First;
        LastNameEdit.Text = parts.Last;
        NicknameEdit.Text = parts.Nickname;
        _cmuUpdatingNameEdits = false;
        CMUUpdateSyntheticNameFields();
        UpdateSaveButton();
        // cmu edit end
    }

    /// <summary>
    /// Randomize values selectively while respecting locked values.
    /// </summary>
    private void RandomizeProfile()
    {
        Profile = Profile == null
            ? HumanoidCharacterProfile.Random()
            : HumanoidCharacterProfile.Random(RandomizeLockButton.RandomizeCfg, Profile!);
        SetProfile(Profile, CharacterSlot);
        SetDirty();
    }
}
