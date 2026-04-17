using Content.Server.GameTicking;
using Content.Server.Voting.Managers;
using Content.Shared.Voting;
using Robust.Shared.Prototypes;
using Robust.Shared.Configuration;
using System.Linq;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking.Presets;
using Content.Server.Maps;
using Content.Server.Voting;
using Content.Shared._RMC14.Intel;
using Content.Shared._RMC14.Rules;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared.AU14;
using Content.Shared.AU14.Threats;
using Content.Shared.AU14.util;
using Content.Shared.CCVar;
using Content.Shared.Storage;
using Robust.Server.Player;
using Robust.Server.GameObjects;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Random;
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
        [Dependency] private readonly IPlayerManager _playerManager = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly IChatManager _chatManager = default!;

        [ViewVariables]
        public string? SelectedPlanetMapName => SelectedPlanetMap?.Announcement;

        [ViewVariables]
        private RMCPlanetMapPrototypeComponent? SelectedPlanetMap { get; set; }

        private GamePresetPrototype? _selectedPreset;
        private RMCPlanetMapPrototypeComponent? _selectedPlanet;
        private bool _voteSequenceRunning;
        public ThreatPrototype _selectedthreat = null!;
        private string? _selectedGovforShip;
        private string? _selectedOpforShip;

        private List<AuThirdPartyPrototype> _selectedThirdParties = new();
        public IReadOnlyList<AuThirdPartyPrototype> SelectedThirdParties => _selectedThirdParties;

        // Default max third parties for rounds with no threat (e.g., ForceOnForce)
        private const int DefaultMaxThirdPartiesNoThreat = 4;

        private const int DistressSignalBonus = 5;

        // Carryover voting dictionaries
        private readonly Dictionary<string, int> _carryoverPresetVotes = new();
        private readonly Dictionary<string, int> _carryoverPlanetVotes = new();
        private readonly Dictionary<string, int> _carryoverGovforVotes = new();
        private readonly Dictionary<string, int> _carryoverOpforVotes = new();

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
            // Build preset options ourselves so we can apply carryover voting + DistressSignal bonus
            var presets = new List<(string id, string title)>();
            foreach (var preset in _prototypeManager.EnumeratePrototypes<GamePresetPrototype>())
            {
                if (!preset.ShowInVote)
                    continue;
                if (_playerManager.PlayerCount < (preset.MinPlayers ?? int.MinValue))
                    continue;
                if (_playerManager.PlayerCount > (preset.MaxPlayers ?? int.MaxValue))
                    continue;
                presets.Add((preset.ID, preset.ModeTitle));
            }

            if (presets.Count == 0)
            {
                Logger.Debug("[AuRoundSystem] No presets available for vote.");
                onFinished();
                return null;
            }

            var options = new List<(string text, object data)>();
            foreach (var (id, title) in presets)
            {
                var displayName = Loc.GetString(title);
                var carryover = _carryoverPresetVotes.GetValueOrDefault(id);
                // DistressSignal gets a permanent +5
                if (id.Equals("DistressSignal", StringComparison.OrdinalIgnoreCase))
                    carryover += DistressSignalBonus;
                if (carryover > 0)
                    displayName = $"{displayName} [+{carryover}]";
                options.Add((displayName, id));
            }

            var voteOpts = new VoteOptions
            {
                Title = "Game Preset",
                Options = options,
                Duration = _playerManager.PlayerCount == 1
                    ? TimeSpan.FromSeconds(_cfg.GetCVar(CCVars.VoteTimerAlone))
                    : TimeSpan.FromSeconds(_cfg.GetCVar(CCVars.VoteTimerPreset)),
            };
            voteOpts.SetInitiatorOrServer(null);

            var handle = _voteManager.CreateVote(voteOpts);
            handle.OnFinished += (_, args) =>
            {
                // Calculate adjusted votes with carryover + DistressSignal bonus
                var adjustedResults = new List<(string id, string title, int newVotes, int totalVotes)>();
                for (int i = 0; i < presets.Count && i < args.Votes.Count; i++)
                {
                    var (id, title) = presets[i];
                    var newVotes = args.Votes[i];
                    var carryover = _carryoverPresetVotes.GetValueOrDefault(id);
                    // DistressSignal permanent +5
                    var bonus = id.Equals("DistressSignal", StringComparison.OrdinalIgnoreCase) ? DistressSignalBonus : 0;
                    adjustedResults.Add((id, title, newVotes, newVotes + carryover + bonus));
                }

                string picked;
                if (adjustedResults.Count > 0)
                {
                    var maxVotes = adjustedResults.Max(v => v.totalVotes);
                    var winners = adjustedResults.Where(v => v.totalVotes == maxVotes).ToList();
                    var winner = winners.Count > 1 ? _random.Pick(winners) : winners[0];
                    picked = winner.id;

                    _chatManager.DispatchServerAnnouncement($"Gamemode vote winner: {Loc.GetString(winner.title)}");

                    // Update carryover: accumulate for losers, reset for winner
                    foreach (var r in adjustedResults)
                    {
                        if (r.id == picked)
                            _carryoverPresetVotes[r.id] = 0;
                        else
                            _carryoverPresetVotes[r.id] = _carryoverPresetVotes.GetValueOrDefault(r.id) + r.newVotes;
                    }
                }
                else
                {
                    picked = args.Winner as string ?? presets[0].id;
                }

                // Apply the selected preset
                var ticker = _entityManager.EntitySysManager.GetEntitySystem<GameTicker>();
                ticker.SetGamePreset(picked);

                Logger.Debug($"[AuRoundSystem] Preset vote finished. Winner: {picked}");
                onFinished();
            };
            return handle;
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
                                Logger.Warning(
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

                            // Show carryover votes in display name
                            var carryover = _carryoverPlanetVotes.GetValueOrDefault(planet.MapId);
                            if (carryover > 0)
                                displayName = $"{displayName} [+{carryover}]";

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

                        // Use OnFinished handler to set _selectedPlanet with carryover logic
                        handle.OnFinished += (_, args) =>
                        {
                            // Calculate adjusted votes (new votes + carryover)
                            var adjustedResults = new List<(RMCPlanetMapPrototypeComponent planet, int newVotes, int totalVotes)>();
                            for (int i = 0; i < planetProtos.Count && i < args.Votes.Count; i++)
                            {
                                var p = planetProtos[i];
                                var newVotes = args.Votes[i];
                                var carryover = _carryoverPlanetVotes.GetValueOrDefault(p.MapId);
                                adjustedResults.Add((p, newVotes, newVotes + carryover));
                            }

                            if (adjustedResults.Count > 0)
                            {
                                var maxVotes = adjustedResults.Max(v => v.totalVotes);
                                var winners = adjustedResults.Where(v => v.totalVotes == maxVotes).ToList();
                                _selectedPlanet = winners.Count > 1
                                    ? _random.Pick(winners).planet
                                    : winners[0].planet;

                                _chatManager.DispatchServerAnnouncement($"Planet vote winner: {(string.IsNullOrWhiteSpace(_selectedPlanet.VoteName) ? _selectedPlanet.MapId : _selectedPlanet.VoteName)}");

                                // Update carryover: accumulate for losers, reset for winner
                                foreach (var r in adjustedResults)
                                {
                                    if (r.planet == _selectedPlanet)
                                        _carryoverPlanetVotes[r.planet.MapId] = 0;
                                    else
                                        _carryoverPlanetVotes[r.planet.MapId] = _carryoverPlanetVotes.GetValueOrDefault(r.planet.MapId) + r.newVotes;
                                }
                            }
                            else
                            {
                                // Fallback
                                object? picked = null;
                                if (args.Winner != null)
                                    picked = args.Winner;
                                else if (args.Winners is var winnersArray && winnersArray.Length > 0)
                                    picked = winnersArray[0];
                                if (picked == null && options.Count > 0)
                                    picked = options[0].data;
                                _selectedPlanet = picked as RMCPlanetMapPrototypeComponent;
                            }
                        };

                        Timer.Spawn(TimeSpan.FromSeconds(32),
                            () =>
                            {
                                // Fallback: if _selectedPlanet wasn't set by handler, pick manually
                                if (_selectedPlanet == null && options.Count > 0)
                                    _selectedPlanet = options[0].data as RMCPlanetMapPrototypeComponent;
                                StartPlatoonVotes();
                            });
                    });
            });
        }

        private void PreselectThirdParties()
        {
            _selectedThirdParties.Clear();
            if (_selectedPreset == null || _selectedPlanet == null)
                return;

            var allThirdParties = new List<AuThirdPartyPrototype>();
            if (_selectedPlanet.ThirdParties.Count > 0)
            {
                foreach (var protoId in _selectedPlanet.ThirdParties)
                {
                    if (_prototypeManager.TryIndex(protoId, out AuThirdPartyPrototype? proto))
                        allThirdParties.Add(proto);
                    else
                        Logger.Warning($"[AuRoundSystem] Could not find AuThirdPartyPrototype for ID: {protoId}");
                }
            }
            else
            {
                return;
            }

            var playerCount = _playerManager.PlayerCount;
            var currentGamemode = _selectedPreset.ID;
            var currentThreat = _selectedthreat?.ID;
            var govforPlatoon = _entityManager.EntitySysManager.GetEntitySystem<PlatoonSpawnRuleSystem>().SelectedGovforPlatoon?.ID;
            var opforPlatoon = _entityManager.EntitySysManager.GetEntitySystem<PlatoonSpawnRuleSystem>().SelectedOpforPlatoon?.ID;
            var filtered = allThirdParties.ToList();
            filtered.RemoveAll(proto =>
                // Gamemode blacklist/whitelist (case-insensitive)
                proto.BlacklistedGamemodes.Any(s => s.Equals(currentGamemode, System.StringComparison.OrdinalIgnoreCase)) ||
                (proto.whitelistedgamemodes.Count > 0 && !proto.whitelistedgamemodes.Any(s => s.Equals(currentGamemode, System.StringComparison.OrdinalIgnoreCase))) ||
                // Player count limits
                proto.MaxPlayers < playerCount ||
                proto.MinPlayers > playerCount ||
                // Threat blacklist/whitelist (case-insensitive)
                (currentThreat != null && proto.BlacklistedThreats.Any(t => t.Equals(currentThreat, System.StringComparison.OrdinalIgnoreCase))) ||
                (proto.WhitelistedThreats.Count > 0 && (currentThreat == null || !proto.WhitelistedThreats.Any(t => t.Equals(currentThreat, System.StringComparison.OrdinalIgnoreCase)))) ||
                // Platoon blacklist/whitelist (case-insensitive)
                (govforPlatoon != null && proto.BlacklistedPlatoons.Any(p => p.Equals(govforPlatoon, System.StringComparison.OrdinalIgnoreCase))) ||
                (opforPlatoon != null && proto.BlacklistedPlatoons.Any(p => p.Equals(opforPlatoon, System.StringComparison.OrdinalIgnoreCase))) ||
                (proto.WhitelistedPlatoons.Any() && ((govforPlatoon != null && !proto.WhitelistedPlatoons.Any(p => p.Equals(govforPlatoon, System.StringComparison.OrdinalIgnoreCase))) || (opforPlatoon != null && !proto.WhitelistedPlatoons.Any(p => p.Equals(opforPlatoon, System.StringComparison.OrdinalIgnoreCase)))))
            );
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
            int maxThirdParties = _selectedthreat != null ? Math.Max(0, _selectedthreat.MaxThirdParties) : DefaultMaxThirdPartiesNoThreat;
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

        private void StartPlatoonVotes()
        {

            Timer.Spawn(TimeSpan.FromMilliseconds(100),
                () =>
                {

                    chooseThreat(_selectedPlanet);
                });
            Timer.Spawn(TimeSpan.FromMilliseconds(200),
                () =>
                {
                    PreselectThirdParties();
                });
            if (_selectedPreset == null || _selectedPlanet == null)
                    {
                        _voteSequenceRunning = false;
                        _selectedPreset = null;
                        _selectedPlanet = null;
                        return;
                    }



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
                            onShipSelected(winner ?? string.Empty);
                        };
                    }



                    if (_selectedPreset.RequiresGovforVote && govforPlatoons.Count > 0)
                    {
                        var optionsplatoons = new List<(string text, object data)>();
                        var govforProtoList = new List<PlatoonPrototype>();
                        foreach (var platoonId in govforPlatoons)
                        {
                            var platoon = _prototypeManager.Index<PlatoonPrototype>(platoonId);
                            govforProtoList.Add(platoon);

                            var displayName = platoon.Name;
                            var carryover = _carryoverGovforVotes.GetValueOrDefault(platoon.ID);
                            if (carryover > 0)
                                displayName = $"{displayName} [+{carryover}]";

                            optionsplatoons.Add((displayName, platoon));
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
                            // Carryover logic for govfor platoons
                            PlatoonPrototype? winnerId = null;

                            var adjustedResults = new List<(PlatoonPrototype platoon, int newVotes, int totalVotes)>();
                            for (int i = 0; i < govforProtoList.Count && i < args.Votes.Count; i++)
                            {
                                var p = govforProtoList[i];
                                var newVotes = args.Votes[i];
                                var carryover = _carryoverGovforVotes.GetValueOrDefault(p.ID);
                                adjustedResults.Add((p, newVotes, newVotes + carryover));
                            }

                            if (adjustedResults.Count > 0)
                            {
                                var maxVotes = adjustedResults.Max(v => v.totalVotes);
                                var winners = adjustedResults.Where(v => v.totalVotes == maxVotes).ToList();
                                winnerId = winners.Count > 1
                                    ? _random.Pick(winners).platoon
                                    : winners[0].platoon;

                                // Update carryover
                                foreach (var r in adjustedResults)
                                {
                                    if (r.platoon == winnerId)
                                        _carryoverGovforVotes[r.platoon.ID] = 0;
                                    else
                                        _carryoverGovforVotes[r.platoon.ID] = _carryoverGovforVotes.GetValueOrDefault(r.platoon.ID) + r.newVotes;
                                }

                                _chatManager.DispatchServerAnnouncement($"Govfor vote winner: {winnerId.Name}");
                            }
                            else if (args.Winner is PlatoonPrototype w)
                            {
                                winnerId = w;
                            }

                            if (winnerId != null)
                            {
                                platoonSpawnRuleSystem.SelectedGovforPlatoon = winnerId;

                                // If this platoon declares a tech-tree, apply it immediately to the IntelSystem as a runtime override.
                                var intelSys = _entityManager.EntitySysManager.GetEntitySystem<Content.Shared._RMC14.Intel.IntelSystem>();
                                if (!string.IsNullOrEmpty(winnerId.TechTree))
                                {
                                    intelSys.SetTeamTechTreeOverride(Team.GovFor, winnerId.TechTree);
                                }

                                // Only start ship vote if planet allows govfor in ship
                                if (_selectedPlanet.GovforInShip)
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

                    if (_selectedPreset.RequiresOpforVote && opforPlatoons.Count > 0)
                    {
                        var optionsplatoons = new List<(string text, object data)>();
                        var opforProtoList = new List<PlatoonPrototype>();
                        foreach (var platoonId in opforPlatoons)
                        {
                            var platoon = _prototypeManager.Index<PlatoonPrototype>(platoonId);
                            opforProtoList.Add(platoon);

                            var displayName = platoon.Name;
                            var carryover = _carryoverOpforVotes.GetValueOrDefault(platoon.ID);
                            if (carryover > 0)
                                displayName = $"{displayName} [+{carryover}]";

                            optionsplatoons.Add((displayName, platoon));
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
                            // Carryover logic for opfor platoons
                            PlatoonPrototype? winnerId = null;

                            var adjustedResults = new List<(PlatoonPrototype platoon, int newVotes, int totalVotes)>();
                            for (int i = 0; i < opforProtoList.Count && i < args.Votes.Count; i++)
                            {
                                var p = opforProtoList[i];
                                var newVotes = args.Votes[i];
                                var carryover = _carryoverOpforVotes.GetValueOrDefault(p.ID);
                                adjustedResults.Add((p, newVotes, newVotes + carryover));
                            }

                            if (adjustedResults.Count > 0)
                            {
                                var maxVotes = adjustedResults.Max(v => v.totalVotes);
                                var winners = adjustedResults.Where(v => v.totalVotes == maxVotes).ToList();
                                winnerId = winners.Count > 1
                                    ? _random.Pick(winners).platoon
                                    : winners[0].platoon;

                                // Update carryover
                                foreach (var r in adjustedResults)
                                {
                                    if (r.platoon == winnerId)
                                        _carryoverOpforVotes[r.platoon.ID] = 0;
                                    else
                                        _carryoverOpforVotes[r.platoon.ID] = _carryoverOpforVotes.GetValueOrDefault(r.platoon.ID) + r.newVotes;
                                }

                                _chatManager.DispatchServerAnnouncement($"Opfor vote winner: {winnerId.Name}");
                            }
                            else if (args.Winner is PlatoonPrototype w)
                            {
                                winnerId = w;
                            }

                            if (winnerId != null)
                            {
                                platoonSpawnRuleSystem.SelectedOpforPlatoon = winnerId;

                                // If this platoon declares a tech-tree, apply it immediately to the IntelSystem as a runtime override.
                                var intelSys = _entityManager.EntitySysManager.GetEntitySystem<Content.Shared._RMC14.Intel.IntelSystem>();
                                if (intelSys != null && !string.IsNullOrEmpty(winnerId.TechTree))
                                {
                                    intelSys.SetTeamTechTreeOverride(Team.OpFor, winnerId.TechTree);
                                }

                                // Only start ship vote if planet allows opfor in ship
                                if (_selectedPlanet.OpforInShip)
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

        public void chooseThreat(RMCPlanetMapPrototypeComponent? planet)
        {
            var noThreatPresets = new HashSet<string> { "ForceOnForce", "Insurgency" };

            if (_selectedPreset != null && noThreatPresets.Any(s => s.Equals(_selectedPreset.ID, System.StringComparison.OrdinalIgnoreCase)))
            {
                _selectedthreat = null!;
                Logger.Debug($"[AuRoundSystem] Skipping threat selection for preset: {_selectedPreset.ID}");
                return;
            }

            {
                var presetId = _selectedPreset?.ID;
                var allowedPresets = new[] { "Prometheus", "ColonyFall", "DistressSignal", "Jailbreak" };
                if (string.IsNullOrEmpty(presetId) ||
                    !allowedPresets.Any(p => p.Equals(presetId, StringComparison.InvariantCultureIgnoreCase)))
                {
                    return;
                }

                Logger.Debug($"[AuRoundSystem]  ffddgffgfht threat:");


                var platoonSpawnRuleSystem = _entityManager.EntitySysManager.GetEntitySystem<PlatoonSpawnRuleSystem>();

                if (planet is { AllowedThreats.Count: >= 1 })
                {
                    Logger.Debug($"[AuRoundSystem]  ffddgffgfht tt2 threat:");

                    var threats = planet.AllowedThreats.ToList();

                    string preset = "";
                    if (_selectedPreset == null)
                        return;

                    else
                    {
                        Logger.Debug($"[AuRoundSystem]454535   ffddgffgfht threat:");

                        preset = _selectedPreset.ID;
                    }

                    Logger.Debug($"[AuRoundSystem] 4354156890332 ffddgffgfht threat:");

                    // Filter allowed threats properly. The previous code attempted to call
                    // RemoveAll inside the loop which used the prototype for the current
                    // iteration across the whole list, causing incorrect removals. Build a
                    // filtered list by checking each threat prototype individually.
                    var playercount = _playerManager.PlayerCount;
                    var govforid = platoonSpawnRuleSystem?.SelectedGovforPlatoon?.ID;
                    var opforid = platoonSpawnRuleSystem?.SelectedOpforPlatoon?.ID;

                    var allowed = new List<ProtoId<ThreatPrototype>>();
                    foreach (var threatId in threats)
                    {
                        if (!_prototypeManager.TryIndex(threatId, out ThreatPrototype? threatProto))
                        {
                            // If the prototype can't be found, skip it.
                            continue;
                        }

                        // Blacklist check: if the current preset is blacklisted, skip.
                        if (threatProto.BlacklistedGamemodes.Any(s => s.Equals(preset, System.StringComparison.OrdinalIgnoreCase)))
                        {
                            continue;
                        }

                        // Whitelist check: if a whitelist exists, only allow those presets.
                        if (threatProto.whitelistedgamemodes.Count > 0 &&
                            !threatProto.whitelistedgamemodes.Any(s => s.Equals(preset, System.StringComparison.OrdinalIgnoreCase)))
                        {
                            continue;
                        }

                        // Player count limits
                        if (threatProto.MaxPlayers < playercount) continue;
                        if (threatProto.MinPlayers > playercount) continue;

                        // Platoon blacklist checks
                        if (govforid != null && threatProto.BlacklistedPlatoons.Any(p => p.Equals(govforid, System.StringComparison.OrdinalIgnoreCase)))
                            continue;
                        if (opforid != null && threatProto.BlacklistedPlatoons.Any(p => p.Equals(opforid, System.StringComparison.OrdinalIgnoreCase)))
                            continue;

                        // Platoon whitelist: if present, require the govfor/opfor to be present in the whitelist
                        if (threatProto.WhitelistedPlatoons.Any())
                        {
                            if ((govforid != null && !threatProto.WhitelistedPlatoons.Any(p => p.Equals(govforid, System.StringComparison.OrdinalIgnoreCase))) ||
                                (opforid != null && !threatProto.WhitelistedPlatoons.Any(p => p.Equals(opforid, System.StringComparison.OrdinalIgnoreCase))))
                            {
                                continue;
                            }
                        }

                        allowed.Add(threatId);
                    }

                    // Replace threats (List<ProtoId<ThreatPrototype>>) with allowed list
                    threats = allowed.ToList();

                    if (threats.Count > 0)
                    {
                            // Build a weighted list for random selection
                            var weightedThreats = new List<ProtoId<ThreatPrototype>>();
                            foreach (var threatId in threats)
                            {
                                if (_prototypeManager.TryIndex(threatId, out ThreatPrototype? threatProto))
                                {
                                    int weight = Math.Max(1, threatProto.ThreatWeight);
                                    for (int i = 0; i < weight; i++)
                                    {
                                        weightedThreats.Add(threatId);
                                    }
                                }
                            }

                            if (weightedThreats.Count > 0)
                            {
                                var random = new Random();
                                var threatSelectedId = weightedThreats[random.Next(weightedThreats.Count)];
                                Logger.Debug($"[AuRoundSystem]  selected threat: {threatSelectedId}");
                                _selectedthreat =
                                    _prototypeManager.TryIndex(threatSelectedId, out ThreatPrototype? threatSelected)
                                        ? threatSelected
                                        : null!;
                                if (_selectedthreat.WinConditions.Count > 0)
                                {
                                    var ticker = _entityManager.EntitySysManager.GetEntitySystem<GameTicker>();
                                    foreach (var ruleId in _selectedthreat.WinConditions)
                                    {
                                        ticker.StartGameRule(ruleId);
                                        Logger.Debug(
                                            $"[AuRoundSystem] Started wincondition rule from threat: {ruleId}");
                                    }
                                }
                            }
                            else
                            {
                                Logger.Debug(
                                    $"[AuRoundSystem]  No valid threats found for planet {planet.MapId} with preset {preset}, govfor {govforid}, opfor {opforid}");
                            }
                        }

                    }
                }

            }
        }
    }










