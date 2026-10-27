using System.Linq;
using System.Numerics;
using Content.Client.GameTicking.Managers;
using Content.Client.LateJoin;
using Content.Client.Lobby;
using Content.Client.Lobby.UI;
using Content.Client.Lobby.UI.ProfileEditorControls;
using Content.Client.Players.PlayTimeTracking;
using Content.Client.CrewManifest;
using Content.Shared.CCVar;
using Content.Shared.CMU14.Lobby;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Client.Console;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.Lobby;

/// <summary>One station and faction at a time, using the same availability and admission checks as late join.</summary>
public sealed partial class CmuJoinRoster : BoxContainer
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private JobRequirementsManager _requirements = default!;
    [Dependency] private IClientPreferencesManager _preferences = default!;
    [Dependency] private IClientConsoleHost _console = default!;
    [Dependency] private IConfigurationManager _configuration = default!;
    private readonly WrapContainer _stations = new() { SeparationOverride = 6, CrossSeparationOverride = 6 };
    private readonly BoxContainer _roles = new() { Orientation = LayoutOrientation.Vertical, SeparationOverride = 10 };
    private readonly ScrollContainer _scroll = new() { HScrollEnabled = false, VerticalExpand = true, HorizontalExpand = true };
    private readonly Dictionary<NetEntity, List<CmuLobbyRosterEntry>> _records = new();
    private readonly ClientGameTicker _ticker;
    private readonly CmuLobbyRosterSystem _roster;
    private string _faction = "colonists";
    private NetEntity? _station;
    public event Action? Joined;

    public CmuJoinRoster()
    {
        IoCManager.InjectDependencies(this);
        _ticker = _entities.System<ClientGameTicker>();
        _roster = _entities.System<CmuLobbyRosterSystem>();
        Orientation = LayoutOrientation.Vertical;
        HorizontalExpand = true;
        VerticalExpand = true;
        SeparationOverride = 10;
        AddChild(_stations);
        _scroll.AddChild(_roles);
        AddChild(_scroll);
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();
        _roster.Updated += OnRoster;
        _ticker.LobbyJobsAvailableUpdated += OnJobs;
        _ticker.LobbyLateJoinStatusUpdated += Render;
        _requirements.Updated += Render;
        RefreshStations();
    }

    protected override void ExitedTree()
    {
        _roster.Updated -= OnRoster;
        _ticker.LobbyJobsAvailableUpdated -= OnJobs;
        _ticker.LobbyLateJoinStatusUpdated -= Render;
        _requirements.Updated -= Render;
        base.ExitedTree();
    }

    public void SelectFaction(string faction)
    {
        if (_faction == faction && _station != null) return;
        _faction = faction;
        RefreshStations();
    }

    public bool HasFaction(string faction) => _ticker.JobsAvailable.Any(station =>
        _prototypes.EnumeratePrototypes<DepartmentPrototype>().Any(department => department.IsCM && !department.Hidden &&
            LateJoinGui.DepartmentMatchesFilter(department, faction) &&
            department.Roles.Any(job => LateJoinGui.JobMatchesFilter(faction, job) && station.Value.ContainsKey(job))));

    public int AvailableRoles(string faction) => _ticker.JobsAvailable.SelectMany(station =>
        _prototypes.EnumeratePrototypes<DepartmentPrototype>()
            .Where(department => department.IsCM && !department.Hidden && LateJoinGui.DepartmentMatchesFilter(department, faction))
            .SelectMany(department => department.Roles)
            .Where(job => LateJoinGui.JobMatchesFilter(faction, job) && station.Value.TryGetValue(job, out var count) && count != 0))
        .Distinct().Count();

    private void OnJobs(IReadOnlyDictionary<NetEntity, Dictionary<ProtoId<JobPrototype>, int?>> _) => RefreshStations();

    private void RefreshStations()
    {
        _stations.RemoveAllChildren();
        var stations = _ticker.StationNames.Where(station => _ticker.JobsAvailable.TryGetValue(station.Key, out var jobs) &&
            _prototypes.EnumeratePrototypes<DepartmentPrototype>().Any(department => department.IsCM && !department.Hidden &&
                LateJoinGui.DepartmentMatchesFilter(department, _faction) &&
                department.Roles.Any(job => LateJoinGui.JobMatchesFilter(_faction, job) && jobs.ContainsKey(job)))).ToArray();
        if (_station == null || stations.All(station => station.Key != _station))
            _station = stations.Length > 0 ? stations[0].Key : null;
        foreach (var (id, name) in stations)
        {
            var button = CmuLobbyPresentation.Action(name);
            CmuLobbyPresentation.Style(button, CmuLobbyPresentation.FactionColor(_faction), id == _station);
            button.OnPressed += _ =>
            {
                if (_station == id) return;
                _station = id;
                RefreshStations();
            };
            _stations.AddChild(button);
        }
        if (_station is { } selected)
        {
            var manifest = CmuLobbyPresentation.Action(Loc.GetString("crew-manifest-button-label"));
            manifest.HorizontalExpand = false;
            manifest.Visible = _configuration.GetCVar(CCVars.CrewManifestWithoutEntity);
            manifest.OnPressed += _ => _entities.System<CrewManifestSystem>().RequestCrewManifest(selected);
            _stations.AddChild(manifest);
            _roster.Request(selected);
        }
        Render();
        CrtLobbyTheme.Apply(_stations);
    }

    private void OnRoster(CmuLobbyRosterResponse response)
    {
        _records[response.Station] = response.Entries;
        if (_station == response.Station) Render();
    }

    private void Render()
    {
        var scroll = _scroll.GetScrollValue(ignoreVisible: true);
        _roles.ReleaseChildren();
        if (_station is not { } station || !_ticker.JobsAvailable.TryGetValue(station, out var available))
        {
            _roles.AddChild(new Label { Text = Loc.GetString("late-join-gui-no-departments-available") });
            return;
        }
        var records = _records.GetValueOrDefault(station) ?? new List<CmuLobbyRosterEntry>();
        var departments = _prototypes.EnumeratePrototypes<DepartmentPrototype>()
            .Where(department => department.IsCM && !department.Hidden && LateJoinGui.DepartmentMatchesFilter(department, _faction)).ToArray();
        Array.Sort(departments, DepartmentUIComparer.Instance);
        var seen = new HashSet<string>();
        if (_ticker.DisallowedLateJoin)
            _roles.AddChild(new Label { Text = Loc.GetString("cmu-workspace-late-join-disabled"), ClipText = true, FontColorOverride = CmuLobbyPresentation.Text });
        foreach (var department in departments)
        {
            var jobs = department.Roles.Where(id => LateJoinGui.JobMatchesFilter(_faction, id) &&
                (available.ContainsKey(id) || records.Any(entry => entry.Record.JobPrototype == id)))
                .Select(id => _prototypes.Index(id)).Where(job => seen.Add(job.ID)).ToArray();
            if (jobs.Length == 0) continue;
            if (JobUIComparer.TryCreate(_prototypes, _ticker.JobWeightsByStation.GetValueOrDefault(station), out var comparer))
                Array.Sort(jobs, comparer);
            var departmentHeading = new CmuAccentLabel(CmuLobbyPresentation.FactionColor(_faction))
            {
                Text = Loc.GetString(department.Name), ClipText = true, Margin = new Thickness(0, 8, 0, 4),
            };
            _roles.AddChild(departmentHeading);
            foreach (var group in jobs.GroupBy(job => department.Faction is "govfor" or "opfor"
                         ? HumanoidProfileEditor.GetMilitaryJobSegment(job).Title : string.Empty))
            {
                if (group.Key.Length > 0)
                    _roles.AddChild(new Label { Text = group.Key, ClipText = true, FontColorOverride = CmuLobbyPresentation.Muted });
                var grid = new CmuAdaptiveGrid(280) { HorizontalExpand = true, HSeparationOverride = 10, VSeparationOverride = 10 };
                _roles.AddChild(grid);
                foreach (var job in group)
                    AddPosition(grid, job, station, available, records);
            }
        }
        CrtLobbyTheme.Apply(_roles);
        _scroll.SetScrollValue(scroll);
        _scroll.VScrollTarget = scroll.Y;
    }

    private void AddPosition(CmuAdaptiveGrid grid, JobPrototype job, NetEntity station,
        Dictionary<ProtoId<JobPrototype>, int?> available, List<CmuLobbyRosterEntry> records)
    {
        var occupants = records.Where(entry => entry.Record.JobPrototype == job.ID).ToArray();
        var listed = available.TryGetValue(job.ID, out var count);
        var accent = CmuLobbyPresentation.FactionColor(_faction);
        var card = new PanelContainer
        {
            Name = job.ID, PanelOverride = CmuLobbyPresentation.Box(accent, padding: 12), HorizontalExpand = true,
        };
        var content = new BoxContainer { Orientation = LayoutOrientation.Vertical, SeparationOverride = 10 };
        var heading = new BoxContainer { SeparationOverride = 8, HorizontalExpand = true };
        var jobIcon = _prototypes.Index(job.Icon);
        heading.AddChild(new TextureRect
        {
            Texture = _entities.System<SpriteSystem>().Frame0(jobIcon.Icon),
            TextureScale = new Vector2(2),
            VerticalAlignment = VAlignment.Center,
            MouseFilter = MouseFilterMode.Ignore,
        });
        var title = new RichTextLabel { HorizontalExpand = true, ToolTip = job.LocalizedDescription };
        title.SetMessage(job.LocalizedName, CmuLobbyPresentation.Text);
        heading.AddChild(title);
        content.AddChild(heading);
        card.AddChild(content);
        grid.AddChild(card);
        foreach (var entry in occupants)
            content.AddChild(Occupied(entry));

        // Occupied people already represent filled slots. Never invent a second, empty
        // "filled" position next to them; only real vacancies get a join control.
        if (!listed || count == 0)
        {
            if (occupants.Length == 0)
                content.AddChild(new Label
                {
                    Text = Loc.GetString("cmu-workspace-no-vacancies"), ClipText = true,
                    FontColorOverride = CmuLobbyPresentation.Muted,
                });
            return;
        }
        var allowed = _requirements.IsAllowed(job, _preferences.Preferences?.SelectedCharacter as HumanoidCharacterProfile, out var reason);
        var disabled = !allowed || _ticker.DisallowedLateJoin;
        var button = CmuLobbyPresentation.Action(
            Loc.GetString(count == null ? "cmu-workspace-vacancies-unlimited" : "cmu-workspace-vacancies", ("count", count ?? 0)), accent);
        button.MinHeight = 46;
        button.ToolTip = job.LocalizedName + "\n" + job.LocalizedDescription;
        button.Disabled = disabled;
        if (disabled)
        {
            content.AddChild(new Label
            {
                Text = Loc.GetString("cmu-workspace-position-locked"), ClipText = true,
                FontColorOverride = CmuLobbyPresentation.Muted,
            });
        }
        if (!allowed && reason is { IsEmpty: false })
        {
            var tooltip = new Tooltip();
            tooltip.SetMessage(reason);
            button.TooltipSupplier = _ => tooltip;
        }
        button.OnPressed += _ =>
        {
            _console.ExecuteCommand($"joingame {CommandParsing.Escape(job.ID)} {station}");
            Joined?.Invoke();
        };
        content.AddChild(button);
    }

    private Control Occupied(CmuLobbyRosterEntry entry)
    {
        var row = new BoxContainer { SeparationOverride = 12, MinHeight = 88 };
        if (entry.IdentityHidden)
        {
            row.AddChild(new CmuObscuredPortrait());
        }
        else if (entry.Portrait is { } portrait && entry.Outfit is { Count: > 0 } outfit)
        {
            var view = new ProfilePreviewSpriteView { MinSize = new Vector2(64, 88), MaxWidth = 64, Scale = new Vector2(2), OverrideDirection = Direction.South };
            var profile = HumanoidCharacterProfile.DefaultWithSpecies(portrait.Species, portrait.Sex)
                .WithName(entry.Record.Name).WithCharacterAppearance(portrait.Appearance);
            view.LoadArrivalPreview(profile, outfit);
            row.AddChild(view);
        }
        else
        {
            // Records created without an arrival image still identify the person, without
            // displaying an unclothed substitute or somebody else's default job equipment.
            row.AddChild(new Label
            {
                Text = Loc.GetString("cmu-workspace-portrait-pending"), MinWidth = 64,
                FontColorOverride = CmuLobbyPresentation.Muted, VerticalAlignment = VAlignment.Center,
            });
        }
        var text = new BoxContainer { Orientation = LayoutOrientation.Vertical, HorizontalExpand = true, VerticalAlignment = VAlignment.Center };
        if (!entry.IdentityHidden)
        {
            text.AddChild(new Label { Text = entry.Record.Name, ClipText = true, ToolTip = entry.Record.Name, FontColorOverride = CmuLobbyPresentation.Text });
            if (entry.Record.Squad != null)
                text.AddChild(new CmuAccentLabel(entry.Record.SquadColor) { Text = entry.Record.Squad, ClipText = true });
        }
        text.AddChild(new Label { Text = Loc.GetString("cmu-workspace-assigned"), ClipText = true, FontColorOverride = CmuLobbyPresentation.Muted });
        row.AddChild(text);
        return row;
    }
}
