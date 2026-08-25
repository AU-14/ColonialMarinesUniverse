using System.Linq;
using System.Numerics;
using Content.Client.Lobby.UI.Loadouts;
using Content.Client.Lobby.UI.Roles;
using Content.Client.Stylesheets;
using Content.Client._CMU14.Roles.Ranks; // CMU14
using Content.Shared._CMU14.Roles.Ranks; // CMU14
using Content.Shared._RMC14.Marines.Roles.Ranks; // CMU14
using Content.Shared.AU14.util; // CMU14
using Content.Shared._AU14.Marines.Roles.Chevrons; // CMU14
using Content.Shared._CMU14.Threats;
using Content.Shared._RMC14.Prototypes;
using Content.Shared.Clothing;
using Content.Shared.CCVar;
using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Roles;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration; // CMU14
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private const string GamemodeInsurgency = "Insurgency";
    private const string GamemodeColonyFall = "ColonyFall";
    private const string GamemodeDistressSignal = "DistressSignal";
    private const string InsurgencyDepartmentId = "AU14DepartmentColonialLiberationFront";

    public JobPrototype? JobOverride;

    private LoadoutWindow? _loadoutWindow;
    [ViewVariables] private PlatoonRankPreferenceWindow? _rankPreferenceWindow; // CMU14
    private readonly List<(string Gamemode, string JobId, RequirementsSelector Selector)> _jobPriorities = [];
    private readonly List<(string Gamemode, string AntagId, RequirementsSelector Selector)> _antagPreferences = [];
    private readonly List<(string Gamemode, string ThreatId, Button Yes, Button No)> _threatPreferenceButtons = [];
    private readonly Dictionary<string, BoxContainer> _jobCategories;

    private void UpdateJobPriorities()
    {
        foreach (var (gamemode, jobId, selector) in _jobPriorities)
        {
            var priority = Profile?.GetJobPriorityForGamemode(gamemode, jobId) ?? JobPriority.Never;
            selector.Select((int) priority);
        }
    }

    public void RefreshLoadouts()
    {
        _loadoutWindow?.Dispose();
        _loadoutWindow = null;
    }

    private void OpenLoadout(JobPrototype job, RoleLoadout roleLoadout, RoleLoadoutPrototype roleLoadoutProto)
    {
        _loadoutWindow?.Dispose();
        var collection = IoCManager.Instance;
        if (collection == null || _playerManager.LocalSession == null || Profile == null)
            return;

        JobOverride = job;
        var session = _playerManager.LocalSession;
        _loadoutWindow = new LoadoutWindow(Profile, roleLoadout, roleLoadoutProto, session, collection)
        {
            Title = Loc.GetString("loadout-window-title-loadout", ("job", job.LocalizedName)),
        };
        _loadoutWindow.RefreshLoadouts(roleLoadout, session, collection);
        _loadoutWindow.OpenCenteredLeft();
        _loadoutWindow.OnNameChanged += name =>
        {
            roleLoadout.EntityName = name;
            Profile = Profile.WithLoadout(roleLoadout);
            SetDirty();
        };
        _loadoutWindow.OnLoadoutPressed += (group, loadout) =>
        {
            roleLoadout.AddLoadout(group, loadout, _prototypeManager);
            _loadoutWindow.RefreshLoadouts(roleLoadout, session, collection);
            Profile = Profile?.WithLoadout(roleLoadout);
            ReloadPreview();
        };
        _loadoutWindow.OnLoadoutUnpressed += (group, loadout) =>
        {
            roleLoadout.RemoveLoadout(group, loadout, _prototypeManager);
            _loadoutWindow.RefreshLoadouts(roleLoadout, session, collection);
            Profile = Profile?.WithLoadout(roleLoadout);
            ReloadPreview();
        };
        _loadoutWindow.OnClose += () =>
        {
            JobOverride = null;
            ReloadPreview();
        };
        ReloadPreview();
    }

    public void RefreshJobs()
    {
        foreach (var list in GetGamemodeJobLists())
            list.RemoveAllChildren();
        _jobCategories.Clear();
        _jobPriorities.Clear();

        var items = new[]
        {
            ("humanoid-profile-editor-job-priority-never-button", (int) JobPriority.Never),
            ("humanoid-profile-editor-job-priority-low-button", (int) JobPriority.Low),
            ("humanoid-profile-editor-job-priority-medium-button", (int) JobPriority.Medium),
            ("humanoid-profile-editor-job-priority-high-button", (int) JobPriority.High),
        };

        var departments = _prototypeManager.EnumerateCM<DepartmentPrototype>()
            .Where(department => !department.EditorHidden)
            .OrderBy(department => department, DepartmentUIComparer.Instance)
            .ToArray();

        foreach (var department in departments)
        {
            var departmentName = Loc.GetString(department.Name);
            var jobs = department.Roles
                .Select(id => _prototypeManager.Index(id))
                .Where(job => job.SetPreference && !job.Hidden)
                .OrderBy(job => GetJobSortGroup(department, job));

            if (JobUIComparer.TryCreate(_prototypeManager, null, out var comparer))
                jobs = jobs.ThenBy(job => job, comparer);

            foreach (var job in jobs)
            {
                foreach (var section in GetJobSections(department, job, departmentName))
                    AddJobSelector(section.Target, section.Gamemode, section.Key, section.Title,
                        department.IsCM && !department.Hidden, departmentName, items, job);
            }
        }

        foreach (var list in GetGamemodeJobLists())
            CrtLobbyTheme.Apply(list);
        UpdateJobPriorities();
    }

    private void AddJobSelector(
        BoxContainer target,
        string gamemode,
        string sectionKey,
        string sectionTitle,
        bool visible,
        string departmentName,
        (string, int)[] items,
        JobPrototype job)
    {
        var category = GetOrCreateJobCategory(sectionKey, target, sectionTitle, visible, departmentName);
        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
        };
        var selector = new RequirementsSelector
        {
            Margin = new Thickness(3, 3, 3, 0),
            HorizontalExpand = true,
        };
        selector.OnOpenGuidebook += OnOpenGuidebook;
        var icon = new TextureRect
        {
            TextureScale = new Vector2(2, 2),
            VerticalAlignment = VAlignment.Center,
            Texture = _sprite.Frame0(_prototypeManager.Index(job.Icon).Icon),
        };
        selector.Setup(items, LobbyHighJobPreview.GetDisplayJobName(job), 220, job.LocalizedDescription, icon, job.Guides);

        if (!_requirements.IsAllowed(job,
                (HumanoidCharacterProfile?) _preferencesManager.Preferences?.SelectedCharacter,
                out var reason))
        {
            selector.LockRequirements(reason);
        }
        else if (Profile != null && job.IsSynthetic != Profile.Synthetic)
        {
            selector.LockRequirements(FormattedMessage.FromUnformatted(Loc.GetString(job.IsSynthetic
                ? "humanoid-profile-editor-synthetic-locked-job"
                : "humanoid-profile-editor-synthetic-locked-job-non-synthetic")));
        }
        else
        {
            selector.UnlockRequirements();
        }

        selector.OnSelected += selected =>
        {
            var priority = (JobPriority) selected;
            Profile = Profile?.WithGamemodeJobPriority(gamemode, job.ID, priority);
            foreach (var (otherGamemode, otherJob, otherSelector) in _jobPriorities)
            {
                if (otherGamemode != gamemode)
                    continue;
                if (otherJob == job.ID)
                {
                    otherSelector.Select(selected);
                    continue;
                }
                if (priority == JobPriority.High && (JobPriority) otherSelector.Selected == JobPriority.High)
                {
                    otherSelector.Select((int) JobPriority.Medium);
                    Profile = Profile?.WithGamemodeJobPriority(gamemode, otherJob, JobPriority.Medium);
                }
            }
            ReloadPreview();
            UpdateJobPriorities();
            SetDirty();
        };

        var loadoutButton = new Button
        {
            Text = Loc.GetString("loadout-window"),
            HorizontalAlignment = HAlignment.Right,
            VerticalAlignment = VAlignment.Center,
            Margin = new Thickness(3, 3, 0, 0),
            MinWidth = 110,
        };
        var jobLoadout = LoadoutSystem.GetJobPrototype(job);
        if (!_prototypeManager.TryIndex<RoleLoadoutPrototype>(jobLoadout, out var loadoutProto))
        {
            loadoutButton.Disabled = true;
        }
        else
        {
            loadoutButton.OnPressed += _ =>
            {
                RoleLoadout? existing = null;
                Profile?.Loadouts.TryGetValue(jobLoadout, out existing);
                var loadout = existing?.Clone() ?? new RoleLoadout(loadoutProto.ID);
                if (existing == null)
                    loadout.SetDefault(Profile, _playerManager.LocalSession, _prototypeManager);
                OpenLoadout(job, loadout, loadoutProto);
            };
        }

        _jobPriorities.Add((gamemode, job.ID, selector));
        var rankEntry = BuildRankPreferenceJobEntry(job); // CMU14: platoon rank prefs window (upstream 438a10891c)

        var rankButton = new Button
        {
            Text = Loc.GetString("cmu14-rank-preference-button"),
            HorizontalAlignment = HAlignment.Right,
            VerticalAlignment = VAlignment.Center,
            Margin = new Thickness(3, 3, 0, 0),
            MinWidth = 90,
            StyleClasses = { StyleNano.StyleClassCrtButton },
            Disabled = rankEntry == null,
        };

        rankButton.OnPressed += _ => OpenRankPreferenceWindowForJob(job, rankEntry);

        row.AddChild(selector);
        row.AddChild(loadoutButton);
        row.AddChild(rankButton);
        category.AddChild(row);
    }

    private BoxContainer GetOrCreateJobCategory(
        string key,
        BoxContainer target,
        string title,
        bool visible,
        string departmentName)
    {
        if (_jobCategories.TryGetValue(key, out var existing))
            return existing;

        var category = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Name = key,
            Visible = visible,
            ToolTip = Loc.GetString("humanoid-profile-editor-jobs-amount-in-department-tooltip",
                ("departmentName", departmentName)),
        };
        if (target.Children.Any(child => child.Visible) && visible)
            category.AddChild(new Control { MinSize = new Vector2(0, 14) });
        category.AddChild(new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex("#464966") },
            Children = { new Label { Text = title, Margin = new Thickness(6, 0, 0, 0) } },
        });
        _jobCategories[key] = category;
        target.AddChild(category);
        return category;
    }

    private IEnumerable<(BoxContainer Target, string Gamemode, string Key, string Title)> GetJobSections(
        DepartmentPrototype department,
        JobPrototype job,
        string departmentName)
    {
        if (department.Faction == "govfor")
        {
            var (segment, title) = GetMilitaryJobSegment(job);
            yield return (InsurgencyGovernmentJobList, GamemodeInsurgency, $"insurgency-govfor-{segment}", title);
            yield return (DistressGovernmentJobList, GamemodeDistressSignal, $"distress-govfor-{segment}", title);
            yield break;
        }
        if (department.Faction == "opfor")
            yield break;
        if (department.ID == InsurgencyDepartmentId)
        {
            yield return (InsurgencyInsurgentJobList, GamemodeInsurgency,
                $"insurgency-{department.ID}", department.CustomName ?? departmentName);
            yield break;
        }
        if (department.Faction == "colonist")
        {
            var title = department.CustomName ?? departmentName;
            yield return (InsurgencyCivilianJobList, GamemodeInsurgency, $"insurgency-civilian-{department.ID}", title);
            yield return (ColonyCivilianJobList, GamemodeColonyFall, $"colony-civilian-{department.ID}", title);
            yield break;
        }
        if (job.ID is not ("AU14JobThreatLeader" or "AU14JobThreatMember" or
            "AU14JobThirdPartyLeader" or "AU14JobThirdPartyMember"))
            yield break;
        yield return (ColonyThreatJobList, GamemodeColonyFall, "colony-threat", "Threat Jobs");
        yield return (DistressThreatJobList, GamemodeDistressSignal, "distress-threat", "Threat Jobs");
    }

    private IEnumerable<BoxContainer> GetGamemodeJobLists()
    {
        yield return InsurgencyGovernmentJobList;
        yield return InsurgencyInsurgentJobList;
        yield return InsurgencyCivilianJobList;
        yield return ColonyCivilianJobList;
        yield return ColonyThreatJobList;
        yield return DistressGovernmentJobList;
        yield return DistressThreatJobList;
    }

    private static (string Key, string Title) GetMilitaryJobSegment(JobPrototype job)
    {
        var id = job.ID;
        var name = job.LocalizedName;
        if (id == "AU14JobGOVFORVehicleCommander" || ContainsAny(id, name, "Pilot", "Dropship", "Crew Chief", "DCC", "VehicleCrewman"))
            return ("flight", Loc.GetString("humanoid-profile-editor-segment-flight"));
        if (job.MarineAuthorityLevel > 0 || ContainsAny(id, name, "PlatCo", "Adjutant", "PlatOp", "Commander", "Command", "Advisor"))
            return ("command", Loc.GetString("humanoid-profile-editor-segment-command"));
        if (ContainsAny(id, name, "Officer", "Chief"))
            return ("officer", Loc.GetString("humanoid-profile-editor-segment-officer"));
        if (ContainsAny(id, name, "Doctor", "AuxTech", "Police", "Synth", "Working Joe", "Auxiliary", "DroneOperator", "Nurse", "EngineeringTech", "Correspondent"))
            return ("support", Loc.GetString("humanoid-profile-editor-segment-support"));
        if (ContainsAny(id, name, "Leader", "Sergeant", "RadioTelephone"))
            return ("leader", Loc.GetString("humanoid-profile-editor-segment-leader"));
        return ("line", Loc.GetString("humanoid-profile-editor-segment-line"));
    }

    private static int GetJobSortGroup(DepartmentPrototype department, JobPrototype job)
    {
        if (department.Faction is not ("govfor" or "opfor"))
            return 0;
        return GetMilitaryJobSegment(job).Key switch
        {
            "command" => 0,
            "officer" => 1,
            "flight" => 2,
            "support" => 3,
            "leader" => 4,
            _ => 5,
        };
    }

    private static bool ContainsAny(string id, string name, params string[] values) =>
        values.Any(value => id.Contains(value, StringComparison.OrdinalIgnoreCase) ||
                            name.Contains(value, StringComparison.OrdinalIgnoreCase));

    public void RefreshAntags()
    {
        InsurgencyAntagList.RemoveAllChildren();
        ColonyAntagList.RemoveAllChildren();
        DistressAntagList.RemoveAllChildren();
        _antagPreferences.Clear();
        PopulateAntagList(InsurgencyAntagList, GamemodeInsurgency);
        PopulateAntagList(ColonyAntagList, GamemodeColonyFall);
        PopulateAntagList(DistressAntagList, GamemodeDistressSignal);
    }

    private void PopulateAntagList(BoxContainer target, string gamemode)
    {
        var items = new[]
        {
            ("humanoid-profile-editor-antag-preference-yes-button", 0),
            ("humanoid-profile-editor-antag-preference-no-button", 1),
        };
        foreach (var antag in _prototypeManager.EnumerateCM<AntagPrototype>()
                     .Where(antag => antag.SetPreference)
                     .OrderBy(antag => Loc.GetString(antag.Name)))
        {
            var selector = new RequirementsSelector { Margin = new Thickness(3, 3, 3, 0) };
            selector.OnOpenGuidebook += OnOpenGuidebook;
            selector.Setup(items, Loc.GetString(antag.Name), 250, Loc.GetString(antag.Objective), guides: antag.Guides);
            selector.Select(Profile?.GetAntagPreferencesForGamemode(gamemode).Contains(antag.ID) == true ? 0 : 1);
            if (!_requirements.IsAllowed(antag,
                    (HumanoidCharacterProfile?) _preferencesManager.Preferences?.SelectedCharacter,
                    out var reason))
            {
                selector.LockRequirements(reason);
            }
            else
            {
                selector.UnlockRequirements();
            }
            selector.OnSelected += selected =>
            {
                Profile = Profile?.WithGamemodeAntagPreference(gamemode, antag.ID, selected == 0);
                foreach (var (otherGamemode, otherAntag, otherSelector) in _antagPreferences)
                {
                    if (otherGamemode == gamemode && otherAntag == antag.ID)
                        otherSelector.Select(selected);
                }
                SetDirty();
            };
            _antagPreferences.Add((gamemode, antag.ID, selector));
            target.AddChild(selector);
        }
        CrtLobbyTheme.Apply(target);
    }

    public void RefreshThreatPreferences()
    {
        ColonyThreatPreferenceList.RemoveAllChildren();
        DistressThreatPreferenceList.RemoveAllChildren();
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
            Margin = new Thickness(6, 4, 0, 6),
            StyleClasses = { StyleNano.StyleClassCrtHeading },
        });

        foreach (var threat in _prototypeManager.EnumeratePrototypes<ThreatPrototype>()
                     .Where(threat => IsThreatVisibleForGamemode(threat, gamemode))
                     .OrderBy(GetThreatDisplayName))
        {
            var row = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                HorizontalExpand = true,
                Margin = new Thickness(6, 0, 6, 4),
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
            var threatId = threat.ID;
            yes.OnPressed += _ => SetThreatPreference(gamemode, threatId, true);
            no.OnPressed += _ => SetThreatPreference(gamemode, threatId, false);
            _threatPreferenceButtons.Add((gamemode, threatId, yes, no));
            row.AddChild(yes);
            row.AddChild(no);
            target.AddChild(row);
        }
    }

    private static Button MakeThreatPreferenceButton(string text) => new()
    {
        Text = text,
        ToggleMode = true,
        MinWidth = 54,
        StyleClasses = { StyleNano.StyleClassCrtButton },
    };

    private void SetThreatPreference(string gamemode, string threatId, bool preferred)
    {
        Profile = Profile?.WithGamemodeThreatPreference(gamemode, new ProtoId<ThreatPrototype>(threatId), preferred);
        SyncThreatPreferenceButtons();
        SetDirty();
    }

    private void SyncThreatPreferenceButtons()
    {
        foreach (var (gamemode, threatId, yes, no) in _threatPreferenceButtons)
        {
            var selected = Profile?.GetThreatPreferencesForGamemode(gamemode).Any(id => id.Id == threatId) == true;
            yes.Pressed = selected;
            no.Pressed = !selected;
        }
    }

    private static bool IsThreatVisibleForGamemode(ThreatPrototype threat, string gamemode) =>
        !threat.BlacklistedGamemodes.Any(mode => mode.Equals(gamemode, StringComparison.OrdinalIgnoreCase)) &&
        (threat.whitelistedgamemodes.Count == 0 ||
         threat.whitelistedgamemodes.Any(mode => mode.Equals(gamemode, StringComparison.OrdinalIgnoreCase)));

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
                    builder.Append(' ');
            }
            builder.Append(current);
        }
        return builder.ToString().Trim();
    }

    // CMU14 method: opens the platoon rank preference window for one job
    private void OpenRankPreferenceWindowForJob(JobPrototype job, PlatoonRankPreferenceJobEntry? entry)
    {
        if (Profile == null || entry == null)
            return;

        _rankPreferenceWindow?.Close();
        _rankPreferenceWindow = new PlatoonRankPreferenceWindow();

        var currentPreferences = Profile.RankPreferences.TryGetValue(job.ID, out var platoonRanks)
            ? new Dictionary<string, string?>(platoonRanks)
            : new Dictionary<string, string?>();

        _rankPreferenceWindow.PopulateSingleJob(entry, currentPreferences);

        _rankPreferenceWindow.OnSave += prefs =>
        {
            foreach (var (platoonId, rankId) in prefs)
                Profile = Profile?.WithRankPreference(job.ID, platoonId, rankId);

            SetDirty();
            _rankPreferenceWindow?.Close();
        };

        _rankPreferenceWindow.OpenCentered();
    }

    // CMU14 method: builds per-platoon rank options for a job from platoon chevron overrides
    private PlatoonRankPreferenceJobEntry? BuildRankPreferenceJobEntry(JobPrototype job)
    {
        var platoonOptions = new List<PlatoonRankOptions>();

        foreach (var platoon in _prototypeManager.EnumeratePrototypes<PlatoonPrototype>())
        {
            var chevronMap = ResolveChevronMapForPlatoon(job, platoon);
            if (chevronMap == null || chevronMap.Count == 0)
                continue;

            var ranks = new List<RankOption>();
            foreach (var (rankId, chevronDef) in chevronMap)
            {
                if (!_prototypeManager.TryIndex<RankPrototype>(rankId, out var rankProto))
                    continue;

                var (unlocked, requirementsText) = EvaluateChevronRequirements(chevronDef.Requirements);

                ranks.Add(new RankOption(
                    rankId,
                    rankProto.Name,
                    rankProto.Paygrade,
                    unlocked,
                    requirementsText,
                    chevronDef.Entity));
            }

            if (ranks.Count == 0)
                continue;

            platoonOptions.Add(new PlatoonRankOptions(
                platoon.ID,
                platoon.Name,
                platoon.PlatoonPatch,
                ranks));
        }

        return platoonOptions.Count > 0
            ? new PlatoonRankPreferenceJobEntry(job.ID, LobbyHighJobPreview.GetDisplayJobName(job), platoonOptions)
            : null;
    }

    // CMU14 method: platoon override chevron map for a job, else the job's base chevrons
    private Dictionary<string, ChevronDefinition>? ResolveChevronMapForPlatoon(JobPrototype job, PlatoonPrototype platoon)
    {
        if (platoon.ChevronOverrides != null)
        {
            foreach (var (overrideJob, overrideChevrons) in platoon.ChevronOverrides)
            {
                if (JobInheritsFrom(job.ID, overrideJob.Id))
                {
                    return overrideChevrons.ToDictionary(
                        kvp => kvp.Key.Id,
                        kvp => kvp.Value);
                }
            }
        }

        return job.Chevrons;
    }

    // CMU14 method: walks the job inheritance chain
    private bool JobInheritsFrom(string jobId, string ancestorId)
    {
        if (jobId == ancestorId)
            return true;

        if (!_prototypeManager.TryIndex<JobPrototype>(jobId, out var job) || job.Parents == null)
            return false;

        foreach (var parent in job.Parents)
        {
            if (JobInheritsFrom(parent, ancestorId))
                return true;
        }

        return false;
    }

    // CMU14 method: chevron requirement evaluation for the rank window
    private (bool Unlocked, string? RequirementsText) EvaluateChevronRequirements(HashSet<JobRequirement>? requirements)
    {
        if (requirements == null || requirements.Count == 0)
            return (true, null);

        if (!_cfgManager.GetCVar(CCVars.GameRoleTimers))
            return (true, null);

        var playTimes = _requirements.GetPlayTimes(_playerManager.LocalSession!);
        var lines = new List<string>();
        var unlocked = true;

        foreach (var requirement in requirements)
        {
            if (requirement is RoleTimeRequirement roleTime)
            {
                playTimes.TryGetValue(roleTime.Role, out var have);
                var remaining = roleTime.Time - have;
                var remainingMinutes = (int) Math.Ceiling(remaining.TotalMinutes);

                if (!roleTime.Inverted)
                {
                    if (remainingMinutes > 0)
                    {
                        unlocked = false;
                        lines.Add($"Requires {remainingMinutes} more min");
                    }
                }
                else if (remainingMinutes <= 0)
                {
                    unlocked = false;
                    lines.Add($"Requires under {-remainingMinutes} min");
                }
            }
            else if (!requirement.Check(_entManager, _prototypeManager, Profile, playTimes, out var reason))
            {
                unlocked = false;
                if (reason != null)
                    lines.Add(reason.ToMarkup());
            }
        }

        return (unlocked, lines.Count > 0 ? string.Join("\n", lines) : null);
    }
}
