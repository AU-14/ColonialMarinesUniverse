using System.Collections.Generic;
using Content.Client.Guidebook;
using Content.Client.Lobby.UI;
using Content.Client.Players.PlayTimeTracking;
using Content.Shared.CCVar;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Roles;
using Content.Shared.Traits;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.Lobby;

public sealed partial class LobbyUIController : UIController, IOnStateEntered<LobbyState>, IOnStateExited<LobbyState>
{
    private const float HighJobPreviewScrollDelay = 2.75f;

    [Dependency] private IClientPreferencesManager _preferencesManager = default!;
    [Dependency] private IConfigurationManager _configurationManager = default!;
    [Dependency] private IFileDialogManager _dialogManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IResourceCache _resourceCache = default!;
    [Dependency] private IStateManager _stateManager = default!;
    [Dependency] private JobRequirementsManager _requirements = default!;
    [Dependency] private MarkingManager _markings = default!;
    [UISystemDependency] private readonly GuidebookSystem _guide = default!;

    private CharacterSetupGui? _characterSetup;
    private HumanoidProfileEditor? _profileEditor;
    private CharacterSetupGuiSavePanel? _savePanel;
    private int _lobbyPreviewJobIndex;
    private float _lobbyPreviewJobTimer;
    private string _lobbyPreviewJobSignature = string.Empty;
    private readonly List<LobbyHighJobPreviewEntry> _lobbyPreviewJobs = new();
    private HumanoidCharacterProfile? _lobbyPreviewJobsProfile;
    private bool _lobbyPreviewJobsDirty = true;

    /// <summary>
    /// This is the characher preview panel in the chat. This should only update if their character updates.
    /// </summary>
    private LobbyCharacterPreviewPanel? PreviewPanel => GetLobbyPreview();

    /// <summary>
    /// This is the modified profile currently being edited.
    /// </summary>
    private HumanoidCharacterProfile? EditedProfile => _profileEditor?.Profile;

    private int? EditedSlot => _profileEditor?.CharacterSlot;

    public override void Initialize()
    {
        base.Initialize();
        _prototypeManager.PrototypesReloaded += OnProtoReload;
        _preferencesManager.OnServerDataLoaded += PreferencesDataLoaded;
        _requirements.Updated += OnRequirementsUpdated;

        _configurationManager.OnValueChanged(CCVars.FlavorText, args =>
        {
            _profileEditor?.RefreshFlavorText();
        });

        _configurationManager.OnValueChanged(CCVars.GameRoleTimers, _ => RefreshProfileEditor());
        _configurationManager.OnValueChanged(CCVars.GameRoleLoadoutTimers, _ => RefreshProfileEditor());

        _configurationManager.OnValueChanged(CCVars.GameRoleWhitelist, _ => RefreshProfileEditor());
    }

    private LobbyCharacterPreviewPanel? GetLobbyPreview()
    {
        if (_stateManager.CurrentState is LobbyState lobby)
        {
            return lobby.Lobby?.CharacterPreview;
        }

        return null;
    }

    private void OnRequirementsUpdated()
    {
        if (_profileEditor != null)
        {
            _profileEditor.RefreshAntags();
            _profileEditor.RefreshSynthetic();
            _profileEditor.RefreshJobs();
            _profileEditor.RefreshThreatPreferences();
        }
    }

    private void OnProtoReload(PrototypesReloadedEventArgs obj)
    {
        if (_profileEditor != null)
        {
            if (obj.WasModified<AntagPrototype>())
            {
                _profileEditor.RefreshAntags();
            }

            if (obj.WasModified<JobPrototype>() ||
                obj.WasModified<DepartmentPrototype>())
            {
                _profileEditor.RefreshJobs();
            }

            if (obj.WasModified<Content.Shared._CMU14.Threats.ThreatPrototype>())
                _profileEditor.RefreshThreatPreferences();

            if (obj.WasModified<LoadoutPrototype>() ||
                obj.WasModified<LoadoutGroupPrototype>() ||
                obj.WasModified<RoleLoadoutPrototype>())
            {
                _profileEditor.RefreshLoadouts();
            }

            if (obj.WasModified<SpeciesPrototype>())
            {
                _profileEditor.RefreshSpecies();
            }

            if (obj.WasModified<TraitPrototype>())
            {
                _profileEditor.RefreshTraits();
            }
        }
    }

    private void PreferencesDataLoaded()
    {
        PreviewPanel?.SetLoaded(true);

        if (_stateManager.CurrentState is not LobbyState)
            return;

        ReloadCharacterSetup();
    }

    public void OnStateEntered(LobbyState state)
    {
        PreviewPanel?.SetLoaded(_preferencesManager.ServerDataLoaded);
        ReloadCharacterSetup();
    }

    public void OnStateExited(LobbyState state)
    {
        PreviewPanel?.SetLoaded(false);
        _profileEditor?.Dispose();
        _characterSetup?.Dispose();

        _characterSetup = null;
        _profileEditor = null;
        ResetLobbyPreviewJobs();
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        UpdateLobbyPreviewJobRotation(args.DeltaSeconds);
    }

    /// <summary>
    /// Reloads every single character setup control.
    /// </summary>
    public void ReloadCharacterSetup()
    {
        RefreshLobbyPreview();
        var (characterGui, profileEditor) = EnsureGui();
        characterGui.ReloadCharacterPickers();
        profileEditor.SetProfile(
            _preferencesManager.Preferences?.SelectedCharacter,
            _preferencesManager.Preferences?.SelectedCharacterIndex);
    }

    /// <summary>
    /// Refreshes the character preview in the lobby chat.
    /// </summary>
    private void RefreshLobbyPreview()
    {
        if (PreviewPanel == null)
            return;

        // Get selected character, load it, then set it
        var character = _preferencesManager.Preferences?.SelectedCharacter;

        if (character is not HumanoidCharacterProfile humanoid)
        {
            PreviewPanel.ProfilePreviewSpriteView.ClearPreview();
            PreviewPanel.SetSummaryText(string.Empty);
            PreviewPanel.SetJobText(string.Empty);
            ResetLobbyPreviewJobs();
            return;
        }

        var entry = GetCurrentLobbyPreviewJob(humanoid);
        PreviewPanel.ProfilePreviewSpriteView.LoadPreview(humanoid, entry?.Job);
        PreviewPanel.SetSummaryText(humanoid.Summary);
        PreviewPanel.SetJobText(entry?.DisplayName ?? string.Empty);
    }

    private void UpdateLobbyPreviewJobRotation(float deltaSeconds)
    {
        if (PreviewPanel == null ||
            _stateManager.CurrentState is not LobbyState ||
            _preferencesManager.Preferences?.SelectedCharacter is not HumanoidCharacterProfile humanoid)
        {
            return;
        }

        if (RefreshLobbyPreviewJobs(humanoid))
        {
            RefreshLobbyPreview();
            return;
        }

        if (_lobbyPreviewJobs.Count <= 1)
            return;

        _lobbyPreviewJobTimer += deltaSeconds;
        if (_lobbyPreviewJobTimer < HighJobPreviewScrollDelay)
            return;

        _lobbyPreviewJobTimer -= HighJobPreviewScrollDelay;
        _lobbyPreviewJobIndex = (_lobbyPreviewJobIndex + 1) % _lobbyPreviewJobs.Count;
        RefreshLobbyPreview();
    }

    private LobbyHighJobPreviewEntry? GetCurrentLobbyPreviewJob(HumanoidCharacterProfile profile)
    {
        RefreshLobbyPreviewJobs(profile);
        if (_lobbyPreviewJobs.Count == 0)
            return null;

        _lobbyPreviewJobIndex %= _lobbyPreviewJobs.Count;
        return _lobbyPreviewJobs[_lobbyPreviewJobIndex];
    }

    private bool RefreshLobbyPreviewJobs(HumanoidCharacterProfile profile)
    {
        if (!_lobbyPreviewJobsDirty && ReferenceEquals(_lobbyPreviewJobsProfile, profile))
            return false;

        var previousSignature = _lobbyPreviewJobSignature;
        _lobbyPreviewJobs.Clear();
        _lobbyPreviewJobs.AddRange(LobbyHighJobPreview.GetHighPriorityJobs(profile, _prototypeManager));
        _lobbyPreviewJobsProfile = profile;
        _lobbyPreviewJobsDirty = false;
        _lobbyPreviewJobSignature = LobbyHighJobPreview.GetSignature(_lobbyPreviewJobs);

        var changed = previousSignature != _lobbyPreviewJobSignature;
        if (changed)
        {
            _lobbyPreviewJobIndex = 0;
            _lobbyPreviewJobTimer = 0;
        }

        return changed;
    }

    private void ResetLobbyPreviewJobs()
    {
        _lobbyPreviewJobIndex = 0;
        _lobbyPreviewJobTimer = 0;
        _lobbyPreviewJobSignature = string.Empty;
        _lobbyPreviewJobs.Clear();
        _lobbyPreviewJobsProfile = null;
        _lobbyPreviewJobsDirty = true;
    }

    private void RefreshProfileEditor()
    {
        _profileEditor?.RefreshAntags();
        _profileEditor?.RefreshSynthetic();
        _profileEditor?.RefreshJobs();
        _profileEditor?.RefreshThreatPreferences();
        _profileEditor?.RefreshLoadouts();
    }

    private void SaveProfile()
    {
        DebugTools.Assert(EditedProfile != null);

        if (EditedProfile == null || EditedSlot == null)
            return;

        var selected = _preferencesManager.Preferences?.SelectedCharacterIndex;

        if (selected == null)
            return;

        _preferencesManager.UpdateCharacter(EditedProfile, EditedSlot.Value);
        ReloadCharacterSetup();
    }

    private void CloseProfileEditor()
    {
        if (_profileEditor == null)
            return;

        _profileEditor.SetProfile(null, null);
        _profileEditor.Visible = false;

        if (_stateManager.CurrentState is LobbyState lobbyGui)
        {
            lobbyGui.SwitchState(LobbyGui.LobbyGuiState.Default);
        }
    }

    private void OpenSavePanel()
    {
        if (_savePanel is { IsOpen: true })
            return;

        _savePanel = new CharacterSetupGuiSavePanel();

        _savePanel.SaveButton.OnPressed += _ =>
        {
            SaveProfile();

            _savePanel.Close();

            CloseProfileEditor();
        };

        _savePanel.NoSaveButton.OnPressed += _ =>
        {
            _savePanel.Close();

            CloseProfileEditor();
        };

        _savePanel.OpenCentered();
    }

    private (CharacterSetupGui, HumanoidProfileEditor) EnsureGui()
    {
        if (_characterSetup != null && _profileEditor != null)
        {
            _characterSetup.Visible = true;
            _profileEditor.Visible = true;
            return (_characterSetup, _profileEditor);
        }

        _profileEditor = new HumanoidProfileEditor(
            _preferencesManager,
            _configurationManager,
            EntityManager,
            _dialogManager,
            LogManager,
            _playerManager,
            _prototypeManager,
            _resourceCache,
            _requirements,
            _markings);

        _profileEditor.OnOpenGuidebook += _guide.OpenHelp;

        _characterSetup = new CharacterSetupGui(_profileEditor);

        _characterSetup.CloseButton.OnPressed += _ =>
        {
            // Open the save panel if we have unsaved changes.
            if (_profileEditor.Profile != null && _profileEditor.IsDirty)
            {
                OpenSavePanel();

                return;
            }

            // Reset sliders etc.
            CloseProfileEditor();
        };

        _profileEditor.Save += SaveProfile;

        _characterSetup.SelectCharacter += args =>
        {
            _preferencesManager.SelectCharacter(args);
            ReloadCharacterSetup();
        };

        _characterSetup.DeleteCharacter += args =>
        {
            _preferencesManager.DeleteCharacter(args);

            // Reload everything
            if (EditedSlot == args)
            {
                ReloadCharacterSetup();
            }
            else
            {
                // Only need to reload character pickers
                _characterSetup?.ReloadCharacterPickers();
            }
        };

        if (_stateManager.CurrentState is LobbyState lobby)
        {
            lobby.Lobby?.CharacterSetupState.AddChild(_characterSetup);
        }

        return (_characterSetup, _profileEditor);
    }
}
