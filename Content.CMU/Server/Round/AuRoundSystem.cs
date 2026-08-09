using Content.Server.AU14.Scenario;
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
using Content.Shared._RMC14.Rules;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared._CMU14.Threats;
using Content.Shared.AU14.util;
using Content.Shared.CMU.Round;
using Content.Shared.CCVar;
using Content.Shared.Preferences;
using Robust.Server.Player;
using Robust.Server.GameObjects;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Random;
using Content.Shared._RMC14.Item;

namespace Content.Server.AU14.Round
{
    /// <summary>
    /// Persistent system that manages the full sequence of votes (preset, planet, platoon, etc.)
    /// </summary>
    public sealed partial class AuRoundSystem : EntitySystem
    {
        private const string DistressSignalPresetId = "DistressSignal";

        [Dependency] private IComponentFactory _componentFactory = default!;
        [Dependency] private IConfigurationManager _cfg = default!;
        [Dependency] private IEntityManager _entityManager = default!;
        [Dependency] private IPlayerManager _playerManager = default!;
        [Dependency] private IPrototypeManager _prototypeManager = default!;
        [Dependency] private IRobustRandom _random = default!;
        [Dependency] private IServerPreferencesManager _prefsManager = default!;
        [Dependency] private IVoteManager _voteManager = default!;
        [Dependency] private ItemCamouflageSystem _camo = default!;

        [ViewVariables]
        public string? SelectedPlanetMapName => SelectedPlanetMap?.Announcement;

        /// <summary>The active planet's prototype component, used by other systems to read per-planet settings.</summary>
        public RMCPlanetMapPrototypeComponent? ActivePlanet => SelectedPlanetMap;

        [ViewVariables]
        private RMCPlanetMapPrototypeComponent? SelectedPlanetMap { get; set; }

        private readonly AuRoundSelectionState _state = new();
        private readonly AuRoundVoteSequenceTracker _voteSequence = new();
        private readonly AuRoundVoteCompletionState _voteCompletion = new();
        private readonly ISawmill _sawmill = Logger.GetSawmill("content");
        private PlatoonSpawnRuleSystem? _platoonSpawnRule;
        private Action? _voteSequenceFinished;
        private bool _selectionFinalized;

        private GamePresetPrototype? _selectedPreset
        {
            get => _state.SelectedPreset;
            set
            {
                if (!string.Equals(_state.SelectedPreset?.ID, value?.ID, StringComparison.OrdinalIgnoreCase))
                    _state.ResetDistressSignalThirdPartyLock();

                _state.SelectedPreset = value;
            }
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

        public void SetOpforShip(string shipId)
        {
            GetRoundDirectorSystem().TrySetMainShip(RoundSide.Opfor, shipId);
        }

        public void SetGovforShip(string shipId)
        {
            GetRoundDirectorSystem().TrySetMainShip(RoundSide.Govfor, shipId);
        }
        public void SetPreset(GamePresetPrototype? preset) => _selectedPreset = preset;
        public void SetSelectedThreat(ThreatPrototype? threat)
        {
            _state.SelectedThreat = threat;
            _sawmill.Debug($"[AuRoundSystem] Selected threat set to: {threat?.ID ?? "null"}");
        }

        /// <summary>
        /// Captures the current mutable selections for pre-freeze planning.
        /// Committed round consumers must capture through <see cref="CMURoundDirectorSystem"/>.
        /// </summary>
        public RoundPlanSelectionSnapshot CaptureRoundPlanSelection(int playerCount)
        {
            return CaptureRoundPlanSelection(
                playerCount,
                _selectedPreset?.ID ?? string.Empty,
                SelectedThreat?.ID);
        }

        /// <summary>
        /// Captures the current mutable world selections with an explicit runtime preset and threat context.
        /// Committed round consumers must capture through <see cref="CMURoundDirectorSystem"/>.
        /// </summary>
        public RoundPlanSelectionSnapshot CaptureRoundPlanSelection(int playerCount,
            string presetId,
            string? selectedThreatId)
        {
            var platoons = _platoonSpawnRule ??=
                _entityManager.EntitySysManager.GetEntitySystem<PlatoonSpawnRuleSystem>();

            return new(
                presetId,
                playerCount,
                platoons.SelectedGovforPlatoon?.ID,
                platoons.SelectedOpforPlatoon?.ID,
                _selectedPlanetId,
                _selectedPlanet?.MapId,
                selectedThreatId,
                _selectedGovforShip,
                _selectedOpforShip);
        }

        internal void ApplyLegacyForceSelection(RoundSide side, PlatoonPrototype? platoon)
        {
            var platoons = GetPlatoonSpawnRuleSystem();
            switch (side)
            {
                case RoundSide.Govfor:
                    platoons.SelectedGovforPlatoon = platoon;
                    break;
                case RoundSide.Opfor:
                    platoons.SelectedOpforPlatoon = platoon;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(side), side, null);
            }
        }

        internal PlatoonPrototype? GetLegacyForceSelection(RoundSide side)
        {
            var platoons = GetPlatoonSpawnRuleSystem();
            return side switch
            {
                RoundSide.Govfor => platoons.SelectedGovforPlatoon,
                RoundSide.Opfor => platoons.SelectedOpforPlatoon,
                _ => throw new ArgumentOutOfRangeException(nameof(side), side, null),
            };
        }

        internal void ApplyMainShipSelection(RoundSide side, string? shipId)
        {
            switch (side)
            {
                case RoundSide.Govfor:
                    _selectedGovforShip = shipId;
                    break;
                case RoundSide.Opfor:
                    _selectedOpforShip = shipId;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(side), side, null);
            }
        }

        private CMURoundDirectorSystem GetRoundDirectorSystem()
        {
            return _entityManager.EntitySysManager.GetEntitySystem<CMURoundDirectorSystem>();
        }

        public bool UsesPostRoundstartThreatVote()
        {
            return _selectedPreset?.ThreatSelectionMode == CmuThreatSelectionMode.PostRoundstartVote;
        }

        private List<ThirdPartyPrototype> _selectedThirdParties => _state.SelectedThirdParties;
        public IReadOnlyList<ThirdPartyPrototype> SelectedThirdParties => _state.SelectedThirdParties;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<AuRoundVoteContinuationEvent>(OnVoteContinuation);
            _voteSequence.Reset();
            _state.Reset();
            SelectedPlanetMap = null;
        }

        /// <summary>
        /// Starts the full vote sequence: preset, planet, faction platoons, and faction ships.
        /// </summary>
        private bool IsCurrentVoteSequence(int sequenceId)
        {
            return _voteSequence.IsRunning(sequenceId);
        }

        private void TrackVoteHandle(IVoteHandle handle)
        {
            _voteSequence.Track(handle);
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
            BeginPresetSelection(_voteSequenceId);
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

            bool usesPresetSchedule = SelectedThreat == null && _selectedPreset.ThirdPartyAutoSpawn;
            if (SelectedThreat == null && !usesPresetSchedule)
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
                if (!IsThirdPartyAllowedForCurrentContext(proto))
                    continue;

                // Threatless preset scheduling is opt-in on both sides. This prevents generally available
                // third parties from silently becoming natural Insurgency spawns.
                if (usesPresetSchedule &&
                    !AuRoundSelectionRules.IsExplicitlyWhitelistedForGamemode(proto, _selectedPreset.ID))
                    continue;

                candidates.Add(proto);
            }

            var playerCount = _playerManager.PlayerCount;
            float thirdPartyRatio = SelectedThreat?.ThirdPartyRatio ?? _selectedPreset.ThirdPartyRatio;
            int maxThirdParties = Math.Max(0, SelectedThreat?.MaxThirdParties ?? _selectedPreset.MaxThirdParties);
            var bodyBudget = CalculateThirdPartyBodyBudget(playerCount, thirdPartyRatio);
            if (SelectedThreat != null &&
                TryCalculateThreatBodyCount(SelectedThreat, playerCount, out var threatBodyCount))
                bodyBudget = Math.Min(bodyBudget, threatBodyCount.Total);

            _sawmill.Debug(
                $"[AuRoundSystem] Third-party candidates for planet {_selectedPlanet.MapId}: listed={allThirdParties.Count}, allowed={candidates.Count}, schedule={(usesPresetSchedule ? $"preset:{_selectedPreset.ID}" : $"threat:{SelectedThreat!.ID}")}, max={maxThirdParties}, bodyBudget={bodyBudget}.");
            if (candidates.Count == 0)
                return;

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

        internal bool HasLockedDistressSignalThirdParties => _state.DistressSignalThirdPartiesLocked;

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

        internal void StartVoteSequence(Action? onFinished = null)
        {
            _voteSequence.Restart();
            ResetMutableSelection();
            _voteSequenceFinished = onFinished;
            _selectionFinalized = false;
            StartFullVoteSequence();
        }

        /// <summary>
        /// Clears mutable lobby choices when a new round generation begins, even when voting is below its player gate.
        /// </summary>
        internal void ResetLobbySelection()
        {
            _voteSequence.Reset();
            ResetMutableSelection();
            _voteSequenceFinished = null;
            _selectionFinalized = false;
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
            SetOpforShip(opfor);
        }

        public void SetGovfor(string govfor)
        {
            SetGovforShip(govfor);
        }

        public bool SetPlanet(string planetId)
        {
            if (_prototypeManager.TryIndex<EntityPrototype>(planetId, out var proto) &&
                proto.TryComp(out RMCPlanetMapPrototypeComponent? planetComp,
                _componentFactory))
            {
                ApplyPlanetSelection(new PlanetCandidate(planetId, planetComp));
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

            var threatSelectionMode = _selectedPreset?.ThreatSelectionMode ?? CmuThreatSelectionMode.Disabled;
            if (threatSelectionMode == CmuThreatSelectionMode.Disabled)
            {
                _state.SelectedThreat = null;
                _sawmill.Debug($"[AuRoundSystem] Skipping threat selection for preset: {_selectedPreset?.ID ?? "null"}");
                return;
            }

            var presetId = _selectedPreset?.ID;
            if (threatSelectionMode == CmuThreatSelectionMode.PostRoundstartVote)
            {
                _state.SelectedThreat = null;
                _sawmill.Debug($"[AuRoundSystem] Deferring threat selection for post-roundstart vote preset: {presetId}");
                return;
            }

            if (string.IsNullOrEmpty(presetId) ||
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
