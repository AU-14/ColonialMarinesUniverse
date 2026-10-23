using System.Linq;
using System.Numerics;
using Content.Client.CrewManifest;
using Content.Client.GameTicking.Managers;
using Content.Client.Lobby;
using Content.Client.Lobby.UI;
using Content.Client.CMU14.Interface;
using Content.Client.UserInterface.Controls;
using Content.Client.Players.PlayTimeTracking;
using Content.Client.Stylesheets;
using Content.Shared._RMC14.Prototypes;
using Content.Shared.CCVar;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared.StatusIcon;
using Robust.Client.Console;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client.LateJoin
{
    /// <summary>
    ///     The rail-and-pane design from <c>docs/cmu/crt-joinmenu.html</c> (direction C), on real
    ///     stations, departments and jobs instead of invented ones. Role groups down a rail on the
    ///     left, the selected one's jobs in the pane beside it - see <see cref="RebuildUI"/>.
    /// </summary>
    public sealed partial class LateJoinGui : DefaultWindow
    {
        [Dependency] private IPrototypeManager _prototypeManager = default!;
        [Dependency] private IClientConsoleHost _consoleHost = default!;
        [Dependency] private IConfigurationManager _configManager = default!;
        [Dependency] private IEntitySystemManager _entitySystem = default!;
        [Dependency] private JobRequirementsManager _jobRequirements = default!;
        [Dependency] private IClientPreferencesManager _preferencesManager = default!;
        [Dependency] private IStylesheetManager _stylesheetManager = default!;
        [Dependency] private IResourceCache _resourceCache = default!;
        [Dependency] private ILogManager _logManager = default!;

        public event Action<(NetEntity, string)> SelectedId;

        private readonly ClientGameTicker _gameTicker;
        private readonly SpriteSystem _sprites;
        private readonly CrewManifestSystem _crewManifest;
        private readonly ISawmill _sawmill;

        private readonly string? _factionFilter;

        private readonly Dictionary<NetEntity, Dictionary<string, List<JobButton>>> _jobButtons = new();

        private readonly List<StationSection> _sections = new();

        private readonly Control _base;
        private readonly LineEdit _filter;

        private PanelContainer _ground = default!;
        private CrtScreenControl _shaderPass = default!;
        private Control _cornerTicks = default!;

        private const int TitleSize = 8;

        private const int BannerSize = 9;
        private const int RailSize = 9;
        private const int RowSize = 9;
        private const int NoteSize = 8;

        private const float ScanlineDepth = 0.55f;

        private const string AllGroupId = "__all__";

        private const float RailWidth = 240f;

        private const float MinBandHeight = 96f;
        private const float MaxBandHeight = 360f;

        private static readonly Color RowRule = Color.FromHex("#10251A");
        private static readonly Color TextFaint = Color.FromHex("#3A6B4D");

        public LateJoinGui(string? factionFilter = null)
        {
            _factionFilter = factionFilter?.ToLowerInvariant();
            MinSize = new Vector2(700, 480);
            SetSize = new Vector2(820, 560);
            IoCManager.InjectDependencies(this);
            _sprites = _entitySystem.GetEntitySystem<SpriteSystem>();
            _crewManifest = _entitySystem.GetEntitySystem<CrewManifestSystem>();
            _gameTicker = _entitySystem.GetEntitySystem<ClientGameTicker>();
            _sawmill = _logManager.GetSawmill("latejoin.panel");

            Title = Loc.GetString("late-join-gui-title");

            _base = new BoxContainer()
            {
                Orientation = LayoutOrientation.Vertical,
                HorizontalExpand = true,
                // Top, not Stretch: the scroll below hands this the full window height, which Stretch would keep.
                VerticalAlignment = VAlignment.Top,
                SeparationOverride = 10,
                Margin = new Thickness(8),
            };

            var scroll = new ScrollContainer
            {
                HorizontalExpand = true,
                VerticalExpand = true,
                Children = { _base },
            };

            _filter = new LineEdit
            {
                HorizontalExpand = true,
                PlaceHolder = Loc.GetString("late-join-gui-filter-placeholder"),
            };
            _filter.AddStyleClass(StyleNano.StyleClassCrtLineEdit);
            _filter.OnTextChanged += _ => ApplyFilter();

            _ground = new PanelContainer
            {
                HorizontalExpand = true,
                VerticalExpand = true,
                Children =
                {
                    new BoxContainer
                    {
                        Orientation = LayoutOrientation.Vertical,
                        HorizontalExpand = true,
                        VerticalExpand = true,
                        Margin = new Thickness(1),
                        SeparationOverride = 8,
                        Children = { FilterBar(_filter), scroll },
                    },
                },
            };
            ContentsContainer.Margin = new Thickness(0);
            ContentsContainer.AddChild(_ground);

            _cornerTicks = CrtLobbyTheme.CornerTicks();
            _ground.AddChild(_cornerTicks);

            _shaderPass = new CrtScreenControl
            {
                Source = Children.OfType<BoxContainer>().First(),
                HorizontalExpand = true,
                VerticalExpand = true,
                Phosphor = StyleNano.CrtGreen,
                Intensity = ScanlineDepth,
                Curvature = 0f,
                Grain = false,
                Roll = false,
                ArtifactAmount = 0f,
            };
            AddChild(_shaderPass);

            ApplyTheme();

            _jobRequirements.Updated += RebuildUI;
            RebuildUI();

            SelectedId += x =>
            {
                var (station, jobId) = x;
                _sawmill.Info($"Late joining as ID: {jobId}");
                _consoleHost.ExecuteCommand($"joingame {CommandParsing.Escape(jobId)} {station}");
                Close();
            };

            _gameTicker.LobbyJobsAvailableUpdated += JobsAvailableUpdated;
            _configManager.OnValueChanged(CCVars.CrtUiColor, OnCrtUiColorChanged);
            _configManager.OnValueChanged(CCVars.CrtUiEnabled, OnCrtUiEnabledChanged);
        }


        private void OnCrtUiColorChanged(string _) => ApplyTheme();

        private void OnCrtUiEnabledChanged(bool _) => ApplyTheme();

        private void ApplyTheme()
        {
            Stylesheet = _stylesheetManager.SheetNano;
            CrtLobbyTheme.Apply(this);

            var crt = StyleNano.CrtUiEnabled;
            var header = FindControl<PanelContainer>("WindowHeader");
            var titleLabel = FindControl<Label>("TitleLabel");
            var closeButton = FindControl<TextureButton>("CloseButton");

            if (crt)
            {
                AddStyleClass(StyleNano.StyleClassCrtWindow);

                header.PanelOverride = new StyleBoxFlat
                {
                    BackgroundColor = CrtTerminalPalette.Surface2,
                    BorderColor = CrtTerminalPalette.Line,
                    BorderThickness = new Thickness(1),
                    ContentMarginLeftOverride = 5,
                    ContentMarginRightOverride = 6,
                };

                titleLabel.FontOverride = StyleNano.GetCrtFont(_resourceCache, TitleSize);
                titleLabel.FontColorOverride = CrtTerminalPalette.TextBright;
                closeButton.Modulate = CrtTerminalPalette.TextDim;

                _ground.PanelOverride = new StyleBoxFlat
                {
                    BackgroundColor = CrtTerminalPalette.Surface0,
                    BorderColor = CrtTerminalPalette.Line,
                    BorderThickness = new Thickness(1, 0, 1, 1),
                };
            }
            else
            {
                RemoveStyleClass(StyleNano.StyleClassCrtWindow);
                header.PanelOverride = null;
                titleLabel.FontOverride = null;
                titleLabel.FontColorOverride = null;
                closeButton.Modulate = Color.White;
                _ground.PanelOverride = null;
            }

            _shaderPass.Visible = crt;
            _cornerTicks.Visible = crt;
        }

        private bool DepartmentMatchesFilter(DepartmentPrototype department)
        {
            return DepartmentMatchesFilter(department, _factionFilter);
        }

        public static bool DepartmentMatchesFilter(DepartmentPrototype department, string? factionFilter)
        {
            factionFilter = factionFilter?.ToLowerInvariant();
            if (string.IsNullOrEmpty(factionFilter))
                return true;

            if (factionFilter is "hunt" or "hunters")
                return department.Roles.Contains("CMUYautjaHunter");

            // Prefer explicit faction field if present on the department prototype
            if (!string.IsNullOrEmpty(department.Faction))
            {
                var f = department.Faction.ToLowerInvariant();
                if (factionFilter == "govfor")
                    return f == "govfor";
                if (factionFilter == "opfor")
                    return f == "opfor";
                if (factionFilter == "humans" || factionFilter == "colonists")
                    return f == "humans" || f == "human" || f == "colonists" || f == "colonist" || f == "default" || f == "";

                return f == factionFilter;
            }

            // Fallback to heuristic matching on ID/name for older prototypes
            var id = department.ID.ToLowerInvariant();
            var name = department.Name.ToString().ToLowerInvariant();

            var isGov = id.Contains("govfor") || id.Contains("government") || id.Contains("gov") || name.Contains("govfor") || name.Contains("government") || name.Contains("gov");
            var isOp = id.Contains("opfor") || id.Contains("op") || name.Contains("opfor") || name.Contains("op");

            if (factionFilter == "govfor")
                return isGov;
            if (factionFilter == "opfor")
                return isOp;
            if (factionFilter == "humans" || factionFilter == "colonists")
                return !isGov && !isOp;

            return true;
        }

        private void RebuildUI()
        {
            _base.RemoveAllChildren();
            _jobButtons.Clear();
            _sections.Clear();

            if (!_gameTicker.DisallowedLateJoin && _gameTicker.StationNames.Count == 0)
                _sawmill.Warning("No stations exist, nothing to display in late-join GUI");

            foreach (var (id, name) in _gameTicker.StationNames)
            {
                var section = BuildStationSection(id, name);
                _sections.Add(section);
            }

            ApplyFilter();

            CrtLobbyTheme.Apply(_base);

            foreach (var section in _sections)
            {
                StyleBanner(section);

                foreach (var group in section.Groups)
                {
                    StyleRailEntry(group, group.Id == section.SelectedGroup);

                    foreach (var job in group.Jobs)
                        StyleJobRow(job);
                }
            }
        }

        private void StyleBanner(StationSection section)
        {
            section.BannerName.FontOverride = StyleNano.GetCrtFont(_resourceCache, BannerSize);
            section.BannerName.FontColorOverride = CrtTerminalPalette.TextBright;

            section.BannerCount.FontOverride = StyleNano.GetCrtFont(_resourceCache, NoteSize);
            section.BannerCount.FontColorOverride = CrtTerminalPalette.TextDim;

            section.NoneLabel.FontOverride = StyleNano.GetCrtChatFont(_resourceCache, RowSize);
            section.NoneLabel.FontColorOverride = CrtTerminalPalette.TextDim;
        }

        private StationSection BuildStationSection(NetEntity id, string name)
        {
            var railList = new BoxContainer
            {
                Orientation = LayoutOrientation.Vertical,
                HorizontalExpand = true,
            };

            var railScroll = new ScrollContainer
            {
                MinWidth = RailWidth,
                MinHeight = MinBandHeight,
                MaxHeight = MaxBandHeight,
                ReturnMeasure = true,
                VerticalExpand = true,
                HScrollEnabled = false,
                Children = { railList },
            };

            var railPanel = new PanelContainer
            {
                // Top, not Stretch: the rail and the pane are each sized to their own content below.
                VerticalAlignment = VAlignment.Top,
                PanelOverride = new StyleBoxFlat
                {
                    BackgroundColor = CrtTerminalPalette.Surface1,
                    BorderColor = CrtTerminalPalette.Line,
                    BorderThickness = new Thickness(0, 0, 1, 0),
                },
                Children = { railScroll },
            };

            var paneList = new BoxContainer
            {
                Orientation = LayoutOrientation.Vertical,
                HorizontalExpand = true,
            };

            var paneScroll = new ScrollContainer
            {
                MinHeight = MinBandHeight,
                MaxHeight = MaxBandHeight,
                ReturnMeasure = true,
                VerticalAlignment = VAlignment.Top,
                HorizontalExpand = true,
                VerticalExpand = true,
                HScrollEnabled = false,
                Children = { paneList },
            };

            var split = new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal,
                HorizontalExpand = true,
                SeparationOverride = 0,
                Visible = false,
                Children = { railPanel, paneScroll },
            };

            var noneLabel = new Label
            {
                Text = Loc.GetString("late-join-gui-no-departments-available"),
                Margin = new Thickness(10, 6, 10, 2),
                Visible = false,
            };

            var body = new BoxContainer
            {
                Orientation = LayoutOrientation.Vertical,
                HorizontalExpand = true,
                Children = { split, noneLabel },
            };

            var bannerName = new Label { HorizontalExpand = true, VerticalAlignment = VAlignment.Center };
            var bannerCount = new Label { VerticalAlignment = VAlignment.Center };
            var banner = FactionBanner(name, bannerName, bannerCount);

            _base.AddChild(banner);
            _base.AddChild(body);

            if (_configManager.GetCVar(CCVars.CrewManifestWithoutEntity))
            {
                var crewManifestButton = new Button
                {
                    Text = Loc.GetString("crew-manifest-button-label")
                };
                crewManifestButton.OnPressed += _ => _crewManifest.RequestCrewManifest(id);
                body.AddChild(crewManifestButton);
            }

            var section = new StationSection(bannerName, bannerCount, noneLabel);
            _jobButtons[id] = new Dictionary<string, List<JobButton>>();

            var departments = _prototypeManager.EnumerateCM<DepartmentPrototype>().ToArray();
            Array.Sort(departments, DepartmentUIComparer.Instance);

            var stationAvailable = _gameTicker.JobsAvailable[id];

            var totalOpen = 0;

            foreach (var department in departments)
            {
                if (!DepartmentMatchesFilter(department))
                    continue;

                var jobsAvailable = new List<JobPrototype>();
                foreach (var jobId in department.Roles)
                {
                    if (!JobMatchesFilter(_factionFilter, jobId))
                        continue;

                    if (!stationAvailable.ContainsKey(jobId))
                        continue;

                    jobsAvailable.Add(_prototypeManager.Index<JobPrototype>(jobId));
                }

                if (JobUIComparer.TryCreate(
                        _prototypeManager,
                        _gameTicker.JobWeightsByStation.GetValueOrDefault(id),
                        out var comparer))
                {
                    jobsAvailable.Sort(comparer);
                }

                if (jobsAvailable.Count == 0)
                    continue;

                var departmentName = Loc.GetString(department.Name);

                foreach (var group in RailGroupsFor(department, departmentName, jobsAvailable))
                    totalOpen += BuildGroup(department, departmentName, group);
            }

            if (section.Groups.Count > 0)
            {
                // Built last, once totalOpen is known, then moved to the top of the rail.
                var allGroup = new RailGroup(AllGroupId, string.Empty, new Control(), CrtTerminalPalette.Accent);
                var allEntry = BuildRailEntry(section, allGroup, Loc.GetString("late-join-gui-all-roles"), totalOpen);
                railList.AddChild(allEntry);
                allEntry.SetPositionFirst();
                section.Groups.Insert(0, allGroup);

                section.SelectedGroup = AllGroupId;
                split.Visible = true;
                bannerCount.Text = Loc.GetString("late-join-gui-station-open-count", ("count", totalOpen));
            }
            else
            {
                noneLabel.Visible = true;
                bannerCount.Text = string.Empty;
            }

            return section;

            int BuildGroup(DepartmentPrototype dept, string deptName, (string Id, string Name, List<JobPrototype> Jobs) group)
            {
                var category = new BoxContainer
                {
                    Orientation = LayoutOrientation.Vertical,
                    HorizontalExpand = true,
                    Name = group.Id,
                    Visible = false,
                    ToolTip = Loc.GetString("late-join-gui-jobs-amount-in-department-tooltip",
                        ("departmentName", deptName)),
                };
                paneList.AddChild(category);

                var railGroup = new RailGroup(group.Id, deptName, category, dept.Color);
                section.Groups.Add(railGroup);

                var header = new Label
                {
                    Text = group.Name,
                    Visible = false,
                    FontOverride = StyleNano.GetCrtFont(_resourceCache, RailSize),
                    FontColorOverride = dept.Color,
                    Margin = new Thickness(12, 8, 12, 2),
                };
                category.AddChild(header);
                railGroup.Header = header;

                var openCount = 0;

                foreach (var prototype in group.Jobs)
                {
                    var value = stationAvailable[prototype.ID];

                    var jobLabel = new Label { HorizontalExpand = true, ClipText = true, VerticalAlignment = VAlignment.Center };
                    var countLabel = new Label
                    {
                        Align = Label.AlignMode.Right,
                        VerticalAlignment = VAlignment.Center,
                        MinWidth = 52,
                    };

                    var jobButton = new JobButton(jobLabel, countLabel, prototype.ID, prototype.LocalizedName, value);

                    var jobSelector = new BoxContainer
                    {
                        Orientation = LayoutOrientation.Horizontal,
                        HorizontalExpand = true,
                        SeparationOverride = 9,
                    };

                    var icon = new TextureRect { TextureScale = new Vector2(2, 2), VerticalAlignment = VAlignment.Center };
                    var jobIcon = _prototypeManager.Index(prototype.Icon);
                    icon.Texture = _sprites.Frame0(jobIcon.Icon);
                    jobSelector.AddChild(icon);
                    jobSelector.AddChild(jobLabel);
                    jobSelector.AddChild(countLabel);
                    jobButton.AddChild(jobSelector);
                    category.AddChild(jobButton);
                    railGroup.Jobs.Add(jobButton);

                    jobButton.OnPressed += _ => SelectedId.Invoke((id, jobButton.JobId));

                    FormattedMessage? reason = null;
                    var locked = !_jobRequirements.IsAllowed(prototype,
                        (HumanoidCharacterProfile?)_preferencesManager.Preferences?.SelectedCharacter, out reason);

                    if (locked)
                    {
                        jobButton.SetState(JobButton.State.Locked);

                        if (reason is { IsEmpty: false })
                        {
                            var tooltip = new Tooltip();
                            tooltip.SetMessage(reason);
                            jobButton.TooltipSupplier = _ => tooltip;
                        }
                    }
                    else
                    {
                        jobButton.SetState(value == 0 ? JobButton.State.Full : JobButton.State.Open);
                        if (value != 0)
                            openCount++;
                    }

                    if (!_jobButtons[id].ContainsKey(prototype.ID))
                        _jobButtons[id][prototype.ID] = new List<JobButton>();

                    _jobButtons[id][prototype.ID].Add(jobButton);
                }

                railList.AddChild(BuildRailEntry(section, railGroup, group.Name, openCount));

                return openCount;
            }
        }

        private static List<(string Id, string Name, List<JobPrototype> Jobs)> RailGroupsFor(
            DepartmentPrototype department,
            string departmentName,
            List<JobPrototype> jobs)
        {
            var whole = new List<(string, string, List<JobPrototype>)> { (department.ID, departmentName, jobs) };

            if (department.Faction?.ToLowerInvariant() is not ("govfor" or "opfor"))
                return whole;

            // OrderBy is stable, so this only clusters the segments; JobUIComparer still orders the rows.
            var segments = new List<(string Id, string Name, List<JobPrototype> Jobs)>();
            foreach (var job in jobs.OrderBy(j => HumanoidProfileEditor.GetJobSortGroup(department, j)))
            {
                var (key, title) = HumanoidProfileEditor.GetMilitaryJobSegment(job);
                var segmentId = $"{department.ID}:{key}";

                if (segments.Count == 0 || segments[^1].Id != segmentId)
                    segments.Add((segmentId, title, new List<JobPrototype>()));

                segments[^1].Jobs.Add(job);
            }

            return segments.Count > 1 ? segments : whole;
        }

        private static Control FactionBanner(string name, Label nameLabel, Label countLabel)
        {
            nameLabel.Text = name;

            var row = new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal,
                HorizontalExpand = true,
                Children = { nameLabel, countLabel },
            };

            return new PanelContainer
            {
                HorizontalExpand = true,
                PanelOverride = new StyleBoxFlat
                {
                    BackgroundColor = CrtTerminalPalette.Surface1,
                    BorderColor = CrtTerminalPalette.Line,
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    ContentMarginLeftOverride = 10,
                    ContentMarginRightOverride = 10,
                    ContentMarginTopOverride = 7,
                    ContentMarginBottomOverride = 6,
                },
                Children = { row },
            };
        }

        private static Control FilterBar(Control filter) => new PanelContainer
        {
            HorizontalExpand = true,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = CrtTerminalPalette.Surface1,
                BorderColor = CrtTerminalPalette.Line,
                BorderThickness = new Thickness(0, 0, 0, 1),
                ContentMarginLeftOverride = 10,
                ContentMarginRightOverride = 10,
                ContentMarginTopOverride = 7,
                ContentMarginBottomOverride = 6,
            },
            Children = { filter },
        };

        private Control BuildRailEntry(StationSection section, RailGroup group, string name, int openCount)
        {
            var box = new StyleBoxFlat
            {
                BackgroundColor = Color.Transparent,
                BorderColor = CrtTerminalPalette.Line,
                BorderThickness = new Thickness(0, 0, 0, 1),
                ContentMarginLeftOverride = 0,
                ContentMarginRightOverride = 12,
                ContentMarginTopOverride = 9,
                ContentMarginBottomOverride = 8,
            };

            var marker = new PanelContainer
            {
                MinWidth = 2,
                Visible = false,
                PanelOverride = new StyleBoxFlat { BackgroundColor = group.AccentColor },
            };

            var nameLabel = new Label
            {
                Text = name,
                ClipText = true,
                VerticalAlignment = VAlignment.Center,
                HorizontalExpand = true,
                Margin = new Thickness(8, 0, 0, 0),
            };

            var countLabel = new Label
            {
                Text = openCount.ToString(),
                VerticalAlignment = VAlignment.Center,
                Margin = new Thickness(10, 0, 0, 0),
            };

            var button = new ContainerButton
            {
                HorizontalExpand = true,
                StyleBoxOverride = box,
                Children =
                {
                    new BoxContainer
                    {
                        Orientation = LayoutOrientation.Horizontal,
                        SeparationOverride = 0,
                        HorizontalExpand = true,
                        Children = { marker, nameLabel, countLabel },
                    },
                },
            };

            group.RailBox = box;
            group.RailMarker = marker;
            group.RailName = nameLabel;
            group.RailCount = countLabel;

            button.OnPressed += _ => SelectGroup(section, group.Id);
            button.OnMouseEntered += _ =>
            {
                if (!marker.Visible)
                    box.BackgroundColor = CrtTerminalPalette.Surface2;
            };
            button.OnMouseExited += _ =>
            {
                if (!marker.Visible)
                    box.BackgroundColor = Color.Transparent;
            };

            return button;
        }

        private void SelectGroup(StationSection section, string groupId)
        {
            section.SelectedGroup = groupId;

            foreach (var group in section.Groups)
                StyleRailEntry(group, group.Id == groupId);

            ApplyFilter();
        }

        private void StyleRailEntry(RailGroup group, bool selected)
        {
            group.RailMarker.Visible = selected;
            group.RailBox.BackgroundColor = selected ? CrtTerminalPalette.Surface2 : Color.Transparent;

            group.RailName.FontOverride = StyleNano.GetCrtFont(_resourceCache, RailSize);
            group.RailName.FontColorOverride = selected ? CrtTerminalPalette.TextBright : CrtTerminalPalette.TextDim;

            group.RailCount.FontOverride = StyleNano.GetCrtChatFont(_resourceCache, RowSize);
            group.RailCount.FontColorOverride = TextFaint;
        }

        private void StyleJobRow(JobButton button)
        {
            button.JobLabel.FontOverride = StyleNano.GetCrtChatFont(_resourceCache, RowSize);
            button.CountLabel.FontOverride = StyleNano.GetCrtChatFont(_resourceCache, RowSize);

            var box = new StyleBoxFlat
            {
                BackgroundColor = Color.Transparent,
                BorderColor = RowRule,
                BorderThickness = new Thickness(0, 0, 0, 1),
                ContentMarginLeftOverride = 12,
                ContentMarginRightOverride = 12,
                ContentMarginTopOverride = 7,
                ContentMarginBottomOverride = 7,
            };
            button.StyleBoxOverride = box;

            if (button.Disabled)
                return;

            button.OnMouseEntered += _ => box.BackgroundColor = CrtTerminalPalette.Surface2;
            button.OnMouseExited += _ => box.BackgroundColor = Color.Transparent;
        }

        private void ApplyFilter()
        {
            var text = _filter.Text.Trim();
            var searching = !string.IsNullOrEmpty(text);

            // A station with groups stays shown for its rail alone, even mid-search with no matches in it.
            foreach (var section in _sections)
            {
                var allSelected = section.SelectedGroup == AllGroupId;
                var showingMultiple = searching || allSelected;

                foreach (var group in section.Groups)
                {
                    var groupSelected = showingMultiple || group.Id == section.SelectedGroup;
                    var groupNameMatch = searching &&
                        (group.RailName.Text?.Contains(text, StringComparison.OrdinalIgnoreCase) == true ||
                            group.DepartmentName.Contains(text, StringComparison.OrdinalIgnoreCase));
                    var groupHasVisible = false;

                    foreach (var job in group.Jobs)
                    {
                        var textMatch = !searching || groupNameMatch ||
                            job.JobLocalisedName.Contains(text, StringComparison.OrdinalIgnoreCase);
                        var visible = groupSelected && textMatch;
                        job.Visible = visible;
                        groupHasVisible |= visible;
                    }

                    group.Container.Visible = groupHasVisible;

                    if (group.Header != null)
                        group.Header.Visible = showingMultiple && groupHasVisible;
                }
            }
        }

        private sealed class StationSection
        {
            public readonly Label BannerName;
            public readonly Label BannerCount;
            public readonly Label NoneLabel;
            public readonly List<RailGroup> Groups = new();
            public string? SelectedGroup;

            public StationSection(Label bannerName, Label bannerCount, Label noneLabel)
            {
                BannerName = bannerName;
                BannerCount = bannerCount;
                NoneLabel = noneLabel;
            }
        }

        /// <summary>
        ///     One rail entry and the job list behind it - a whole department, or one military segment
        ///     of one. See <see cref="RailGroupsFor"/>.
        /// </summary>
        private sealed class RailGroup
        {
            public readonly string Id;

            /// <summary>
            ///     Kept apart from the rail's own label, which for a segment says "Vehicle Crew" and
            ///     never names the department at all - <see cref="ApplyFilter"/> searches both.
            /// </summary>
            public readonly string DepartmentName;

            public readonly Control Container;
            public readonly Color AccentColor;
            public readonly List<JobButton> Jobs = new();

            /// <summary>
            ///     This group's name, printed inside its own pane category - null for a group whose rail
            ///     entry already names it unambiguously.
            /// </summary>
            public Label? Header;

            public StyleBoxFlat RailBox = default!;
            public Control RailMarker = default!;
            public Label RailName = default!;
            public Label RailCount = default!;

            public RailGroup(string id, string departmentName, Control container, Color accentColor)
            {
                Id = id;
                DepartmentName = departmentName;
                Container = container;
                AccentColor = accentColor;
            }
        }

        public static bool JobMatchesFilter(string? factionFilter, string jobId)
        {
            return factionFilter is not ("hunt" or "hunters") || jobId == "CMUYautjaHunter";
        }

        private void JobsAvailableUpdated(IReadOnlyDictionary<NetEntity, Dictionary<ProtoId<JobPrototype>, int?>> updatedJobs)
        {
            foreach (var stationEntries in updatedJobs)
            {
                if (_jobButtons.ContainsKey(stationEntries.Key))
                {
                    var jobsAvailable = stationEntries.Value;

                    var existingJobEntries = _jobButtons[stationEntries.Key];
                    foreach (var existingJobEntry in existingJobEntries)
                    {
                        if (jobsAvailable.ContainsKey(existingJobEntry.Key))
                        {
                            var updatedJobValue = jobsAvailable[existingJobEntry.Key];
                            foreach (var matchingJobButton in existingJobEntry.Value)
                            {
                                if (matchingJobButton.Amount != updatedJobValue)
                                {
                                    matchingJobButton.RefreshLabel(updatedJobValue);
                                    matchingJobButton.Disabled |= matchingJobButton.Amount == 0;
                                }
                            }
                        }
                    }
                }
            }
        }

        [Obsolete("Retained for CMUControlLifetime.Release cleanup.")]
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                _jobRequirements.Updated -= RebuildUI;
                _gameTicker.LobbyJobsAvailableUpdated -= JobsAvailableUpdated;
                _configManager.UnsubValueChanged(CCVars.CrtUiColor, OnCrtUiColorChanged);
                _configManager.UnsubValueChanged(CCVars.CrtUiEnabled, OnCrtUiEnabledChanged);
                _jobButtons.Clear();
                _sections.Clear();
            }
        }
    }

    sealed class JobButton : ContainerButton
    {
        /// <summary>
        ///     Why a row cannot be taken, or that it can. Open and <see cref="Full"/> swap as slots
        ///     come and go; <see cref="Locked"/> is decided once, when the row is built.
        /// </summary>
        public enum State
        {
            Open,
            Full,
            Locked,
        }

        public Label JobLabel { get; }
        public Label CountLabel { get; }
        public string JobId { get; }
        public string JobLocalisedName { get; }
        public int? Amount { get; private set; }

        private State _state = State.Open;
        private bool _initialised;

        public JobButton(Label jobLabel, Label countLabel, ProtoId<JobPrototype> jobId, string jobLocalisedName, int? amount)
        {
            JobLabel = jobLabel;
            CountLabel = countLabel;
            JobId = jobId;
            JobLocalisedName = jobLocalisedName;
            JobLabel.Text = JobLocalisedName;
            RefreshLabel(amount);
            _initialised = true;
        }

        /// <summary>
        ///     Both signals for a state are set here together - the text colour and what the count
        ///     column says - so neither can drift out of step with the other.
        /// </summary>
        public void SetState(State state)
        {
            _state = state;
            Disabled = state != State.Open;

            JobLabel.FontColorOverride = state == State.Open
                ? CrtTerminalPalette.Text
                : CrtTerminalPalette.TextDim;

            RefreshCount();
        }

        public void RefreshLabel(int? amount)
        {
            if (Amount == amount && _initialised)
                return;

            Amount = amount;

            // Slots can drain to zero while the window is open, so an Open row has to fall to Full itself.
            if (_initialised && _state != State.Locked)
                SetState(amount == 0 ? State.Full : State.Open);
            else
                RefreshCount();
        }

        private void RefreshCount()
        {
            switch (_state)
            {
                case State.Locked:
                    CountLabel.Text = Loc.GetString("late-join-gui-job-locked");
                    CountLabel.FontColorOverride = CrtTerminalPalette.TextDim;
                    break;

                case State.Full:
                    CountLabel.Text = Loc.GetString("late-join-gui-job-full");
                    CountLabel.FontColorOverride = CrtTerminalPalette.TextDim;
                    break;

                default:
                    CountLabel.Text = Amount is { } amount
                        ? Loc.GetString("late-join-gui-job-open-count", ("count", amount))
                        : Loc.GetString("late-join-gui-job-uncapped");
                    CountLabel.FontColorOverride = CrtTerminalPalette.Accent;
                    break;
            }
        }
    }
}
