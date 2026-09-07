using System.Linq;
using System.Numerics;
using Content.Client.CMU14.Roles.Ranks;
using Content.Client.Lobby.UI.Loadouts;
using Content.Client.Lobby.UI.Roles;
using Content.Client.Stylesheets;
using Content.Shared.CMU14.Marines.Roles.Chevrons;
using Content.Shared.CMU14.Roles.Ranks;
using Content.Shared._RMC14.Marines.Roles.Ranks;
using Content.Shared._RMC14.Prototypes;
using Content.Shared.CMU14.util;
using Content.Shared.CCVar;
using Content.Shared.Clothing;
using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Roles;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private const string InsurgencyDepartmentId = "AU14DepartmentColonialLiberationFront";

    /// <summary>
    /// Temporary override of the selected job, used to preview roles.
    /// </summary>
    public JobPrototype? JobOverride;

    private LoadoutWindow? _loadoutWindow;
    [ViewVariables] private PlatoonRankPreferenceWindow? _rankPreferenceWindow;

    private readonly List<(string Gamemode, string JobId, RequirementsSelector Selector)> _jobPriorities = new();
    private readonly List<(string Gamemode, string Id, RequirementsSelector Selector)> _antagPreferences = new();
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
    }

    private void OpenLoadout(JobPrototype jobProto, RoleLoadout roleLoadout, RoleLoadoutPrototype roleLoadoutProto)
    {
        _loadoutWindow?.Dispose();
        _loadoutWindow = null;
        var collection = IoCManager.Instance;

        if (collection == null || _playerManager.LocalSession == null || Profile == null)
            return;

        JobOverride = jobProto;
        var session = _playerManager.LocalSession;

        _loadoutWindow = new LoadoutWindow(Profile, roleLoadout, roleLoadoutProto, session, collection)
        {
            Title = Loc.GetString("loadout-window-title-loadout", ("job", jobProto.LocalizedName)),
        };

        _loadoutWindow.RefreshLoadouts(roleLoadout, session, collection);
        _loadoutWindow.OpenCenteredLeft();

        var concreteKey = LoadoutSystem.GetJobPrototype(jobProto.ID);
        _loadoutWindow.OnNameChanged += name =>
        {
            roleLoadout.EntityName = name;
            Profile = Profile.WithLoadout(concreteKey, roleLoadout);
            SetDirty();
        };

        _loadoutWindow.OnLoadoutPressed += (loadoutGroup, loadoutProto) =>
        {
            roleLoadout.AddLoadout(loadoutGroup, loadoutProto, _prototypeManager);
            _loadoutWindow.RefreshLoadouts(roleLoadout, session, collection);
            Profile = Profile?.WithLoadout(concreteKey, roleLoadout);
            ReloadPreview();
        };

        _loadoutWindow.OnLoadoutUnpressed += (loadoutGroup, loadoutProto) =>
        {
            roleLoadout.RemoveLoadout(loadoutGroup, loadoutProto, _prototypeManager);
            _loadoutWindow.RefreshLoadouts(roleLoadout, session, collection);
            Profile = Profile?.WithLoadout(concreteKey, roleLoadout);
            ReloadPreview();
        };

        ReloadPreview();
        _loadoutWindow.OnClose += () =>
        {
            JobOverride = null;
            ReloadPreview();
        };

        UpdateJobPriorities();
    }

    public void RefreshJobs()
    {
        foreach (var list in GetGamemodeJobLists())
            list.DisposeAllChildren();

        _jobCategories.Clear();
        _jobPriorities.Clear();

        var departments = _prototypeManager.EnumeratePrototypes<DepartmentPrototype>()
            .Where(department => !department.EditorHidden)
            .ToList();
        departments.Sort(CompareDepartmentsForCharacterSetup);

        var items = new[]
        {
            ("humanoid-profile-editor-job-priority-never-button", (int) JobPriority.Never),
            ("humanoid-profile-editor-job-priority-low-button", (int) JobPriority.Low),
            ("humanoid-profile-editor-job-priority-medium-button", (int) JobPriority.Medium),
            ("humanoid-profile-editor-job-priority-high-button", (int) JobPriority.High),
        };

        foreach (var department in departments)
        {
            var departmentName = Loc.GetString(department.Name);
            var jobs = department.Roles
                .Select(jobId => _prototypeManager.Index(jobId))
                .Where(job => job.SetPreference && !job.Hidden)
                .ToArray();

            Array.Sort(jobs, (a, b) =>
            {
                var group = GetJobSortGroup(department, a).CompareTo(GetJobSortGroup(department, b));
                return group != 0 ? group : JobUIComparer.Instance.Compare(a, b);
            });

            foreach (var job in jobs)
            {
                foreach (var (target, gamemode, sectionKey, sectionTitle) in
                         GetJobSections(department, job, departmentName))
                {
                    AddJobSelector(
                        target,
                        gamemode,
                        sectionKey,
                        sectionTitle,
                        department.IsCM && !department.Hidden,
                        departmentName,
                        items,
                        job);
                }
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
        var category = GetOrCreateJobCategory(
            sectionKey,
            target,
            sectionTitle,
            visible,
            departmentName);

        var jobContainer = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            HorizontalExpand = true,
        };
        var selector = new RequirementsSelector
        {
            Margin = new Thickness(3f, 3f, 3f, 0f),
            HorizontalExpand = true,
        };
        selector.OnOpenGuidebook += OnOpenGuidebook;

        var icon = new TextureRect
        {
            TextureScale = new Vector2(2, 2),
            VerticalAlignment = VAlignment.Center,
        };
        var jobIcon = _prototypeManager.Index(job.Icon);
        icon.Texture = _sprite.Frame0(jobIcon.Icon);
        selector.Setup(items, GetJobDisplayName(job), 220, job.LocalizedDescription, icon, job.Guides);

        if (!_requirements.IsAllowed(
                job,
                (HumanoidCharacterProfile?) _preferencesManager.Preferences?.SelectedCharacter,
                out var reason))
        {
            selector.LockRequirements(reason);
        }
        else if (Profile != null && job.IsSynthetic != Profile.Synthetic)
        {
            selector.LockRequirements(FormattedMessage.FromUnformatted(
                Loc.GetString(job.IsSynthetic
                    ? "humanoid-profile-editor-synthetic-locked-job"
                    : "humanoid-profile-editor-synthetic-locked-job-non-synthetic")));
        }
        else
        {
            selector.UnlockRequirements();
        }

        selector.OnSelected += selectedPriority =>
        {
            var selectedJobPriority = (JobPriority) selectedPriority;
            Profile = Profile?.WithGamemodeJobPriority(gamemode, job.ID, selectedJobPriority);

            foreach (var (otherGamemode, jobId, other) in _jobPriorities)
            {
                if (otherGamemode != gamemode)
                    continue;

                if (jobId == job.ID)
                {
                    other.Select(selectedPriority);
                    continue;
                }

                if (selectedJobPriority != JobPriority.High || (JobPriority) other.Selected != JobPriority.High)
                    continue;

                other.Select((int) JobPriority.Medium);
                Profile = Profile?.WithGamemodeJobPriority(gamemode, jobId, JobPriority.Medium);
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
            Margin = new Thickness(3f, 3f, 0f, 0f),
            MinWidth = 110,
            StyleClasses = { StyleNano.StyleClassCrtButton },
        };

        var (loadoutKey, loadoutProto) = LoadoutSystem.GetJobLoadoutInfo(job.ID, _prototypeManager);
        if (loadoutProto == null)
        {
            loadoutButton.Disabled = true;
        }
        else
        {
            loadoutButton.OnPressed += _ =>
            {
                RoleLoadout? loadout = null;
                if (Profile?.Loadouts.TryGetValue(loadoutKey, out var existing) == true)
                    loadout = existing.Clone();

                if (loadout == null)
                {
                    loadout = new RoleLoadout(loadoutProto.ID);
                    loadout.SetDefault(Profile, _playerManager.LocalSession, _prototypeManager);
                }

                OpenLoadout(job, loadout, loadoutProto);
            };
        }

        var rankEntry = BuildRankPreferenceJobEntry(job);
        var rankButton = new Button
        {
            Text = Loc.GetString("cmu14-rank-preference-button"),
            HorizontalAlignment = HAlignment.Right,
            VerticalAlignment = VAlignment.Center,
            Margin = new Thickness(3f, 3f, 0f, 0f),
            MinWidth = 90,
            StyleClasses = { StyleNano.StyleClassCrtButton },
            Disabled = rankEntry == null,
        };
        rankButton.OnPressed += _ => OpenRankPreferenceWindowForJob(job, rankEntry);

        _jobPriorities.Add((gamemode, job.ID, selector));
        jobContainer.AddChild(selector);
        jobContainer.AddChild(loadoutButton);
        jobContainer.AddChild(rankButton);
        CrtLobbyTheme.Apply(jobContainer);
        category.AddChild(jobContainer);
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

    private BoxContainer GetOrCreateJobCategory(
        string key,
        BoxContainer target,
        string title,
        bool visible,
        string departmentName)
    {
        if (_jobCategories.TryGetValue(key, out var category))
            return category;

        category = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            Name = key,
            ToolTip = Loc.GetString(
                "humanoid-profile-editor-jobs-amount-in-department-tooltip",
                ("departmentName", departmentName)),
            Visible = visible,
        };

        if (target.Children.Any(child => child.Visible) && visible)
        {
            category.AddChild(new Control
            {
                MinSize = new Vector2(0, 14),
            });
        }

        category.AddChild(new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = StyleNano.CrtPanelBackgroundAlt,
                BorderColor = StyleNano.CrtGreenDim,
                BorderThickness = new Thickness(0, 0, 0, 1),
                ContentMarginTopOverride = 3,
                ContentMarginBottomOverride = 3,
            },
            Children =
            {
                new Label
                {
                    Text = title,
                    Margin = new Thickness(6f, 0, 0, 0),
                    StyleClasses = { StyleNano.StyleClassCrtHeading },
                },
            },
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
            var (segmentKey, segmentTitle) = GetMilitaryJobSegment(job);
            yield return (
                InsurgencyGovernmentJobList,
                GamemodeInsurgency,
                $"insurgency-govfor-{segmentKey}",
                Loc.GetString("humanoid-profile-editor-government-forces-label", ("segmentTitle", segmentTitle)));
            yield return (
                DistressGovernmentJobList,
                GamemodeDistressSignal,
                $"distress-govfor-{segmentKey}",
                Loc.GetString("humanoid-profile-editor-government-forces-label", ("segmentTitle", segmentTitle)));
            yield break;
        }

        if (department.Faction == "opfor")
            yield break;

        if (IsInsurgencyDepartment(department))
        {
            yield return (
                InsurgencyInsurgentJobList,
                GamemodeInsurgency,
                $"insurgency-jobs-{department.ID}",
                department.CustomName ?? Loc.GetString(
                    "humanoid-profile-editor-department-jobs-label",
                    ("departmentName", departmentName)));
            yield break;
        }

        if (department.Faction == "colonist")
        {
            var title = department.CustomName ?? Loc.GetString(
                "humanoid-profile-editor-department-jobs-label",
                ("departmentName", departmentName));
            yield return (
                InsurgencyCivilianJobList,
                GamemodeInsurgency,
                $"insurgency-civilian-{department.ID}",
                title);
            yield return (
                ColonyCivilianJobList,
                GamemodeColonyFall,
                $"colony-civilian-{department.ID}",
                title);
            yield break;
        }

        if (!IsRoundStartThreatAssignmentJob(job))
            yield break;

        yield return (ColonyThreatJobList, GamemodeColonyFall, "colony-threat", "Threat Jobs");
        yield return (DistressThreatJobList, GamemodeDistressSignal, "distress-threat", "Threat Jobs");
    }

    private static int CompareDepartmentsForCharacterSetup(DepartmentPrototype? x, DepartmentPrototype? y)
    {
        if (ReferenceEquals(x, y))
            return 0;
        if (ReferenceEquals(null, y))
            return 1;
        if (ReferenceEquals(null, x))
            return -1;

        var colonyGroup = GetColonyDepartmentSortGroup(x).CompareTo(GetColonyDepartmentSortGroup(y));
        return colonyGroup != 0 ? colonyGroup : DepartmentUIComparer.Instance.Compare(x, y);
    }

    private static int GetColonyDepartmentSortGroup(DepartmentPrototype department)
    {
        return department.ID switch
        {
            "AU14DepartmentColonyCommand" => 0,
            "AU14DepartmentColonySecurity" => 1,
            "AU14DepartmentColonyMedical" => 2,
            "AU14DepartmentEngineering" => 3,
            "AU14DepartmentCivilian" => 4,
            "AU14DepartmentCriminal" => 5,
            "AU14DepartmentCorporate" => 6,
            "AU14DepartmentAmbassadors" => 7,
            "AU14DepartmentOrbital" => 8,
            _ => 100,
        };
    }

    private static bool IsInsurgencyDepartment(DepartmentPrototype department)
    {
        return department.ID == InsurgencyDepartmentId;
    }

    private static bool IsRoundStartThreatAssignmentJob(JobPrototype job)
    {
        return job.ID is
            "AU14JobThreatLeader" or
            "AU14JobThreatMember" or
            "AU14JobThirdPartyLeader" or
            "AU14JobThirdPartyMember";
    }

    private static (string Key, string Title) GetMilitaryJobSegment(JobPrototype job)
    {
        var id = job.ID;
        var name = job.LocalizedName;

        if (id is "AU14JobGOVFORVehicleCommander")
            return ("flight", Loc.GetString("humanoid-profile-editor-segment-flight"));
        if (ContainsAny(id, name, "MilitaryDoctor"))
            return ("support", Loc.GetString("humanoid-profile-editor-segment-support"));
        if (job.MarineAuthorityLevel > 0 ||
            ContainsAny(id, name, "PlatCo", "Adjutant", "PlatOp", "Commander", "Command", "Advisor"))
        {
            return ("command", Loc.GetString("humanoid-profile-editor-segment-command"));
        }
        if (ContainsAny(id, name, "Pilot", "Dropship", "Crew Chief", "DCC", "VehicleCrewman"))
            return ("flight", Loc.GetString("humanoid-profile-editor-segment-flight"));
        if (ContainsAny(id, name, "Officer", "Chief"))
            return ("officer", Loc.GetString("humanoid-profile-editor-segment-officer"));
        if (ContainsAny(
                id,
                name,
                "Doctor",
                "AuxTech",
                "Police",
                "Synth",
                "Working Joe",
                "Auxiliary",
                "DroneOperator",
                "Nurse",
                "EngineeringTech",
                "Correspondent"))
        {
            return ("support", Loc.GetString("humanoid-profile-editor-segment-support"));
        }
        if (ContainsAny(id, name, "Leader", "Sergeant", "RadioTelephone"))
            return ("leader", Loc.GetString("humanoid-profile-editor-segment-leader"));

        return ("line", Loc.GetString("humanoid-profile-editor-segment-line"));
    }

    private static int GetJobSortGroup(DepartmentPrototype department, JobPrototype job)
    {
        if (department.Faction != "govfor" && department.Faction != "opfor")
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

    private static string GetJobDisplayName(JobPrototype job)
    {
        return LobbyHighJobPreview.GetDisplayJobName(job);
    }

    private static bool ContainsAny(string id, string name, params string[] needles)
    {
        return needles.Any(needle =>
            id.Contains(needle, StringComparison.OrdinalIgnoreCase) ||
            name.Contains(needle, StringComparison.OrdinalIgnoreCase));
    }

    public void RefreshAntags()
    {
        InsurgencyAntagList.DisposeAllChildren();
        ColonyAntagList.DisposeAllChildren();
        DistressAntagList.DisposeAllChildren();
        _antagPreferences.Clear();

        PopulateAntagPreferences(InsurgencyAntagList, GamemodeInsurgency);
        PopulateAntagPreferences(ColonyAntagList, GamemodeColonyFall);
        PopulateAntagPreferences(DistressAntagList, GamemodeDistressSignal);
    }

    private void PopulateAntagPreferences(BoxContainer target, string gamemode)
    {
        var items = new[]
        {
            ("humanoid-profile-editor-antag-preference-yes-button", 0),
            ("humanoid-profile-editor-antag-preference-no-button", 1),
        };

        var antagsByCategory = _prototypeManager.EnumerateCM<AntagPrototype>()
            .Where(antag => antag.SetPreference)
            .GroupBy(antag => string.IsNullOrEmpty(antag.Category)
                ? Loc.GetString("humanoid-profile-editor-antag-category-uncategorized")
                : Loc.GetString(antag.Category))
            .OrderBy(group => group.Key);

        foreach (var categoryGroup in antagsByCategory)
        {
            var category = new BoxContainer
            {
                Orientation = LayoutOrientation.Vertical,
            };
            category.AddChild(new Label
            {
                Text = categoryGroup.Key,
                StyleClasses = { StyleNano.StyleClassLabelHeadingBigger },
            });

            foreach (var antag in categoryGroup.OrderBy(antag => Loc.GetString(antag.Name)))
            {
                var row = new BoxContainer
                {
                    Orientation = LayoutOrientation.Horizontal,
                };
                var selector = new RequirementsSelector
                {
                    Margin = new Thickness(3f, 3f, 3f, 0f),
                };
                selector.OnOpenGuidebook += OnOpenGuidebook;
                selector.Setup(
                    items,
                    Loc.GetString(antag.Name),
                    250,
                    Loc.GetString(antag.Objective),
                    guides: antag.Guides);
                selector.Select(Profile?.GetAntagPreferencesForGamemode(gamemode).Contains(antag.ID) == true ? 0 : 1);

                if (!_requirements.IsAllowed(
                        antag,
                        (HumanoidCharacterProfile?) _preferencesManager.Preferences?.SelectedCharacter,
                        out var reason))
                {
                    selector.LockRequirements(reason);
                    Profile = Profile?.WithGamemodeAntagPreference(gamemode, antag.ID, false);
                    SetDirty();
                }
                else
                {
                    selector.UnlockRequirements();
                }

                selector.OnSelected += preference =>
                {
                    Profile = Profile?.WithGamemodeAntagPreference(gamemode, antag.ID, preference == 0);
                    foreach (var (otherGamemode, antagId, other) in _antagPreferences)
                    {
                        if (otherGamemode == gamemode && antagId == antag.ID)
                            other.Select(preference);
                    }

                    SetDirty();
                };

                _antagPreferences.Add((gamemode, antag.ID, selector));
                row.AddChild(selector);
                row.AddChild(new Button
                {
                    Disabled = true,
                    Text = Loc.GetString("loadout-window"),
                    HorizontalAlignment = HAlignment.Right,
                    Margin = new Thickness(3f, 0f, 0f, 0f),
                });
                category.AddChild(row);
            }

            target.AddChild(category);
        }

        CrtLobbyTheme.Apply(target);
    }

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
        _rankPreferenceWindow.OnSave += preferences =>
        {
            foreach (var (platoonId, rankId) in preferences)
                Profile = Profile?.WithRankPreference(job.ID, platoonId, rankId);

            SetDirty();
            _rankPreferenceWindow?.Close();
        };
        _rankPreferenceWindow.OpenCentered();
    }

    private PlatoonRankPreferenceJobEntry? BuildRankPreferenceJobEntry(JobPrototype job)
    {
        var platoonOptions = new List<PlatoonRankOptions>();
        foreach (var platoon in _prototypeManager.EnumeratePrototypes<PlatoonPrototype>())
        {
            var chevronMap = ResolveChevronMapForPlatoon(job, platoon);
            if (chevronMap == null || chevronMap.Count == 0)
                continue;

            var ranks = new List<RankOption>();
            foreach (var (rankId, chevron) in chevronMap)
            {
                if (!_prototypeManager.TryIndex<RankPrototype>(rankId, out var rank))
                    continue;

                var (unlocked, requirementsText) = EvaluateChevronRequirements(chevron.Requirements);
                ranks.Add(new RankOption(
                    rankId,
                    rank.Name,
                    rank.Paygrade,
                    unlocked,
                    requirementsText,
                    chevron.Entity));
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
            ? new PlatoonRankPreferenceJobEntry(job.ID, GetJobDisplayName(job), platoonOptions)
            : null;
    }

    private Dictionary<string, ChevronDefinition>? ResolveChevronMapForPlatoon(
        JobPrototype job,
        PlatoonPrototype platoon)
    {
        if (platoon.ChevronOverrides != null)
        {
            foreach (var (overrideJob, overrideChevrons) in platoon.ChevronOverrides)
            {
                if (!JobInheritsFrom(job.ID, overrideJob.Id))
                    continue;

                return overrideChevrons.ToDictionary(pair => pair.Key.Id, pair => pair.Value);
            }
        }

        return job.Chevrons;
    }

    private bool JobInheritsFrom(string jobId, string ancestorId)
    {
        if (jobId == ancestorId)
            return true;
        if (!_prototypeManager.TryIndex<JobPrototype>(jobId, out var job) || job.Parents == null)
            return false;

        return job.Parents.Any(parent => JobInheritsFrom(parent, ancestorId));
    }

    private (bool Unlocked, string? RequirementsText) EvaluateChevronRequirements(
        HashSet<JobRequirement>? requirements)
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
                var remainingMinutes = (int) Math.Ceiling((roleTime.Time - have).TotalMinutes);
                if (!roleTime.Inverted && remainingMinutes > 0)
                {
                    unlocked = false;
                    lines.Add($"Requires {remainingMinutes} more min");
                }
                else if (roleTime.Inverted && remainingMinutes <= 0)
                {
                    unlocked = false;
                    lines.Add($"Requires under {-remainingMinutes} min");
                }

                continue;
            }

            if (requirement.Check(_entManager, _prototypeManager, Profile, playTimes, out var reason))
                continue;

            unlocked = false;
            if (reason != null)
                lines.Add(reason.ToMarkup());
        }

        return (unlocked, lines.Count > 0 ? string.Join("\n", lines) : null);
    }
}
