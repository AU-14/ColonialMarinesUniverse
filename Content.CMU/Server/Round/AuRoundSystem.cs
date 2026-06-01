using Content.Server.GameTicking;
using Content.Server.Preferences.Managers;
using Content.Server.Voting.Managers;
using Content.Shared.Voting;
using Robust.Shared.Prototypes;
using Robust.Shared.Configuration;
using System.Linq;
using Content.Server.GameTicking.Presets;
using Content.Server.Maps;
using Content.Server.AU14.Threats;
using Content.Server.Voting;
using Content.Shared._RMC14.Intel;
using Content.Shared._RMC14.Rules;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared.AU14;
using Content.Shared.AU14.Threats;
using Content.Shared.AU14.util;
using Content.Shared.CCVar;
using Content.Shared.Preferences;
using Content.Shared.Storage;
using Robust.Server.Player;
using Robust.Server.GameObjects;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Random;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Timing;
using Content.Shared._RMC14.Item;

namespace Content.Server.AU14.Round
{
    /// <summary>
    /// Persistent system that manages the full sequence of votes (preset, planet, platoon, etc.)
    /// </summary>
    public sealed partial class AuRoundSystem : EntitySystem
    {
        [Dependency] private IVoteManager _voteManager = default!;
        [Dependency] private IConfigurationManager _cfg = default!;
        [Dependency] private IPrototypeManager _prototypeManager = default!;
        [Dependency] private IEntityManager _entityManager = default!;
        [Dependency] private IPlayerManager _playerManager = default!;
        [Dependency] private IServerPreferencesManager _prefsManager = default!;
        [Dependency] private IRobustRandom _random = default!;
        [Dependency] private ItemCamouflageSystem _camo = default!;

        [ViewVariables]
        public string? SelectedPlanetMapName => SelectedPlanetMap?.Announcement;

        /// <summary>The active planet's prototype component, used by other systems to read per-planet settings.</summary>
        public RMCPlanetMapPrototypeComponent? ActivePlanet => SelectedPlanetMap;

        [ViewVariables]
        private RMCPlanetMapPrototypeComponent? SelectedPlanetMap { get; set; }

        private GamePresetPrototype? _selectedPreset;
        public GamePresetPrototype? SelectedPreset => _selectedPreset;
        private RMCPlanetMapPrototypeComponent? _selectedPlanet;
        private bool _voteSequenceRunning;
        public ThreatPrototype _selectedthreat = null!;
        private string? _selectedGovforShip;
        private string? _selectedOpforShip;
        public void SetOpforShip(string shipId) => _selectedOpforShip = shipId;
        public void SetGovforShip(string shipId) => _selectedGovforShip = shipId;
        public void SetSelectedThreat(ThreatPrototype? threat) => _selectedthreat = threat ?? null!;

        public bool UsesPostRoundstartThreatVote()
        {
            var presetId = _selectedPreset?.ID;
            return IsPostRoundstartThreatVotePreset(presetId);
        }

        public static bool IsPostRoundstartThreatVotePreset(string? presetId)
        {
            return presetId != null &&
                   (presetId.Equals("DistressSignal", StringComparison.OrdinalIgnoreCase) ||
                    presetId.Equals("ColonyFall", StringComparison.OrdinalIgnoreCase));
        }

        private List<AuThirdPartyPrototype> _selectedThirdParties = new();
        public IReadOnlyList<AuThirdPartyPrototype> SelectedThirdParties => _selectedThirdParties;

        public override void Initialize()
        {

            base.Initialize();
            _voteSequenceRunning = false;
            _selectedPreset = null;
            _selectedPlanet = null;
            SelectedPlanetMap = null;
            _selectedThirdParties.Clear(); // Reset third parties at round init

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
                        Logger.GetSawmill("content").Debug("[PlatoonVoteManagerSystem] Preset vote finished.");
                        onFinished();
                    };
                    return vote;
                }
            }

            Logger.GetSawmill("content").Debug("[PlatoonVoteManagerSystem] Preset vote finished (no active vote found).\n");
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
            _selectedthreat = null!;
            _selectedThirdParties.Clear();
            StartPresetVote(() =>
            {
                // After preset vote timer, get selected preset and start planet vote
                Timer.Spawn(TimeSpan.FromSeconds(_cfg.GetCVar(CCVars.VoteTimerPreset)),
                    () =>
                    {
                        var ticker = _entityManager.EntitySysManager.GetEntitySystem<GameTicker>();
                        var presetId = ticker.Preset?.ID;
                        if (string.IsNullOrEmpty(presetId) ||
                            !_prototypeManager.TryIndex<GamePresetPrototype>(presetId, out var preset))
                        {
                            _voteSequenceRunning = false;
                            return;
                        }

                        _selectedPreset = preset;

                        // Get planet list from either pool or direct list
                        List<string>? planetIds = null;
                        // Prefer pool if set, fallback to supportedPlanets
                        if (!string.IsNullOrEmpty(_selectedPreset.PlanetPool) &&
                            _prototypeManager.TryIndex<GamePlanetPoolPrototype>(_selectedPreset.PlanetPool,
                                out var poolProto))
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
                                proto.TryGetComponent(out RMCPlanetMapPrototypeComponent? planetComp,
                                    IoCManager.Resolve<IComponentFactory>()))
                            {
                                planetProtos.Add(planetComp);
                            }
                            else
                            {
                                Logger.GetSawmill("content").Warning(
                                    $"[AuRoundSystem] Could not find RMCPlanetMapPrototypeComponent for planet ID: {pid}");
                            }
                        }

                        // Filter planets by their MinPlayers/MaxPlayers so planets intended for
                        // specific player counts cannot be voted for when out of range.
                        var playerCount = _playerManager.PlayerCount;
                        planetProtos.RemoveAll(p =>
                            // If MinPlayers is set (>0) and current players are fewer, exclude.
                            (p.MinPlayers > 0 && playerCount < p.MinPlayers) ||
                            // If MaxPlayers is set (>0) and current players exceed it, exclude.
                            (p.MaxPlayers > 0 && playerCount > p.MaxPlayers)
                        );

                        if (planetProtos.Count == 0)
                        {
                            _voteSequenceRunning = false;
                            return;
                        }

                        var options = new List<(string text, object data)>();
                        foreach (var planet in planetProtos)
                        {
                            // Use VoteName if available, otherwise fallback to MapId
                            var displayName = string.IsNullOrWhiteSpace(planet.VoteName)
                                ? planet.MapId
                                : planet.VoteName;
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
                            if (picked != null)
                                args.ResolveWinner(picked);
                            _selectedPlanet = picked as RMCPlanetMapPrototypeComponent;
                        };

                        Timer.Spawn(TimeSpan.FromSeconds(32),
                            () =>
                            {
                                // Fallback: if _selectedPlanet wasn't set by handler, pick manually
                                if (_selectedPlanet == null && options.Count > 0)
                                    _selectedPlanet = options[0].data as RMCPlanetMapPrototypeComponent;
                                SetCamoType();
                                StartPlatoonVotes();
                            });
                    });
            });
        }

        public bool IsThirdPartyAllowedForCurrentContext(AuThirdPartyPrototype proto)
        {
            if (_selectedPreset == null)
                return true;

            var platoonSpawnRuleSystem = _entityManager.EntitySysManager.GetEntitySystem<PlatoonSpawnRuleSystem>();
            return IsThirdPartyAllowed(
                proto,
                _selectedPreset.ID,
                _selectedthreat?.ID,
                platoonSpawnRuleSystem.SelectedGovforPlatoon?.ID,
                platoonSpawnRuleSystem.SelectedOpforPlatoon?.ID,
                _playerManager.PlayerCount);
        }

        private static bool IsThirdPartyAllowed(
            AuThirdPartyPrototype proto,
            string currentGamemode,
            string? currentThreat,
            string? govforPlatoon,
            string? opforPlatoon,
            int playerCount)
        {
            if (ContainsIgnoreCase(proto.BlacklistedGamemodes, currentGamemode))
                return false;

            if (proto.whitelistedgamemodes.Count > 0 &&
                !ContainsIgnoreCase(proto.whitelistedgamemodes, currentGamemode))
                return false;

            if (proto.MaxPlayers < playerCount || proto.MinPlayers > playerCount)
                return false;

            if (currentThreat != null && ContainsIgnoreCase(proto.BlacklistedThreats, currentThreat))
                return false;

            if (proto.WhitelistedThreats.Count > 0 &&
                (currentThreat == null || !ContainsIgnoreCase(proto.WhitelistedThreats, currentThreat)))
                return false;

            if (govforPlatoon != null && ContainsIgnoreCase(proto.BlacklistedPlatoons, govforPlatoon))
                return false;

            if (opforPlatoon != null && ContainsIgnoreCase(proto.BlacklistedPlatoons, opforPlatoon))
                return false;

            if (proto.WhitelistedPlatoons.Any() &&
                ((govforPlatoon != null && !ContainsIgnoreCase(proto.WhitelistedPlatoons, govforPlatoon)) ||
                 (opforPlatoon != null && !ContainsIgnoreCase(proto.WhitelistedPlatoons, opforPlatoon))))
                return false;

            return true;
        }

        private static bool ContainsIgnoreCase(IEnumerable<string> values, string value)
        {
            return values.Any(candidate => candidate.Equals(value, StringComparison.OrdinalIgnoreCase));
        }

        private void PreselectThirdParties()
        {
            _selectedThirdParties.Clear();
            if (_selectedPreset == null || _selectedPlanet == null)
                return;
            if (_selectedthreat == null)
                return;

            var allThirdParties = new List<AuThirdPartyPrototype>();
            if (_selectedPlanet.ThirdParties.Count > 0)
            {
                foreach (var protoId in _selectedPlanet.ThirdParties)
                {
                    if (_prototypeManager.TryIndex(protoId, out AuThirdPartyPrototype? proto))
                        allThirdParties.Add(proto);
                    else
                        Logger.GetSawmill("content").Warning($"[AuRoundSystem] Could not find AuThirdPartyPrototype for ID: {protoId}");
                }
            }
            else
            {
                return;
            }

            var filtered = allThirdParties
                .Where(IsThirdPartyAllowedForCurrentContext)
                .ToList();
            if (filtered.Count == 0)
                return;
            var weighted = new List<AuThirdPartyPrototype>();
            foreach (var proto in filtered)
            {
                int weight = Math.Max(1, proto.weight);
                for (int i = 0; i < weight; i++)
                    weighted.Add(proto);
            }
            if (weighted.Count == 0)
                return;
            int maxThirdParties = Math.Max(0, _selectedthreat.MaxThirdParties);
            if (maxThirdParties <= 0)
                return;
            var selectedSet = new HashSet<AuThirdPartyPrototype>();
            // Select up to maxThirdParties unique third parties
            while (selectedSet.Count < maxThirdParties && weighted.Count > 0)
            {
                var pick = _random.Pick(weighted);
                if (!selectedSet.Contains(pick))
                {
                    selectedSet.Add(pick);
                }
                // Remove all instances of this pick from weighted to avoid duplicates
                weighted.RemoveAll(x => x == pick);
            }
            // Insert roundstart third parties at the start, others at the end
            foreach (var party in selectedSet)
            {
                if (party.RoundStart)
                    _selectedThirdParties.Insert(0, party);
                else
                    _selectedThirdParties.Add(party);
            }
        }

        public void PreselectThirdPartiesForSelectedThreat()
        {
            PreselectThirdParties();
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

            var presetProto = _selectedPreset;
            var planetProto = _selectedPlanet;

            Timer.Spawn(TimeSpan.FromMilliseconds(100),
                () =>
                {

                    chooseThreat(planetProto);
                });
            Timer.Spawn(TimeSpan.FromMilliseconds(200),
                () =>
                {
                    PreselectThirdParties();
                });

                    var govforPlatoons = planetProto.PlatoonsGovfor;
                    var opforPlatoons = planetProto.PlatoonsOpfor;
                    var duration = TimeSpan.FromSeconds(_cfg.GetCVar(CCVars.VotePlatoonDuration));
                    var platoonSpawnRuleSystem =
                        _entityManager.EntitySysManager.GetEntitySystem<PlatoonSpawnRuleSystem>();

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
                        voteopt.SetInitiatorOrServer(null);

                        var handle = _voteManager.CreateVote(voteopt);
                        handle.OnFinished += (_, args) =>
                        {
                            string? winner = args.Winner as string;
                            if (winner == null && args.Winners is var arr && arr.Length > 0)
                                winner = arr[0] as string;
                            if (winner == null && shipOptions.Count > 0)
                                winner = shipOptions[0].id;
                            if (winner != null)
                                args.ResolveWinner(winner);
                            onShipSelected(winner ?? string.Empty);
                        };
                    }



                    if (presetProto.RequiresGovforVote && govforPlatoons.Count > 0)
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
                            var winnerId = args.Winner as PlatoonPrototype;
                            if (winnerId == null && args.Winners is var winnersArray && winnersArray.Length > 0)
                                winnerId = winnersArray[0] as PlatoonPrototype;

                            if (winnerId != null)
                            {
                                args.ResolveWinner(winnerId);
                                platoonSpawnRuleSystem.SelectedGovforPlatoon = winnerId;

                                // If this platoon declares a tech-tree, apply it immediately to the IntelSystem as a runtime override.
                                var intelSys = _entityManager.EntitySysManager.GetEntitySystem<Content.Shared._RMC14.Intel.IntelSystem>();
                                if (!string.IsNullOrEmpty(winnerId.TechTree))
                                {
                                    intelSys.SetTeamTechTreeOverride(Team.GovFor, winnerId.TechTree);
                                }

                                // Only start ship vote if planet allows govfor in ship
                                if (planetProto.GovforInShip)
                                {
                                    Timer.Spawn(TimeSpan.FromMilliseconds(100),
                                        () =>
                                        {

                                            StartShipVote(winnerId.PossibleShips,
                                                "Govfor Ship Vote",
                                                shipId => _selectedGovforShip = shipId);
                                        });
                                }
                            }
                        };
                    }

                    if (presetProto.RequiresOpforVote && opforPlatoons.Count > 0)
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
                            var winnerId = args.Winner as PlatoonPrototype;
                            if (winnerId == null && args.Winners is var winnersArray && winnersArray.Length > 0)
                                winnerId = winnersArray[0] as PlatoonPrototype;

                            if (winnerId != null)
                            {
                                args.ResolveWinner(winnerId);
                                platoonSpawnRuleSystem.SelectedOpforPlatoon = winnerId;

                                // If this platoon declares a tech-tree, apply it immediately to the IntelSystem as a runtime override.
                                var intelSys = _entityManager.EntitySysManager.GetEntitySystem<Content.Shared._RMC14.Intel.IntelSystem>();
                                if (intelSys != null && !string.IsNullOrEmpty(winnerId.TechTree))
                                {
                                    intelSys.SetTeamTechTreeOverride(Team.OpFor, winnerId.TechTree);
                                }

                                // Only start ship vote if planet allows opfor in ship
                                if (planetProto.OpforInShip)
                                {
                                    Timer.Spawn(TimeSpan.FromMilliseconds(100),
                                        () =>
                                        {
                                            StartShipVote(winnerId.PossibleShips,
                                                "Opfor Ship Vote",
                                                shipId => _selectedOpforShip = shipId);
                                        });
                                }
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
                sawmill.Error(
                    $"[AuRoundSystem] Failed to find GameMapPrototype for selected planet: {_selectedPlanet.MapId}");
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

        public void SetOpfor(string opfor)
        {
            _selectedOpforShip = opfor;
        }

        public void SetGovfor(string govfor)
        {
            _selectedGovforShip = govfor;
        }

        public bool SetPlanet(string planetId)
        {
            if (_prototypeManager.TryIndex<EntityPrototype>(planetId, out var proto) &&
                proto.TryGetComponent(out RMCPlanetMapPrototypeComponent? planetComp,
                    IoCManager.Resolve<IComponentFactory>()))
            {
                _selectedPlanet = planetComp;
                return true;
            }

            return false;
        }

        public void SetCamoType(CamouflageType? ct = null)
        {
            if (ct != null)
            {
                _camo.CurrentMapCamouflage = ct.Value;
                return;
            }

            if (_selectedPlanet != null)
                _camo.CurrentMapCamouflage = _selectedPlanet.Camouflage;
        }

        public void chooseThreat(RMCPlanetMapPrototypeComponent? planet)
        {
            if (_cfg.GetCVar(CCVars.GameDummyTicker))
                return;

            var noThreatPresets = new HashSet<string> { "ForceOnForce", "Insurgency" };
            if (_selectedPreset != null && noThreatPresets.Any(s => s.Equals(_selectedPreset.ID, StringComparison.OrdinalIgnoreCase)))
            {
                _selectedthreat = null!;
                Logger.GetSawmill("content").Debug($"[AuRoundSystem] Skipping threat selection for preset: {_selectedPreset.ID}");
                return;
            }

            var presetId = _selectedPreset?.ID;
            if (IsPostRoundstartThreatVotePreset(presetId))
            {
                _selectedthreat = null!;
                Logger.GetSawmill("content").Debug($"[AuRoundSystem] Deferring threat selection for post-roundstart vote preset: {presetId}");
                return;
            }

            var allowedPresets = new[] { "Prometheus", "ColonyFall", "DistressSignal", "Jailbreak" };
            if (string.IsNullOrEmpty(presetId) ||
                !allowedPresets.Any(p => p.Equals(presetId, StringComparison.InvariantCultureIgnoreCase)) ||
                planet is not { AllowedThreats.Count: >= 1 })
            {
                return;
            }

            var platoonSpawnRuleSystem = _entityManager.EntitySysManager.GetEntitySystem<PlatoonSpawnRuleSystem>();
            var playerCount = _playerManager.PlayerCount;
            var govforId = platoonSpawnRuleSystem?.SelectedGovforPlatoon?.ID;
            var opforId = platoonSpawnRuleSystem?.SelectedOpforPlatoon?.ID;
            var threats = new List<ProtoId<ThreatPrototype>>();

            foreach (var threatId in planet.AllowedThreats)
            {
                if (!_prototypeManager.TryIndex(threatId, out ThreatPrototype? threatProto) ||
                    !ThreatVoteSelection.IsThreatAllowed(threatProto, presetId, govforId, opforId, playerCount))
                {
                    continue;
                }

                threats.Add(threatId);
            }

            if (threats.Count == 0)
            {
                Logger.GetSawmill("content").Debug(
                    $"[AuRoundSystem] No valid threats found for planet {planet.MapId} with preset {presetId}, govfor {govforId}, opfor {opforId}");
                return;
            }

            var preferredThreats = GetThreatPreferenceWeights(threats);
            var weightedThreats = new List<ProtoId<ThreatPrototype>>();

            foreach (var threatId in threats)
            {
                if (!_prototypeManager.TryIndex(threatId, out ThreatPrototype? threatProto))
                    continue;

                var weight = Math.Max(1, threatProto.ThreatWeight);
                if (preferredThreats.TryGetValue(threatProto.ID, out var preferenceCount))
                    weight += preferenceCount * Math.Max(3, threatProto.ThreatWeight);

                for (var i = 0; i < weight; i++)
                {
                    weightedThreats.Add(threatId);
                }
            }

            if (weightedThreats.Count == 0)
                return;

            var threatSelectedId = _random.Pick(weightedThreats);
            Logger.GetSawmill("content").Debug($"[AuRoundSystem] Selected threat: {threatSelectedId}");
            _selectedthreat = _prototypeManager.TryIndex(threatSelectedId, out ThreatPrototype? threatSelected)
                ? threatSelected
                : null!;

        }

        public void StartThreatWinConditions(ThreatPrototype threat)
        {
            if (threat.WinConditions.Count == 0)
                return;

            var ticker = _entityManager.EntitySysManager.GetEntitySystem<GameTicker>();
            foreach (var ruleId in threat.WinConditions)
            {
                ticker.StartGameRule(ruleId);
                Logger.GetSawmill("content").Debug($"[AuRoundSystem] Started wincondition rule from threat: {ruleId}");
            }
        }

        private Dictionary<string, int> GetThreatPreferenceWeights(IEnumerable<ProtoId<ThreatPrototype>> allowedThreats)
        {
            var allowed = allowedThreats.Select(id => id.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var weights = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var session in _playerManager.Sessions)
            {
                if (!_prefsManager.TryGetCachedPreferences(session.UserId, out var preferences) ||
                    preferences.SelectedCharacter is not HumanoidCharacterProfile profile)
                {
                    continue;
                }

                var threatPreferences = profile.GetThreatPreferencesForGamemode(_selectedPreset?.ID);
                if (threatPreferences.Count == 0)
                    continue;

                foreach (var preference in threatPreferences)
                {
                    if (!allowed.Contains(preference.Id))
                        continue;

                    weights.TryGetValue(preference.Id, out var current);
                    weights[preference.Id] = current + 1;
                }
            }

            return weights;
        }
    }
}
