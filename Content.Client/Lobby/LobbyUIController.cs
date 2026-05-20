using System.Collections.Generic;
using System.Linq;
using Content.Client._RMC14.LinkAccount;
using Content.Client.Guidebook;
using Content.Client.Humanoid;
using Content.Client.Inventory;
using Content.Client.Lobby.UI;
using Content.Client.Players.PlayTimeTracking;
using Content.Shared._RMC14.Armor;
using Content.Shared.AU14.Allegiance;
using Content.Shared.AU14.Origin;
using Content.Shared.AU14.Threats;
using Content.Shared.CCVar;
using Content.Shared.Clothing;
using Content.Shared.GameTicking;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Lobby;
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
using Robust.Shared.Map;
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
    [Dependency] private LinkAccountManager _linkAccount = default!;
    [UISystemDependency] private HumanoidAppearanceSystem _humanoid = default!;
    [UISystemDependency] private ClientInventorySystem _inventory = default!;
    [UISystemDependency] private GuidebookSystem _guide = default!;
    [UISystemDependency] private CMArmorSystem _armorSystem = default!;

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

        _configurationManager.OnValueChanged(CCVars.GameRoleWhitelist, _ => RefreshProfileEditor());

        _linkAccount.Updated += RefreshProfileEditor;
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
            _profileEditor.RefreshJobs();
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

            if (obj.WasModified<AllegiancePrototype>())
            {
                _profileEditor.RefreshAllegiances();
            }

            if (obj.WasModified<OriginPrototype>())
            {
                _profileEditor.RefreshOrigins();
            }

            if (obj.WasModified<ThreatPrototype>())
            {
                _profileEditor.RefreshThreatPreferences();
                _profileEditor.RefreshJobs();
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
        _lobbyPreviewJobIndex = 0;
        _lobbyPreviewJobTimer = 0;
        _lobbyPreviewJobSignature = string.Empty;
        _lobbyPreviewJobs.Clear();
        _lobbyPreviewJobsProfile = null;
        _lobbyPreviewJobsDirty = true;
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
            (HumanoidCharacterProfile?) _preferencesManager.Preferences?.SelectedCharacter,
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
            PreviewPanel.SetSprite(EntityUid.Invalid);
            PreviewPanel.SetSummaryText(string.Empty);
            PreviewPanel.SetJobText(string.Empty);
            _lobbyPreviewJobIndex = 0;
            _lobbyPreviewJobTimer = 0;
            _lobbyPreviewJobSignature = string.Empty;
            _lobbyPreviewJobs.Clear();
            _lobbyPreviewJobsProfile = null;
            _lobbyPreviewJobsDirty = true;
            return;
        }

        var entry = GetCurrentLobbyPreviewJob(humanoid);
        var dummy = LoadProfileEntity(humanoid, entry?.Job, true);
        PreviewPanel.SetSprite(dummy);
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

        var entries = _lobbyPreviewJobs;
        if (entries.Count <= 1)
            return;

        _lobbyPreviewJobTimer += deltaSeconds;
        if (_lobbyPreviewJobTimer < HighJobPreviewScrollDelay)
            return;

        _lobbyPreviewJobTimer -= HighJobPreviewScrollDelay;
        _lobbyPreviewJobIndex = (_lobbyPreviewJobIndex + 1) % entries.Count;
        RefreshLobbyPreview();
    }

    private LobbyHighJobPreviewEntry? GetCurrentLobbyPreviewJob(HumanoidCharacterProfile profile)
    {
        RefreshLobbyPreviewJobs(profile);
        var entries = _lobbyPreviewJobs;

        if (entries.Count == 0)
            return null;

        _lobbyPreviewJobIndex %= entries.Count;
        return entries[_lobbyPreviewJobIndex];
    }

    private bool RefreshLobbyPreviewJobs(HumanoidCharacterProfile profile)
    {
        if (!_lobbyPreviewJobsDirty &&
            ReferenceEquals(_lobbyPreviewJobsProfile, profile))
        {
            return false;
        }

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

    private void RefreshProfileEditor()
    {
        _profileEditor?.RefreshAntags();
        _profileEditor?.RefreshJobs();
        _profileEditor?.RefreshLoadouts();
        _profileEditor?.RefreshRMC(_linkAccount.Tier);
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

    #region Helpers

    /// <summary>
    /// Applies the highest priority job's clothes to the dummy.
    /// </summary>
    public void GiveDummyJobClothesLoadout(EntityUid dummy, JobPrototype? jobProto, HumanoidCharacterProfile profile)
    {
        var job = jobProto ?? GetPreferredJob(profile);
        GiveDummyJobClothes(dummy, profile, job);

        if (_prototypeManager.HasIndex<RoleLoadoutPrototype>(LoadoutSystem.GetJobPrototype(job.ID)))
        {
            var loadout = profile.GetLoadoutOrDefault(LoadoutSystem.GetJobPrototype(job.ID), _playerManager.LocalSession, profile.Species, EntityManager, _prototypeManager);
            GiveDummyLoadout(dummy, loadout);
        }
    }

    /// <summary>
    /// Gets the highest priority job for the profile.
    /// </summary>
    public JobPrototype GetPreferredJob(HumanoidCharacterProfile profile)
    {
        var highPriorityJobs = LobbyHighJobPreview.GetHighPriorityJobs(profile, _prototypeManager);
        if (highPriorityJobs.Count > 0)
            return highPriorityJobs[0].Job;

        var highPriorityJob = profile.JobPriorities.FirstOrDefault(p => p.Value == JobPriority.High).Key;
        // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract (what is resharper smoking?)
        return _prototypeManager.Index<JobPrototype>(highPriorityJob.Id ?? SharedGameTicker.FallbackOverflowJob);
    }

    public void GiveDummyLoadout(EntityUid uid, RoleLoadout? roleLoadout)
    {
        if (roleLoadout == null)
            return;

        foreach (var group in roleLoadout.SelectedLoadouts.Values)
        {
            foreach (var loadout in group)
            {
                if (!_prototypeManager.TryIndex(loadout.Prototype, out var loadoutProto))
                    continue;

                if (_prototypeManager.TryIndex(loadoutProto.StartingGear, out var startingGear))
                    GiveDummyEquipmentLoadout(uid, startingGear);

                GiveDummyEquipmentLoadout(uid, loadoutProto);
            }
        }
    }

    private void GiveDummyEquipmentLoadout(EntityUid uid, IEquipmentLoadout? loadout)
    {
        if (loadout == null ||
            !_inventory.TryGetSlots(uid, out var slots))
        {
            return;
        }

        // Preview dummies only need visible equipment. In-hand and storage loadouts can fire
        // real pickup/fill behavior, including sounds, every time the preview refreshes.
        foreach (var slot in slots)
        {
            var itemType = loadout.GetGear(slot.Name);
            if (string.IsNullOrEmpty(itemType))
                continue;

            if (_inventory.TryUnequip(uid, slot.Name, out var unequippedItem, silent: true, force: true, reparent: false))
            {
                EntityManager.DeleteEntity(unequippedItem.Value);
            }

            var item = EntityManager.SpawnEntity(itemType, MapCoordinates.Nullspace);
            MarkPreviewEntity(item);
            _inventory.TryEquip(uid, item, slot.Name, true, true);
        }
    }

    /// <summary>
    /// Applies the specified job's clothes to the dummy.
    /// </summary>
    public void GiveDummyJobClothes(EntityUid dummy, HumanoidCharacterProfile profile, JobPrototype job)
    {
        if (!_inventory.TryGetSlots(dummy, out var slots))
            return;

        // Apply loadout
        if (profile.Loadouts.TryGetValue(job.ID, out var jobLoadout))
        {
            foreach (var loadouts in jobLoadout.SelectedLoadouts.Values)
            {
                foreach (var loadout in loadouts)
                {
                    if (!_prototypeManager.TryIndex(loadout.Prototype, out var loadoutProto))
                        continue;

                    // TODO: Need some way to apply starting gear to an entity and replace existing stuff coz holy fucking shit dude.
                    foreach (var slot in slots)
                    {
                        // Try startinggear first
                        if (_prototypeManager.TryIndex(loadoutProto.StartingGear, out var loadoutGear))
                        {
                            var itemType = ((IEquipmentLoadout) loadoutGear).GetGear(slot.Name);

                            if (_inventory.TryUnequip(dummy, slot.Name, out var unequippedItem, silent: true, force: true, reparent: false))
                            {
                                EntityManager.DeleteEntity(unequippedItem.Value);
                            }

                            if (itemType != string.Empty)
                            {
                                var item = EntityManager.SpawnEntity(itemType, MapCoordinates.Nullspace);
                                MarkPreviewEntity(item);
                                _inventory.TryEquip(dummy, item, slot.Name, true, true);
                            }
                        }
                        else
                        {
                            var itemType = ((IEquipmentLoadout) loadoutProto).GetGear(slot.Name);

                            if (_inventory.TryUnequip(dummy, slot.Name, out var unequippedItem, silent: true, force: true, reparent: false))
                            {
                                EntityManager.DeleteEntity(unequippedItem.Value);
                            }

                            if (itemType != string.Empty)
                            {
                                var item = EntityManager.SpawnEntity(itemType, MapCoordinates.Nullspace);
                                MarkPreviewEntity(item);
                                _inventory.TryEquip(dummy, item, slot.Name, true, true);
                            }
                        }
                    }
                }
            }
        }

        if (!_prototypeManager.TryIndex(job.StartingGear, out var gear))
            return;

        _prototypeManager.TryIndex(job.DummyStartingGear, out var dummyGear);

        foreach (var slot in slots)
        {
            var itemType = ((IEquipmentLoadout) gear).GetGear(slot.Name);

            if (itemType == string.Empty && dummyGear != null)
                itemType = ((IEquipmentLoadout) dummyGear).GetGear(slot.Name);

            if (_inventory.TryUnequip(dummy, slot.Name, out var unequippedItem, silent: true, force: true, reparent: false))
            {
                EntityManager.DeleteEntity(unequippedItem.Value);
            }

            if (itemType != string.Empty)
            {
                var item = EntityManager.SpawnEntity(itemType, MapCoordinates.Nullspace);

                if (EntityManager.TryGetComponent<RMCArmorVariantComponent>(item, out var variantComponent))
                {
                    var variantItemProtoId = _armorSystem.GetArmorVariant((item, variantComponent), profile.ArmorPreference);
                    var variantItem = EntityManager.SpawnEntity(variantItemProtoId, MapCoordinates.Nullspace);
                    MarkPreviewEntity(variantItem);
                    _inventory.TryEquip(dummy, variantItem, slot.Name, true, true);
                    EntityManager.QueueDeleteEntity(item);

                    continue;
                }

                MarkPreviewEntity(item);
                _inventory.TryEquip(dummy, item, slot.Name, true, true);
            }
        }
    }

    /// <summary>
    /// Loads the profile onto a dummy entity.
    /// </summary>
    public EntityUid LoadProfileEntity(HumanoidCharacterProfile? humanoid, JobPrototype? job, bool jobClothes)
    {
        EntityUid dummyEnt;

        EntProtoId? previewEntity = null;
        if (humanoid != null && jobClothes)
        {
            job ??= GetPreferredJob(humanoid);

            previewEntity = job.JobPreviewEntity ?? (EntProtoId?)job?.JobEntity;
        }

        if (previewEntity != null)
        {
            // Special type like borg or AI, do not spawn a human just spawn the entity.
            dummyEnt = EntityManager.SpawnEntity(previewEntity, MapCoordinates.Nullspace);
            MarkPreviewEntity(dummyEnt);
            return dummyEnt;
        }
        else if (humanoid is not null)
        {
            var dummy = _prototypeManager.Index<SpeciesPrototype>(humanoid.Species).DollPrototype;
            dummyEnt = EntityManager.SpawnEntity(dummy, MapCoordinates.Nullspace);
            MarkPreviewEntity(dummyEnt);
        }
        else
        {
            dummyEnt = EntityManager.SpawnEntity(_prototypeManager.Index<SpeciesPrototype>(SharedHumanoidAppearanceSystem.DefaultSpecies).DollPrototype, MapCoordinates.Nullspace);
            MarkPreviewEntity(dummyEnt);
        }

        _humanoid.LoadProfile(dummyEnt, humanoid);

        if (humanoid != null && jobClothes)
        {
            DebugTools.Assert(job != null);

            GiveDummyJobClothes(dummyEnt, humanoid, job);

            if (_prototypeManager.HasIndex<RoleLoadoutPrototype>(LoadoutSystem.GetJobPrototype(job.ID)))
            {
                var loadout = humanoid.GetLoadoutOrDefault(LoadoutSystem.GetJobPrototype(job.ID), _playerManager.LocalSession, humanoid.Species, EntityManager, _prototypeManager);
                GiveDummyLoadout(dummyEnt, loadout);
            }
        }

        return dummyEnt;
    }

    private void MarkPreviewEntity(EntityUid uid)
    {
        EntityManager.EnsureComponent<LobbyPreviewEntityComponent>(uid);
    }

    #endregion
}
