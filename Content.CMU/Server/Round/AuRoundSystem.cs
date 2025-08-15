using Content.Server.GameTicking;
using Content.Server.Voting.Managers;
using Content.Shared.Voting;
using Robust.Shared.Prototypes;
using Robust.Shared.Configuration;
using System.Linq;
using Content.Server.GameTicking.Presets;
using Content.Server.Maps;
using Content.Server.Voting;
using Content.Shared._RMC14.Rules;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared.AU14;
using Content.Shared.AU14.util;
using Content.Shared.CCVar;
using Robust.Server.GameObjects;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Timing;

namespace Content.Server.AU14.Round
{
    /// <summary>
    /// Persistent system that manages the full sequence of votes (preset, planet, platoon, etc.)
    /// </summary>
    public sealed class AuRoundSystem : EntitySystem
    {
        [Dependency] private readonly IVoteManager _voteManager = default!;
        [Dependency] private readonly IConfigurationManager _cfg = default!;
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        [Dependency] private readonly IEntityManager _entityManager = default!;
        [ViewVariables]
        public string? SelectedPlanetMapName => SelectedPlanetMap?.Announcement;
        [ViewVariables]
        private RMCPlanetMapPrototypeComponent? SelectedPlanetMap { get; set; }

        private GamePresetPrototype? _selectedPreset;
        private RMCPlanetMapPrototypeComponent? _selectedPlanet;
        private bool _voteSequenceRunning;

        private string? _selectedGovforShip;
        private string? _selectedOpforShip;

        public override void Initialize()
        {

            base.Initialize();
            _voteSequenceRunning = false;
            _selectedPreset = null;
            _selectedPlanet = null;
            SelectedPlanetMap = null;


        }

        /// <summary>
        /// Starts the full vote sequence: preset, planet, then platoons.
        /// </summary>
        ///
        ///         // Each vote method takes a callback to call when finished
        private IVoteHandle? StartPresetVote(Action onFinished)
        {
            _voteManager.CreateStandardVote(null, StandardVoteType.Preset);
            foreach (var vote in _voteManager.ActiveVotes)
            {
                if (vote.Title == "Game Preset")
                {
                    vote.OnFinished += (_, __) =>
                    {
                        Logger.Debug("[PlatoonVoteManagerSystem] Preset vote finished.");
                        onFinished();
                    };
                    return vote;
                }
            }

            Logger.Debug("[PlatoonVoteManagerSystem] Preset vote finished (no active vote found).\n");
            onFinished();
            return null;
        }

        public void StartFullVoteSequence()
        {
            if (_voteSequenceRunning)
                return;
            _voteSequenceRunning = true;
            _selectedPreset = null;
            _selectedPlanet = null;

            StartPresetVote(() =>
            {
                // After preset vote timer, get selected preset and start planet vote
                Timer.Spawn(TimeSpan.FromSeconds(_cfg.GetCVar(CCVars.VoteTimerPreset)), () =>
                {
                    var ticker = _entityManager.EntitySysManager.GetEntitySystem<GameTicker>();
                    var presetId = ticker.Preset?.ID;
                    if (string.IsNullOrEmpty(presetId) || !_prototypeManager.TryIndex<GamePresetPrototype>(presetId, out var preset))
                    {
                        _voteSequenceRunning = false;
                        return;
                    }
                    _selectedPreset = preset;

                    // Get planet list from either pool or direct list
                    List<string>? planetIds = null;
                    // Prefer pool if set, fallback to supportedPlanets
                    if (!string.IsNullOrEmpty(_selectedPreset.PlanetPool) && _prototypeManager.TryIndex<GamePlanetPoolPrototype>(_selectedPreset.PlanetPool, out var poolProto))
                    {
                        planetIds = poolProto.Planets;
                    }
                    else if (_selectedPreset.SupportedPlanets != null && _selectedPreset.SupportedPlanets.Count > 0)
                    {
                        planetIds = _selectedPreset.SupportedPlanets;
                    }

                    if (planetIds == null || planetIds.Count == 0)
                    {
                        _voteSequenceRunning = false;
                        return;
                    }

                    // Build planet options from planetIds
                    var planetProtos = new List<RMCPlanetMapPrototypeComponent>();
                    foreach (var pid in planetIds)
                    {
                        if (_prototypeManager.TryIndex<EntityPrototype>(pid, out var proto) &&
                            proto.TryGetComponent(out RMCPlanetMapPrototypeComponent? planetComp, IoCManager.Resolve<IComponentFactory>()))
                        {
                            planetProtos.Add(planetComp);
                        }
                        else
                        {
                            Logger.Warning($"[AuRoundSystem] Could not find RMCPlanetMapPrototypeComponent for planet ID: {pid}");
                        }
                    }
                    if (planetProtos.Count == 0)
                    {
                        _voteSequenceRunning = false;
                        return;
                    }
                    var options = new List<(string text, object data)>();
                    foreach (var planet in planetProtos)
                    {
                        // Use VoteName if available, otherwise fallback to MapId
                        var displayName = string.IsNullOrWhiteSpace(planet.VoteName) ? planet.MapId : planet.VoteName;
                        options.Add((displayName, planet));
                    }

                    var vote = new VoteOptions
                    {
                        Title = "Select Planet",
                        Options = options,
                        Duration = TimeSpan.FromSeconds(30),
                    };
                    vote.SetInitiatorOrServer(null);
                    var handle = _voteManager.CreateVote(vote);

                    // Use OnFinished handler to set _selectedPlanet
                    handle.OnFinished += (_, args) =>
                    {
                        object? picked = null;
                        if (args.Winner != null)
                            picked = args.Winner;
                        else if (args.Winners is var winnersArray && winnersArray.Length > 0)
                            picked = winnersArray[0];
                        if (picked == null && options.Count > 0)
                            picked = options[0].data;
                        _selectedPlanet = picked as RMCPlanetMapPrototypeComponent;
                    };

                    Timer.Spawn(TimeSpan.FromSeconds(30), () =>
                    {
                        // Fallback: if _selectedPlanet wasn't set by handler, pick manually
                        if (_selectedPlanet == null && options.Count > 0)
                            _selectedPlanet = options[0].data as RMCPlanetMapPrototypeComponent;
                        StartPlatoonVotes();
                    });
                });
            });
        }

        private void StartPlatoonVotes()
        {
            if (_selectedPreset == null || _selectedPlanet == null)
            {
                _voteSequenceRunning = false;
                _selectedPreset = null;
                _selectedPlanet = null;
                return;
            }

            // The selected planet is an EntityPrototype, get the RMCPlanetMapPrototypeComponent for platoon info
            var planetProto = _selectedPlanet;
            if (planetProto == null)
            {
                _voteSequenceRunning = false;
                _selectedPreset = null;
                _selectedPlanet = null;
                return;
            }

            var govforPlatoons = _selectedPlanet.PlatoonsGovfor;
            var opforPlatoons = _selectedPlanet.PlatoonsOpfor;
            var duration = TimeSpan.FromSeconds(_cfg.GetCVar(CCVars.VotePlatoonDuration));
            var platoonSpawnRuleSystem = _entityManager.EntitySysManager.GetEntitySystem<PlatoonSpawnRuleSystem>();

            void StartShipVote(List<string> possibleShips, string title, Action<string> onShipSelected)
            {
                if (possibleShips.Count == 0)
                {
                    onShipSelected(string.Empty);
                    return;
                }

                var shipOptions = possibleShips.Select(id => (id, (object)id)).ToList();
                var voteopt = new VoteOptions
                {
                    Title = title,
                    Options = shipOptions,
                    Duration = duration
                };
                var handle = _voteManager.CreateVote(voteopt);
                handle.OnFinished += (_, args) =>
                {
                    string? winner = args.Winner as string;
                    if (winner == null && args.Winners is var arr && arr.Length > 0)
                        winner = arr[0] as string;
                    if (winner == null && shipOptions.Count > 0)
                        winner = shipOptions[0].id;
                    onShipSelected(winner ?? string.Empty);
                };
            }



            if (_selectedPreset.RequiresGovforVote && govforPlatoons.Count > 0)
            {
                var optionsplatoons = new List<(string text, object data)>();
                foreach (var platoonId in govforPlatoons)
                {
                    var platoon = _prototypeManager.Index<PlatoonPrototype>(platoonId);
                    optionsplatoons.Add((platoon.Name, platoon));
                }

                var voteopt = new VoteOptions
                {
                    Title = "Govfor Vote",
                    Options = optionsplatoons,
                    Duration = duration
                };
                voteopt.SetInitiatorOrServer(null);
                var handle = _voteManager.CreateVote(voteopt);
                handle.OnFinished += (_, args) =>
                {
                    if (args.Winner is PlatoonPrototype winnerId)
                    {
                        platoonSpawnRuleSystem.SelectedGovforPlatoon = winnerId;
                        // Ship vote for Govfor after a timer to avoid nesting
                        Timer.Spawn(TimeSpan.FromMilliseconds(100),
                            () =>
                            {
                                StartShipVote(winnerId.PossibleShips,
                                    "Govfor Ship Vote",
                                    shipId => _selectedGovforShip = shipId);
                            });
                    }
                };
            }

            if (_selectedPreset.RequiresOpforVote && opforPlatoons != null && opforPlatoons.Count > 0)
            {
                var optionsplatoons = new List<(string text, object data)>();
                foreach (var platoonId in opforPlatoons)
                {
                    var platoon = _prototypeManager.Index<PlatoonPrototype>(platoonId);
                    optionsplatoons.Add((platoon.Name, platoon));
                }

                var voteopt = new VoteOptions
                {
                    Title = "Opfor Vote",
                    Options = optionsplatoons,
                    Duration = duration
                };
                voteopt.SetInitiatorOrServer(null);
                var handle = _voteManager.CreateVote(voteopt);
                handle.OnFinished += (_, args) =>
                {
                    if (args.Winner is PlatoonPrototype winnerId)
                    {
                        platoonSpawnRuleSystem.SelectedOpforPlatoon = winnerId;
                        // Ship vote for Opfor after a timer to avoid nesting
                        Timer.Spawn(TimeSpan.FromMilliseconds(100),
                            () =>
                            {
                                StartShipVote(winnerId.PossibleShips,
                                    "Opfor Ship Vote",
                                    shipId => _selectedOpforShip = shipId);
                            });
                    }
                };
            }
        }

        public string? GetSelectedGovforShip()
        {
            return _selectedGovforShip;
        }

        public string? GetSelectedOpforShip()
        {
            return _selectedOpforShip;
        }

        public bool IsVoteSequenceRunning()
        {
            return _voteSequenceRunning;
        }

        public void StartVoteSequence(Action? onFinished = null)
        {
            _voteSequenceRunning = false;
            _selectedPreset = null;
            _selectedPlanet = null;
            SelectedPlanetMap = null;
            _selectedGovforShip = null;
            _selectedOpforShip = null;

            StartFullVoteSequence();
            onFinished?.Invoke();
        }

        public RMCPlanetMapPrototypeComponent? GetSelectedPlanet()
        {
            return _selectedPlanet;
        }

        // --- PLANET LOGIC: Load planet like cmdistress does after round starts ---
        public void LoadSelectedPlanetMap()
        {
            if (_selectedPlanet == null)
                return;

            var mapLoader = _entityManager.EntitySysManager.GetEntitySystem<MapLoaderSystem>();
            var mapSystem = _entityManager.EntitySysManager.GetEntitySystem<MapSystem>();
            var sawmill = Logger.GetSawmill("game");
            var compFactory = IoCManager.Resolve<IComponentFactory>();
            var serialization = IoCManager.Resolve<ISerializationManager>();

            // Try to load the selected planet's map
            if (!_prototypeManager.TryIndex<GameMapPrototype>(_selectedPlanet.MapId, out var mapProto))
            {
                sawmill.Error($"[AuRoundSystem] Failed to find GameMapPrototype for selected planet: {_selectedPlanet.MapId}");
                return;
            }
            if (!mapLoader.TryLoadMap(mapProto.MapPath, out var mapNullable, out var grids))
            {
                sawmill.Error($"[AuRoundSystem] Failed to load selected planet map: {mapProto.MapPath}");
                return;
            }

            var map = mapNullable.Value;
            mapSystem.InitializeMap((map, map));

            // Attach RMCPlanetComponent, TacticalMapComponent, etc. (if not already present)
            if (!_entityManager.HasComponent<RMCPlanetComponent>(map))
                _entityManager.AddComponent<RMCPlanetComponent>(map);
            if (!_entityManager.HasComponent<TacticalMapComponent>(map))
                _entityManager.AddComponent<TacticalMapComponent>(map);


        }
    }
}
