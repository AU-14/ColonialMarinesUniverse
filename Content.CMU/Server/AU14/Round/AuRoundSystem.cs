using Content.Server.GameTicking;
using Content.Server.Preferences.Managers;
using Content.Server.Voting.Managers;
using Content.Shared.Voting;
using Robust.Shared.Prototypes;
using Robust.Shared.Configuration;
using System.Linq;
using Content.Server._CMU14.Threats;
using Content.Server.GameTicking.Presets;
using Content.Server.Maps;
using Content.Server.Voting;
using Content.Shared._RMC14.Intel;
using Content.Shared._RMC14.Rules;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared._CMU14.Threats;
using Content.Shared.AU14.util;
using Content.Shared.CCVar;
using Content.Shared.Preferences;
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
        private const string DistressSignalPresetId = "DistressSignal";

        private static readonly HashSet<string> NoThreatPresets = new(StringComparer.OrdinalIgnoreCase)
        {
            "ForceOnForce",
            "Insurgency",
        };

        private static readonly HashSet<string> PostRoundstartThreatVotePresets = new(StringComparer.OrdinalIgnoreCase)
        {
            "DistressSignal",
            "ColonyFall",
        };

        private static readonly HashSet<string> ThreatSelectionPresets = new(StringComparer.OrdinalIgnoreCase)
        {
            "Prometheus",
            "ColonyFall",
            "DistressSignal",
            "Jailbreak",
        };

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

        private readonly AuRoundSelectionState _state = new();
        private readonly AuRoundVoteSequenceTracker _voteSequence = new();
        private readonly ISawmill _sawmill = Logger.GetSawmill("content");

        private GamePresetPrototype? _selectedPreset
        {
            get => _state.SelectedPreset;
            set => _state.SelectedPreset = value;
        }

        public GamePresetPrototype? SelectedPreset => _state.SelectedPreset;

        private RMCPlanetMapPrototypeComponent? _selectedPlanet
        {
            get => _state.SelectedPlanet;
            set => _state.SelectedPlanet = value;
        }

        private string? _selectedPlanetId
        {
            get => _state.SelectedPlanetId;
            set => _state.SelectedPlanetId = value;
        }

        private bool _voteSequenceRunning
        {
            get => _voteSequence.Running;
            set => _voteSequence.Running = value;
        }

        private int _voteSequenceId => _voteSequence.SequenceId;
        public ThreatPrototype? SelectedThreat => _state.SelectedThreat;

        private string? _selectedGovforShip
        {
            get => _state.SelectedGovforShip;
            set => _state.SelectedGovforShip = value;
        }

        private string? _selectedOpforShip
        {
            get => _state.SelectedOpforShip;
            set => _state.SelectedOpforShip = value;
        }

        public void SetOpforShip(string shipId) => _selectedOpforShip = shipId;
        public void SetGovforShip(string shipId) => _selectedGovforShip = shipId;
        public void SetPreset(GamePresetPrototype? preset) => _selectedPreset = preset;
        public void SetSelectedThreat(ThreatPrototype? threat)
        {
            _state.SelectedThreat = threat;
            _sawmill.Debug($"[AuRoundSystem] Selected threat set to: {threat?.ID ?? "null"}");
        }

        public bool UsesPostRoundstartThreatVote()
        {
            var presetId = _selectedPreset?.ID;
            return IsPostRoundstartThreatVotePreset(presetId);
        }

        public static bool IsPostRoundstartThreatVotePreset(string? presetId)
        {
            return presetId != null && PostRoundstartThreatVotePresets.Contains(presetId);
        }

        private List<ThirdPartyPrototype> _selectedThirdParties => _state.SelectedThirdParties;
        public IReadOnlyList<ThirdPartyPrototype> SelectedThirdParties => _state.SelectedThirdParties;

        public override void Initialize()
        {

            base.Initialize();
            _voteSequence.Reset();
            _state.Reset();
            SelectedPlanetMap = null;

        }

        /// <summary>
        /// Starts the full vote sequence: preset, planet, then platoons.
        /// Each vote method takes a callback to call when finished
        /// </summary>
        private IVoteHandle? StartPresetVote(int sequenceId, Action<string?> onFinished)
        {
            var existingVotes = _voteManager.ActiveVotes
                .Select(vote => vote.Id)
                .ToHashSet();

            _voteManager.CreateStandardVote(null, StandardVoteType.Preset);
            foreach (var vote in _voteManager.ActiveVotes.Where(vote => !existingVotes.Contains(vote.Id)))
            {
                vote.OnFinished += (_, args) =>
                {
                    if (!IsCurrentVoteSequence(sequenceId))
                        return;

                    Logger.GetSawmill("content").Debug("[PlatoonVoteManagerSystem] Preset vote finished.");
                    onFinished(GetPresetVoteWinner(args));
                };
                TrackVoteHandle(vote);
                return vote;
            }

            Logger.GetSawmill("content").Warning("[PlatoonVoteManagerSystem] Preset vote could not be found after starting it.");
            return null;
        }

        private bool IsCurrentVoteSequence(int sequenceId)
        {
            return _voteSequence.IsCurrent(sequenceId);
        }

        private void TrackVoteHandle(IVoteHandle handle)
        {
            _voteSequence.Track(handle);
        }

        private void CancelActiveVoteHandles()
        {
            _voteSequence.CancelActive();
        }

        private static string? GetPresetVoteWinner(VoteFinishedEventArgs args)
        {
            if (args.SelectedWinner is string selected)
                return selected;

            if (args.Winner is string winner)
                return winner;

            if (args.Winners.Length > 0 && args.Winners[0] is string first)
                return first;

            return null;
        }

        public void StartFullVoteSequence()
        {
            if (_voteSequenceRunning)
                return;
            _voteSequenceRunning = true;
            _selectedPreset = null;
            _selectedPlanet = null;
            _selectedPlanetId = null;
            _state.SelectedThreat = null;
            _state.ResetDistressSignalThirdPartyLock();
            _selectedThirdParties.Clear();
            var sequenceId = _voteSequenceId;
            var presetVote = StartPresetVote(sequenceId, presetId =>
            {
                if (!IsCurrentVoteSequence(sequenceId))
                    return;

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
                var planetProtos = new List<(string Id, RMCPlanetMapPrototypeComponent Planet)>();
                foreach (var pid in planetIds)
                {
                    if (_prototypeManager.TryIndex<EntityPrototype>(pid, out var proto) &&
                        proto.TryComp(out RMCPlanetMapPrototypeComponent? planetComp,
                            IoCManager.Resolve<IComponentFactory>()))
                    {
                        planetProtos.Add((pid, planetComp));
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
                    (p.Planet.MinPlayers > 0 && playerCount < p.Planet.MinPlayers) ||
                    // If MaxPlayers is set (>0) and current players exceed it, exclude.
                    (p.Planet.MaxPlayers > 0 && playerCount > p.Planet.MaxPlayers)
                );

                if (planetProtos.Count == 0)
                {
                    _voteSequenceRunning = false;
                    return;
                }

                var planets = planetProtos
                    .Select(planet => planet.Planet)
                    .ToList();
                var vote = BuildPlanetVoteOptions(preset.ID, planets, TimeSpan.FromSeconds(30));
                vote.SetInitiatorOrServer(null);
                var planetByMapId = planetProtos
                    .GroupBy(planet => planet.Planet.MapId, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
                var handle = _voteManager.CreateVote(vote);
                TrackVoteHandle(handle);

                // Use OnFinished handler to set _selectedPlanet
                handle.OnFinished += (_, args) =>
                {
                    if (!IsCurrentVoteSequence(sequenceId))
                        return;

                    string? picked = null;
                    if (args.Winner is string winner)
                        picked = winner;
                    else if (args.Winners is var winnersArray && winnersArray.Length > 0)
                        picked = winnersArray[0] as string;
                    if (picked == null && vote.Options.Count > 0)
                        picked = vote.Options[0].data as string;
                    if (picked != null && planetByMapId.TryGetValue(picked, out var planet))
                    {
                        args.ResolveWinner(picked);
                        _state.SetPlanet(planet.Id, planet.Planet);
                    }
                };

                Timer.Spawn(TimeSpan.FromSeconds(32),
                    () =>
                    {
                        if (!IsCurrentVoteSequence(sequenceId))
                            return;

                        // Fallback: if _selectedPlanet wasn't set by handler, pick manually
                        if (_selectedPlanet == null && planetProtos.Count > 0)
                        {
                            _state.SetPlanet(planetProtos[0].Id, planetProtos[0].Planet);
                        }
                        SetCamoType();
                        StartPlatoonVotes(sequenceId);
                    });
            });

            if (presetVote == null)
                _voteSequenceRunning = false;
        }

        public bool IsThirdPartyAllowedForCurrentContext(ThirdPartyPrototype proto)
        {
            if (_selectedPreset == null)
                return true;

            var platoonSpawnRuleSystem = _entityManager.EntitySysManager.GetEntitySystem<PlatoonSpawnRuleSystem>();
            return IsThirdPartyAllowed(
                proto,
                _selectedPreset.ID,
                SelectedThreat?.ID,
                platoonSpawnRuleSystem.SelectedGovforPlatoon?.ID,
                platoonSpawnRuleSystem.SelectedOpforPlatoon?.ID,
                _playerManager.PlayerCount);
        }

        private static bool IsThirdPartyAllowed(
            ThirdPartyPrototype proto,
            string currentGamemode,
            string? currentThreat,
            string? govforPlatoon,
            string? opforPlatoon,
            int playerCount)
        {
            return AuRoundSelectionRules.IsThirdPartyAllowed(
                proto,
                currentGamemode,
                currentThreat,
                govforPlatoon,
                opforPlatoon,
                playerCount);
        }

        internal static VoteOptions BuildPlanetVoteOptions(
            string presetId,
            IReadOnlyList<RMCPlanetMapPrototypeComponent> planets,
            TimeSpan duration)
        {
            return AuRoundSelectionRules.BuildPlanetVoteOptions(presetId, planets, duration);
        }

        private void PreselectThirdParties()
        {
            if (_state.DistressSignalThirdPartiesLocked &&
                _selectedPreset?.ID.Equals(DistressSignalPresetId, StringComparison.OrdinalIgnoreCase) == true)
            {
                FillLockedDistressSignalThirdPartiesForSelectedThreat();
                _sawmill.Debug(
                    $"[AuRoundSystem] Keeping pre-round Distress Signal third-party selection: selected={
                        _selectedThirdParties.Count}, survivors={_state.DistressSignalSurvivorCount}, fillCompleted={
                            _state.DistressSignalThirdPartyFillCompleted}.");
                return;
            }

            _selectedThirdParties.Clear();
            Logger.GetSawmill("content").Debug(
                $"[AuRoundSystem] PreselectThirdParties start: preset={_selectedPreset?.ID ?? "null"}, planet={_selectedPlanet?.MapId ?? "null"}, threat={SelectedThreat?.ID ?? "null"}.");
            if (_selectedPreset == null || _selectedPlanet == null)
                return;
            if (SelectedThreat == null)
                return;

            var allThirdParties = new List<ThirdPartyPrototype>();
            if (_selectedPlanet.ThirdParties.Count > 0)
            {
                foreach (var protoId in _selectedPlanet.ThirdParties)
                {
                    if (_prototypeManager.TryIndex(protoId, out ThirdPartyPrototype? proto))
                        allThirdParties.Add(proto);
                    else
                        _sawmill.Warning($"[AuRoundSystem] Could not find ThirdPartyPrototype for ID: {protoId}");
                }
            }
            else
            {
                return;
            }

            var candidates = new List<ThirdPartyPrototype>();
            foreach (var proto in allThirdParties)
            {
                if (IsThirdPartyAllowedForCurrentContext(proto))
                    candidates.Add(proto);
            }

            var playerCount = _playerManager.PlayerCount;
            var bodyBudget = CalculateThirdPartyBodyBudget(playerCount, SelectedThreat.ThirdPartyRatio);
            if (TryCalculateThreatBodyCount(SelectedThreat, playerCount, out var threatBodyCount))
                bodyBudget = Math.Min(bodyBudget, threatBodyCount.Total);

            _sawmill.Debug(
                $"[AuRoundSystem] Third-party candidates for planet {_selectedPlanet.MapId}: listed={allThirdParties.Count}, allowed={candidates.Count}, max={SelectedThreat.MaxThirdParties}, bodyBudget={bodyBudget}.");
            if (candidates.Count == 0)
                return;

            var maxThirdParties = Math.Max(0, SelectedThreat.MaxThirdParties);
            if (maxThirdParties <= 0 || bodyBudget <= 0)
                return;

            List<ThirdPartyPrototype> selected = SelectThirdPartiesWithinBodyBudget(
                candidates,
                maxThirdParties,
                bodyBudget,
                PickWeightedThirdParty,
                GetThirdPartyBodyCount,
                out var selectedBodyCount);

            SetSelectedThirdPartiesInSpawnOrder(selected);
            if (_sawmill.Level <= Robust.Shared.Log.LogLevel.Debug)
            {
                _sawmill.Debug(
                    $"[AuRoundSystem] Selected third parties: bodies={selectedBodyCount}/{bodyBudget}, {string.Join(", ", _selectedThirdParties.Select(party => $"{party.ID}(roundStart={party.RoundStart}, bodies={GetThirdPartyBodyCount(party)})"))}");
            }

            int GetThirdPartyBodyCount(ThirdPartyPrototype party)
                => TryCalculateThirdPartyBodyCount(party, playerCount, out var bodyCount)
                    ? bodyCount
                    : 0;
        }

        internal static int CalculateThirdPartyBodyBudget(
            int playerCount,
            float thirdPartyRatio,
            ThreatVoteBodyCount? threatBodyCount = null)
        {
            if (playerCount <= 0 ||
                thirdPartyRatio <= 0 ||
                float.IsNaN(thirdPartyRatio) ||
                float.IsInfinity(thirdPartyRatio))
            {
                return 0;
            }

            var budget = (int) Math.Floor(playerCount * thirdPartyRatio);
            if (threatBodyCount is { } cap)
                budget = Math.Min(budget, cap.Total);

            return Math.Max(0, budget);
        }

        internal static List<ThirdPartyPrototype> SelectThirdPartiesWithinBodyBudget(
            IReadOnlyList<ThirdPartyPrototype> candidates,
            int maxThirdParties,
            int bodyBudget,
            Func<IReadOnlyList<ThirdPartyPrototype>, ThirdPartyPrototype?> pickThirdParty,
            Func<ThirdPartyPrototype, int> getBodyCount,
            out int selectedBodyCount)
        {
            selectedBodyCount = 0;
            var selected = new List<ThirdPartyPrototype>();
            if (maxThirdParties <= 0 || bodyBudget <= 0 || candidates.Count == 0)
                return selected;

            var remaining = candidates
                .DistinctBy(candidate => candidate.ID, StringComparer.OrdinalIgnoreCase)
                .ToList();
            while (selected.Count < maxThirdParties &&
                   selectedBodyCount < bodyBudget &&
                   remaining.Count > 0)
            {
                var remainingBudget = bodyBudget - selectedBodyCount;
                var fitting = remaining
                    .Where(candidate =>
                    {
                        var bodyCount = getBodyCount(candidate);
                        return bodyCount > 0 && bodyCount <= remainingBudget;
                    })
                    .ToList();
                if (fitting.Count == 0)
                    break;

                var pick = pickThirdParty(fitting);
                if (pick == null)
                    break;

                remaining.Remove(pick);
                var pickedBodies = getBodyCount(pick);
                if (pickedBodies <= 0 || pickedBodies > remainingBudget)
                    continue;

                selected.Add(pick);
                selectedBodyCount += pickedBodies;
            }

            return selected;
        }

        private bool TryCalculateThreatBodyCount(ThreatPrototype threat,
            int playerCount,
            out ThreatVoteBodyCount bodyCount)
        {
            bodyCount = default;
            if (!_prototypeManager.TryIndex(threat.RoundStartSpawn, out PartySpawnPrototype? spawn))
                return false;

            bodyCount = ThreatVoteSelection.CalculateBodyCount(spawn, playerCount);
            return true;
        }

        private bool TryCalculateThirdPartyBodyCount(ThirdPartyPrototype party, int playerCount, out int bodyCount)
        {
            bodyCount = 0;
            if (!_prototypeManager.TryIndex(party.PartySpawn, out PartySpawnPrototype? spawn))
                return false;

            bodyCount = ThreatVoteSelection.CalculateBodyCount(spawn, playerCount).Total;
            return true;
        }

        private ThirdPartyPrototype? PickWeightedThirdParty(IReadOnlyList<ThirdPartyPrototype> candidates)
        {
            var totalWeight = 0;
            foreach (var candidate in candidates)
            {
                totalWeight += Math.Max(1, candidate.weight);
            }

            if (totalWeight <= 0)
                return null;

            var roll = _random.Next(totalWeight);
            foreach (var candidate in candidates)
            {
                roll -= Math.Max(1, candidate.weight);
                if (roll < 0)
                    return candidate;
            }

            return candidates[candidates.Count - 1];
        }

        public void PreselectThirdPartiesForSelectedThreat()
        {
            PreselectThirdParties();
        }

        private void FillLockedDistressSignalThirdPartiesForSelectedThreat()
        {
            var selectedThreat = SelectedThreat;
            var selectedPlanet = _selectedPlanet;
            if (_state.DistressSignalThirdPartyFillCompleted ||
                selectedThreat == null ||
                selectedPlanet == null)
            {
                return;
            }

            var playerCount = _playerManager.PlayerCount;
            var bodyBudget = CalculateThirdPartyBodyBudget(playerCount, selectedThreat.ThirdPartyRatio);
            if (TryCalculateThreatBodyCount(selectedThreat, playerCount, out var threatBodyCount))
                bodyBudget = Math.Min(bodyBudget, threatBodyCount.Total);
            var maxThirdParties = Math.Max(0, selectedThreat.MaxThirdParties);

            var candidates = new List<ThirdPartyPrototype>();
            foreach (var partyId in selectedPlanet.ThirdParties)
            {
                if (!_prototypeManager.TryIndex(partyId, out ThirdPartyPrototype? party))
                {
                    _sawmill.Warning($"[AuRoundSystem] Could not find ThirdPartyPrototype for ID: {partyId}");
                    continue;
                }

                if (IsThirdPartyAllowedForCurrentContext(party))
                    candidates.Add(party);
            }

            List<ThirdPartyPrototype> additional = SelectAdditionalDistressSignalThirdParties(
                candidates,
                _selectedThirdParties,
                maxThirdParties,
                bodyBudget,
                PickWeightedThirdParty,
                GetThirdPartyBodyCount,
                out var lockedBodyCount,
                out var additionalBodyCount);

            var lockedPartyCount = _selectedThirdParties.Count;
            if (lockedPartyCount > maxThirdParties || lockedBodyCount > bodyBudget)
            {
                _sawmill.Warning(
                    $"[AuRoundSystem] Locked Distress Signal third parties exceed the final threat capacity after the player count changed: selected={
                        lockedPartyCount}/{maxThirdParties}, bodies={lockedBodyCount}/{bodyBudget}. Keeping the announced roster.");
            }

            if (additional.Count > 0)
                SetSelectedThirdPartiesInSpawnOrder(_selectedThirdParties.Concat(additional));

            _state.DistressSignalThirdPartyFillCompleted = true;
            _sawmill.Info(
                $"[AuRoundSystem] Completed Distress Signal third-party selection for threat {selectedThreat.ID}: locked={
                    lockedPartyCount}, added={additional.Count}, bodies={lockedBodyCount + additionalBodyCount}/{
                        bodyBudget}, survivors={_state.DistressSignalSurvivorCount}.");

            int GetThirdPartyBodyCount(ThirdPartyPrototype party)
                => TryCalculateThirdPartyBodyCount(party, playerCount, out var bodyCount)
                    ? bodyCount
                    : 0;
        }

        internal static List<ThirdPartyPrototype> SelectAdditionalDistressSignalThirdParties(
            IReadOnlyList<ThirdPartyPrototype> candidates,
            IReadOnlyCollection<ThirdPartyPrototype> lockedParties,
            int maxThirdParties,
            int bodyBudget,
            Func<IReadOnlyList<ThirdPartyPrototype>, ThirdPartyPrototype?> pickThirdParty,
            Func<ThirdPartyPrototype, int> getBodyCount,
            out int lockedBodyCount,
            out int additionalBodyCount)
        {
            lockedBodyCount = lockedParties.Sum(party => Math.Max(0, getBodyCount(party)));
            var lockedIds = lockedParties
                .Select(party => party.ID)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var fillCandidates = candidates
                .Where(party =>
                    !lockedIds.Contains(party.ID) &&
                    !(party.RoundStart && party.AnnounceAsSurvivors))
                .ToList();

            return SelectThirdPartiesWithinBodyBudget(
                fillCandidates,
                Math.Max(0, maxThirdParties - lockedParties.Count),
                Math.Max(0, bodyBudget - lockedBodyCount),
                pickThirdParty,
                getBodyCount,
                out additionalBodyCount);
        }

        private void SetSelectedThirdPartiesInSpawnOrder(IEnumerable<ThirdPartyPrototype> selected)
        {
            var parties = selected.ToList();
            _selectedThirdParties.Clear();
            _selectedThirdParties.AddRange(parties.Where(party => party.RoundStart));
            _selectedThirdParties.AddRange(parties.Where(party => !party.RoundStart));
        }

        /// <summary>
        ///     Locks a safe Distress Signal third-party roster before the post-roundstart threat vote.
        ///     The winning threat can fill any remaining capacity without changing the announced survivor count.
        /// </summary>
        public bool TryLockDistressSignalThirdParties(out int survivorCount)
        {
            survivorCount = _state.DistressSignalSurvivorCount;
            if (_state.DistressSignalThirdPartiesLocked)
                return true;

            if (_selectedPreset?.ID.Equals(DistressSignalPresetId, StringComparison.OrdinalIgnoreCase) != true ||
                _selectedPlanet == null)
            {
                return false;
            }

            var playerCount = _playerManager.PlayerCount;
            var platoonSpawnRule = _entityManager.EntitySysManager.GetEntitySystem<PlatoonSpawnRuleSystem>();
            var govforId = platoonSpawnRule.SelectedGovforPlatoon?.ID;
            var opforId = platoonSpawnRule.SelectedOpforPlatoon?.ID;
            var threatLimits = new List<(int MaxThirdParties, int BodyBudget)>();
            var eligibleThreatIds = new List<string>();

            foreach (var threatId in _selectedPlanet.AllowedThreats)
            {
                if (!_prototypeManager.TryIndex(threatId, out ThreatPrototype? threat) ||
                    !ThreatVoteSelection.IsThreatAllowed(
                        threat,
                        DistressSignalPresetId,
                        govforId,
                        opforId,
                        playerCount) ||
                    !TryCalculateThreatBodyCount(threat, playerCount, out var threatBodyCount) ||
                    threatBodyCount.Total <= 0)
                {
                    continue;
                }

                eligibleThreatIds.Add(threat.ID);
                threatLimits.Add((
                    Math.Max(0, threat.MaxThirdParties),
                    CalculateThirdPartyBodyBudget(playerCount, threat.ThirdPartyRatio, threatBodyCount)));
            }

            var (maxThirdParties, bodyBudget) = GetConservativeThirdPartyLimits(threatLimits);
            var candidates = new List<ThirdPartyPrototype>();
            foreach (var partyId in _selectedPlanet.ThirdParties)
            {
                if (!_prototypeManager.TryIndex(partyId, out ThirdPartyPrototype? party))
                {
                    _sawmill.Warning($"[AuRoundSystem] Could not find ThirdPartyPrototype for ID: {partyId}");
                    continue;
                }

                var allowedForEveryThreat = eligibleThreatIds.Count > 0 && eligibleThreatIds.All(threatId =>
                    IsThirdPartyAllowed(
                        party,
                        DistressSignalPresetId,
                        threatId,
                        govforId,
                        opforId,
                        playerCount));
                if (allowedForEveryThreat)
                    candidates.Add(party);
            }

            var bodyCounts = new Dictionary<ThirdPartyPrototype, int>();
            foreach (var party in candidates)
            {
                bodyCounts[party] = TryCalculateThirdPartyBodyCount(party, playerCount, out var count)
                    ? count
                    : 0;
            }

            List<ThirdPartyPrototype> selected = SelectThirdPartiesWithinBodyBudget(
                candidates,
                maxThirdParties,
                bodyBudget,
                PickWeightedThirdParty,
                party => bodyCounts[party],
                out var selectedBodyCount);

            SetSelectedThirdPartiesInSpawnOrder(selected);
            survivorCount = CalculateAnnouncedSurvivorCount(selected, party => bodyCounts[party]);
            _state.DistressSignalSurvivorCount = survivorCount;
            _state.DistressSignalThirdPartiesLocked = true;
            _state.DistressSignalThirdPartyFillCompleted = false;

            _sawmill.Info(
                $"[AuRoundSystem] Locked Distress Signal third parties before round start: selected={
                    selected.Count}, bodies={selectedBodyCount}/{bodyBudget}, survivors={survivorCount}, eligibleThreats=[{
                        string.Join(", ", eligibleThreatIds)}].");

            return true;
        }

        internal static (int MaxThirdParties, int BodyBudget) GetConservativeThirdPartyLimits(
            IReadOnlyCollection<(int MaxThirdParties, int BodyBudget)> threatLimits)
        {
            if (threatLimits.Count == 0)
                return default;

            return (
                threatLimits.Min(limit => Math.Max(0, limit.MaxThirdParties)),
                threatLimits.Min(limit => Math.Max(0, limit.BodyBudget)));
        }

        internal static int CalculateAnnouncedSurvivorCount(
            IEnumerable<ThirdPartyPrototype> selected,
            Func<ThirdPartyPrototype, int> getBodyCount)
        {
            return selected
                .Where(party => party.RoundStart && party.AnnounceAsSurvivors)
                .Sum(getBodyCount);
        }

        /// <summary>
        ///     Clears the committed pre-round roster without disturbing ordinary third-party preselection.
        /// </summary>
        public void ResetLockedDistressSignalThirdParties()
        {
            _state.ResetDistressSignalThirdPartyLock();
        }

        private void StartPlatoonVotes(int sequenceId)
        {
            if (!IsCurrentVoteSequence(sequenceId))
                return;

            if (_selectedPreset == null || _selectedPlanet == null)
            {
                _voteSequenceRunning = false;
                _selectedPreset = null;
                _selectedPlanet = null;
                _selectedPlanetId = null;
                return;
            }

            var presetProto = _selectedPreset;
            var planetProto = _selectedPlanet;

            Timer.Spawn(TimeSpan.FromMilliseconds(100),
                () =>
                {
                    if (!IsCurrentVoteSequence(sequenceId))
                        return;

                    ChooseThreat(planetProto);
                });
            Timer.Spawn(TimeSpan.FromMilliseconds(200),
                () =>
                {
                    if (!IsCurrentVoteSequence(sequenceId))
                        return;

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
                TrackVoteHandle(handle);
                handle.OnFinished += (_, args) =>
                {
                    if (!IsCurrentVoteSequence(sequenceId))
                        return;

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
                TrackVoteHandle(handle);
                handle.OnFinished += (_, args) =>
                {
                    if (!IsCurrentVoteSequence(sequenceId))
                        return;

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
                                    if (!IsCurrentVoteSequence(sequenceId))
                                        return;

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
                TrackVoteHandle(handle);
                handle.OnFinished += (_, args) =>
                {
                    if (!IsCurrentVoteSequence(sequenceId))
                        return;

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
                                    if (!IsCurrentVoteSequence(sequenceId))
                                        return;

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
            _voteSequence.Restart();
            _state.Reset();
            SelectedPlanetMap = null;

            StartFullVoteSequence();
            onFinished?.Invoke();
        }

        public RMCPlanetMapPrototypeComponent? GetSelectedPlanet()
        {
            return _selectedPlanet;
        }

        public string? GetSelectedPlanetId()
        {
            return _selectedPlanetId;
        }

        // --- PLANET LOGIC: Load planet like cmdistress does after round starts ---
        // Dead code - never called - legacy from AuVoteRuleSystem class
        public void LoadSelectedPlanetMap_()
        {
            if (_selectedPlanet == null)
                return;

            var mapLoader = _entityManager.EntitySysManager.GetEntitySystem<MapLoaderSystem>();
            var mapSystem = _entityManager.EntitySysManager.GetEntitySystem<MapSystem>();
            var sawmill = Logger.GetSawmill("game");
            // var compFactory = IoCManager.Resolve<IComponentFactory>();
            // var serialization = IoCManager.Resolve<ISerializationManager>();

            // Try to load the selected planet's map
            if (!_prototypeManager.TryIndex<GameMapPrototype>(_selectedPlanet.MapId, out var mapProto))
            {
                sawmill.Error(
                    $"[AuRoundSystem] Failed to find GameMapPrototype for selected planet: {_selectedPlanet.MapId}");
                return;
            }

            if (!mapLoader.TryLoadMap(mapProto.MapPath, out var mapNullable, out var _))
            {
                sawmill.Error($"[AuRoundSystem] Failed to load selected planet map: {mapProto.MapPath}");
                return;
            }

            var map = mapNullable.Value;
            mapSystem.InitializeMap((map, map));

            // Attach RMCPlanetComponent, TacticalMapComponent, etc. (if not already present)
            // TODO: Look at how multiple Z levels tackle this
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
                proto.TryComp(out RMCPlanetMapPrototypeComponent? planetComp,
                IoCManager.Resolve<IComponentFactory>()))
            {
                _state.SetPlanet(planetId, planetComp);
                SetCamoType();
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

        public void ChooseThreat(RMCPlanetMapPrototypeComponent? planet)
        {
            if (_cfg.GetCVar(CCVars.GameDummyTicker))
                return;

            if (_selectedPreset != null && NoThreatPresets.Contains(_selectedPreset.ID))
            {
                _state.SelectedThreat = null;
                _sawmill.Debug($"[AuRoundSystem] Skipping threat selection for preset: {_selectedPreset.ID}");
                return;
            }

            var presetId = _selectedPreset?.ID;
            if (IsPostRoundstartThreatVotePreset(presetId))
            {
                _state.SelectedThreat = null;
                _sawmill.Debug($"[AuRoundSystem] Deferring threat selection for post-roundstart vote preset: {presetId}");
                return;
            }

            if (string.IsNullOrEmpty(presetId) ||
                !ThreatSelectionPresets.Contains(presetId) ||
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
                _sawmill.Debug(
                    $"[AuRoundSystem] No valid threats found for planet {planet.MapId} with preset {presetId}, govfor {govforId}, opfor {opforId}");
                return;
            }

            var preferredThreats = GetThreatPreferenceWeights(threats);
            var threatSelected = PickWeightedThreat(threats, preferredThreats);
            if (threatSelected == null)
                return;

            _sawmill.Debug($"[AuRoundSystem] Selected threat: {threatSelected.ID}");
            _state.SelectedThreat = threatSelected;

        }

        private ThreatPrototype? PickWeightedThreat(
            IReadOnlyList<ProtoId<ThreatPrototype>> threats,
            IReadOnlyDictionary<string, int> preferredThreats)
        {
            var totalWeight = 0;
            foreach (var threatId in threats)
            {
                if (!_prototypeManager.TryIndex(threatId, out ThreatPrototype? threatProto))
                    continue;

                totalWeight += GetThreatSelectionWeight(threatProto, preferredThreats);
            }

            if (totalWeight <= 0)
                return null;

            var roll = _random.Next(totalWeight);
            foreach (var threatId in threats)
            {
                if (!_prototypeManager.TryIndex(threatId, out ThreatPrototype? threatProto))
                    continue;

                roll -= GetThreatSelectionWeight(threatProto, preferredThreats);
                if (roll < 0)
                    return threatProto;
            }

            return null;
        }

        private static int GetThreatSelectionWeight(
            ThreatPrototype threat,
            IReadOnlyDictionary<string, int> preferredThreats)
        {
            var weight = Math.Max(1, threat.ThreatWeight);
            if (preferredThreats.TryGetValue(threat.ID, out var preferenceCount))
                weight += preferenceCount * Math.Max(3, threat.ThreatWeight);

            return weight;
        }

        public void StartThreatWinConditions(ThreatPrototype threat)
        {
            StartThreatWinConditions(threat.WinConditions, $"threat '{threat.ID}'");
        }

        public void StartThreatWinConditions(IReadOnlyList<string> winConditions, string source)
        {
            if (winConditions.Count == 0)
                return;

            var ticker = _entityManager.EntitySysManager.GetEntitySystem<GameTicker>();
            foreach (var ruleId in winConditions)
            {
                ticker.StartGameRule(ruleId);
                _sawmill.Debug($"[AuRoundSystem] Started wincondition rule from {source}: {ruleId}");
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
