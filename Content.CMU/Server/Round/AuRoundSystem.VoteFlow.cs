using Content.Server._CMU14.Threats;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Presets;
using Content.Server.Maps;
using Content.Server.Voting;
using Content.Server.Voting.Managers;
using Content.Shared._RMC14.Intel;
using Content.Shared._RMC14.Rules;
using Content.Shared.AU14.util;
using Content.Shared.CCVar;
using Content.Shared.Voting;
using Robust.Shared.Prototypes;

namespace Content.Server.AU14.Round;

public sealed partial class AuRoundSystem
{
    private readonly record struct PlanetCandidate(
        string Id,
        RMCPlanetMapPrototypeComponent Planet);

    private void OnVoteContinuation(AuRoundVoteContinuationEvent ev)
    {
        if (IsCurrentVoteSequence(ev.SequenceId))
            ev.Continuation();
    }

    private void QueueVoteContinuation(int sequenceId, Action continuation)
    {
        if (!IsCurrentVoteSequence(sequenceId))
            return;

        QueueLocalEvent(new AuRoundVoteContinuationEvent(sequenceId, continuation));
    }

    private void TrackVoteOutcome(
        IVoteHandle vote,
        int sequenceId,
        Action<VoteFinishedEventArgs> onFinished,
        Action onCancelled)
    {
        TrackVoteHandle(vote);
        var concluded = false;
        vote.OnFinished += HandleFinished;
        vote.OnCancelled += HandleCancelled;

        void HandleFinished(IVoteHandle handle, VoteFinishedEventArgs args)
        {
            if (TryConclude())
                onFinished(args);
        }

        void HandleCancelled(IVoteHandle handle)
        {
            if (TryConclude())
                onCancelled();
        }

        bool TryConclude()
        {
            if (concluded || !IsCurrentVoteSequence(sequenceId))
                return false;

            concluded = true;
            return true;
        }
    }

    private void BeginPresetSelection(int sequenceId)
    {
        var existingVotes = new HashSet<int>();
        foreach (var activeVote in _voteManager.ActiveVotes)
        {
            existingVotes.Add(activeVote.Id);
        }

        _voteManager.CreateStandardVote(null, StandardVoteType.Preset);

        IVoteHandle? presetVote = null;
        foreach (var activeVote in _voteManager.ActiveVotes)
        {
            if (existingVotes.Contains(activeVote.Id))
                continue;

            presetVote = activeVote;
            break;
        }

        if (presetVote == null)
        {
            _sawmill.Warning("[AuRoundSystem] Preset vote could not be found after starting it; using the configured preset.");
            ApplyPresetSelectionAndQueueContinuation(sequenceId, null);
            return;
        }

        TrackVoteOutcome(
            presetVote,
            sequenceId,
            args =>
            {
                var winner = GetPresetVoteWinner(args);
                ApplyPresetSelectionAndQueueContinuation(sequenceId, winner);
            },
            () => ApplyPresetSelectionAndQueueContinuation(sequenceId, null));
    }

    private void ApplyPresetSelectionAndQueueContinuation(int sequenceId, string? selectedPresetId)
    {
        if (!IsCurrentVoteSequence(sequenceId))
            return;

        var preset = ResolvePreset(selectedPresetId);
        _selectedPreset = preset;
        if (preset == null)
            ClearWorldSelection();

        // Store the completed outcome now, but defer creating the next vote until VoteManager
        // has finished enumerating its active votes for this tick.
        QueueVoteContinuation(sequenceId, () => ContinueAfterPresetSelection(sequenceId, preset));
    }

    private void ContinueAfterPresetSelection(int sequenceId, GamePresetPrototype? preset)
    {
        if (!IsCurrentVoteSequence(sequenceId))
            return;

        if (preset == null)
        {
            _sawmill.Warning("[AuRoundSystem] No valid voted or configured preset was available.");
            FinishVoteSequence(sequenceId);
            return;
        }

        BeginPlanetSelection(sequenceId, preset);
    }

    private void BeginPlanetSelection(int sequenceId, GamePresetPrototype preset)
    {
        var candidates = GetEligiblePlanets(preset, _playerManager.PlayerCount);
        if (candidates.Count == 0)
        {
            ClearWorldSelection();
            FinishVoteSequence(sequenceId);
            return;
        }

        if (candidates.Count == 1)
        {
            SelectPlanetAndBeginFactionSelection(sequenceId, preset, candidates[0]);
            return;
        }

        var planets = new List<RMCPlanetMapPrototypeComponent>(candidates.Count);
        foreach (var candidate in candidates)
        {
            planets.Add(candidate.Planet);
        }

        var options = BuildPlanetVoteOptions(preset.ID, planets, TimeSpan.FromSeconds(30));
        options.SetInitiatorOrServer(null);
        var vote = _voteManager.CreateVote(options);
        TrackVoteOutcome(
            vote,
            sequenceId,
            args =>
            {
                var selectedMapId = GetVoteWinner<string>(args);
                var selected = FindPlanetByMapId(candidates, selectedMapId) ?? candidates[0];
                args.ResolveWinner(selected.Planet.MapId);
                ApplyPlanetSelection(selected);
                QueueVoteContinuation(
                    sequenceId,
                    () => BeginFactionSelections(sequenceId, preset, selected.Planet));
            },
            () =>
            {
                var selected = candidates[0];
                ApplyPlanetSelection(selected);
                QueueVoteContinuation(
                    sequenceId,
                    () => BeginFactionSelections(sequenceId, preset, selected.Planet));
            });
    }

    private void SelectPlanetAndBeginFactionSelection(
        int sequenceId,
        GamePresetPrototype preset,
        PlanetCandidate selected)
    {
        if (!IsCurrentVoteSequence(sequenceId))
            return;

        ApplyPlanetSelection(selected);
        BeginFactionSelections(sequenceId, preset, selected.Planet);
    }

    private void BeginFactionSelections(
        int sequenceId,
        GamePresetPrototype preset,
        RMCPlanetMapPrototypeComponent planet)
    {
        var required = AuRoundSelectionRules.GetActiveFactionBranches(
            preset.RequiresGovforVote,
            preset.RequiresOpforVote,
            preset.UsesGovforPlatoon,
            preset.UsesOpforPlatoon);

        _voteCompletion.Begin(sequenceId, required);
        if (required == AuRoundVoteBranch.None)
        {
            FinishVoteSequence(sequenceId);
            return;
        }

        if ((required & AuRoundVoteBranch.Govfor) != 0)
            BeginFactionSelection(sequenceId, preset, planet, AuRoundVoteBranch.Govfor);
        if ((required & AuRoundVoteBranch.Opfor) != 0)
            BeginFactionSelection(sequenceId, preset, planet, AuRoundVoteBranch.Opfor);
    }

    private void BeginFactionSelection(
        int sequenceId,
        GamePresetPrototype preset,
        RMCPlanetMapPrototypeComponent planet,
        AuRoundVoteBranch faction)
    {
        var candidates = GetFactionPlatoons(planet, faction);
        if (candidates.Count == 0)
        {
            SetFactionPlatoon(faction, null);
            SetFactionShip(faction, null);
            CompleteFactionSelection(sequenceId, faction);
            return;
        }

        if (candidates.Count == 1 || !FactionRequiresVote(preset, faction))
        {
            var selected = GetDefaultPlatoon(candidates, planet, faction);
            SelectPlatoonAndBeginShipSelection(sequenceId, preset, planet, faction, selected);
            return;
        }

        var factionName = GetFactionName(faction);
        var planetId = _selectedPlanetId ?? planet.MapId;
        var options = AuRoundSelectionRules.BuildPlatoonVoteOptions(
            factionName,
            preset.ID,
            planetId,
            candidates,
            TimeSpan.FromSeconds(_cfg.GetCVar(CCVars.VotePlatoonDuration)));
        options.SetInitiatorOrServer(null);
        var vote = _voteManager.CreateVote(options);
        TrackVoteOutcome(
            vote,
            sequenceId,
            args =>
            {
                var selected = FindPlatoon(candidates, GetVoteWinner<PlatoonPrototype>(args)) ??
                               GetDefaultPlatoon(candidates, planet, faction);
                args.ResolveWinner(selected);
                ApplyPlatoonSelectionAndQueueContinuation(sequenceId, preset, planet, faction, selected);
            },
            () =>
            {
                var selected = GetDefaultPlatoon(candidates, planet, faction);
                ApplyPlatoonSelectionAndQueueContinuation(sequenceId, preset, planet, faction, selected);
            });
    }

    private void ApplyPlatoonSelectionAndQueueContinuation(
        int sequenceId,
        GamePresetPrototype preset,
        RMCPlanetMapPrototypeComponent planet,
        AuRoundVoteBranch faction,
        PlatoonPrototype selected)
    {
        SetFactionPlatoon(faction, selected);
        if (!FactionUsesShip(planet, faction))
        {
            SetFactionShip(faction, null);
            QueueVoteContinuation(sequenceId, () => CompleteFactionSelection(sequenceId, faction));
            return;
        }

        QueueVoteContinuation(
            sequenceId,
            () => BeginShipSelection(sequenceId, preset, planet, faction, selected));
    }

    private void SelectPlatoonAndBeginShipSelection(
        int sequenceId,
        GamePresetPrototype preset,
        RMCPlanetMapPrototypeComponent planet,
        AuRoundVoteBranch faction,
        PlatoonPrototype platoon)
    {
        if (!IsCurrentVoteSequence(sequenceId))
            return;

        SetFactionPlatoon(faction, platoon);
        if (!FactionUsesShip(planet, faction))
        {
            SetFactionShip(faction, null);
            CompleteFactionSelection(sequenceId, faction);
            return;
        }

        BeginShipSelection(sequenceId, preset, planet, faction, platoon);
    }

    private void BeginShipSelection(
        int sequenceId,
        GamePresetPrototype preset,
        RMCPlanetMapPrototypeComponent planet,
        AuRoundVoteBranch faction,
        PlatoonPrototype platoon)
    {
        var candidates = GetValidShips(platoon);
        if (candidates.Count == 0)
        {
            SetFactionShip(faction, null);
            CompleteFactionSelection(sequenceId, faction);
            return;
        }

        if (candidates.Count == 1 || !FactionRequiresVote(preset, faction))
        {
            SetFactionShip(faction, candidates[0]);
            CompleteFactionSelection(sequenceId, faction);
            return;
        }

        var factionName = GetFactionName(faction);
        var planetId = _selectedPlanetId ?? planet.MapId;
        var options = AuRoundSelectionRules.BuildShipVoteOptions(
            factionName,
            preset.ID,
            planetId,
            platoon,
            candidates,
            TimeSpan.FromSeconds(_cfg.GetCVar(CCVars.VotePlatoonDuration)));
        options.SetInitiatorOrServer(null);
        var vote = _voteManager.CreateVote(options);
        TrackVoteOutcome(
            vote,
            sequenceId,
            args =>
            {
                var winner = AuRoundSelectionRules.SelectCandidate(
                                 candidates,
                                 GetVoteWinner<string>(args)) ??
                             candidates[0];
                args.ResolveWinner(winner);
                SetFactionShip(faction, winner);
                QueueVoteContinuation(sequenceId, () => CompleteFactionSelection(sequenceId, faction));
            },
            () =>
            {
                SetFactionShip(faction, candidates[0]);
                QueueVoteContinuation(sequenceId, () => CompleteFactionSelection(sequenceId, faction));
            });
    }

    private void CompleteFactionSelection(int sequenceId, AuRoundVoteBranch faction)
    {
        if (_voteCompletion.Complete(sequenceId, faction))
            FinishVoteSequence(sequenceId);
    }

    private void FinishVoteSequence(int sequenceId)
    {
        if (!IsCurrentVoteSequence(sequenceId))
            return;

        FinalizeDerivedSelections();
        if (!_voteSequence.TryFinish(sequenceId))
            return;

        _selectionFinalized = true;
        var onFinished = _voteSequenceFinished;
        _voteSequenceFinished = null;
        onFinished?.Invoke();
    }

    /// <summary>
    /// Invalidates unfinished ballots and fills every map-affecting selection before preloading consumes it.
    /// </summary>
    internal void FinalizeVoteSequence(int playerCount, string? fallbackPresetId)
    {
        if (_selectionFinalized &&
            (string.IsNullOrWhiteSpace(fallbackPresetId) ||
             fallbackPresetId.Equals(_selectedPreset?.ID, StringComparison.OrdinalIgnoreCase)) &&
            HasCompletePreloadSelection(playerCount))
        {
            FinalizeDerivedSelections();
            return;
        }

        var onFinished = _voteSequenceFinished;
        _voteSequenceFinished = null;
        _voteSequence.Restart();

        var previousPresetId = _selectedPreset?.ID;
        var preset = ResolvePreset(fallbackPresetId);
        var presetChanged = !string.Equals(previousPresetId, preset?.ID, StringComparison.OrdinalIgnoreCase);
        _selectedPreset = preset;
        if (presetChanged)
            ClearWorldSelection();

        if (preset == null)
        {
            ClearWorldSelection();
            CompleteForcedSelection(onFinished);
            return;
        }

        var planets = GetEligiblePlanets(preset, playerCount);
        var planet = FindPlanetById(planets, _selectedPlanetId) ??
                     (planets.Count > 0 ? planets[0] : null);
        if (planet == null)
        {
            ClearWorldSelection();
            CompleteForcedSelection(onFinished);
            return;
        }

        ApplyPlanetSelection(planet.Value);
        ResolveFactionForCutoff(preset, planet.Value.Planet, AuRoundVoteBranch.Govfor);
        ResolveFactionForCutoff(preset, planet.Value.Planet, AuRoundVoteBranch.Opfor);
        FinalizeDerivedSelections();
        CompleteForcedSelection(onFinished);
    }

    private void CompleteForcedSelection(Action? onFinished)
    {
        _selectionFinalized = true;
        onFinished?.Invoke();
    }

    private bool HasCompletePreloadSelection(int playerCount)
    {
        if (_selectedPreset == null)
            return false;

        var planets = GetEligiblePlanets(_selectedPreset, playerCount);
        if (planets.Count == 0)
            return !HasConfiguredPlanets(_selectedPreset) && _selectedPlanet == null;

        var selectedPlanet = FindPlanetById(planets, _selectedPlanetId);
        if (selectedPlanet == null)
            return false;

        return HasCompleteFactionSelection(selectedPlanet.Value.Planet, AuRoundVoteBranch.Govfor) &&
               HasCompleteFactionSelection(selectedPlanet.Value.Planet, AuRoundVoteBranch.Opfor);
    }

    private bool HasCompleteFactionSelection(
        RMCPlanetMapPrototypeComponent planet,
        AuRoundVoteBranch faction)
    {
        if (_selectedPreset == null || !FactionIsEnabled(_selectedPreset, faction))
            return true;

        var platoons = GetFactionPlatoons(planet, faction);
        var selectedPlatoon = FindPlatoon(platoons, GetFactionPlatoon(faction));
        if (selectedPlatoon == null)
            return false;

        if (!FactionUsesShip(planet, faction))
            return true;

        var ships = GetValidShips(selectedPlatoon);
        var selectedShip = GetFactionShip(faction);
        var canonicalShip = AuRoundSelectionRules.SelectCandidate(ships, selectedShip);
        return selectedShip != null &&
               selectedShip.Equals(canonicalShip, StringComparison.OrdinalIgnoreCase);
    }

    private bool HasConfiguredPlanets(GamePresetPrototype preset)
    {
        if (preset.SupportedPlanets is { Count: > 0 })
            return true;

        return !string.IsNullOrWhiteSpace(preset.PlanetPool) &&
               _prototypeManager.TryIndex<GamePlanetPoolPrototype>(preset.PlanetPool, out var pool) &&
               pool.Planets.Count > 0;
    }

    private void ResolveFactionForCutoff(
        GamePresetPrototype preset,
        RMCPlanetMapPrototypeComponent planet,
        AuRoundVoteBranch faction)
    {
        if (!FactionIsEnabled(preset, faction))
        {
            SetFactionPlatoon(faction, null);
            SetFactionShip(faction, null);
            return;
        }

        var platoons = GetFactionPlatoons(planet, faction);
        var current = GetFactionPlatoon(faction)?.ID;
        var preferred = GetDefaultPlatoonId(planet, faction);
        var platoonIds = new List<string>(platoons.Count);
        foreach (var platoon in platoons)
        {
            platoonIds.Add(platoon.ID);
        }

        var selectedId = AuRoundSelectionRules.SelectCandidate(platoonIds, current, preferred);
        var selected = FindPlatoon(platoons, selectedId);
        SetFactionPlatoon(faction, selected);
        if (selected == null || !FactionUsesShip(planet, faction))
        {
            SetFactionShip(faction, null);
            return;
        }

        var ships = GetValidShips(selected);
        SetFactionShip(
            faction,
            AuRoundSelectionRules.SelectCandidate(ships, GetFactionShip(faction)));
    }

    private GamePresetPrototype? ResolvePreset(string? requestedId)
    {
        if (TryResolvePreset(requestedId, out var requested))
            return requested;

        var ticker = _entityManager.EntitySysManager.GetEntitySystem<GameTicker>();
        var configured = ticker.CurrentPreset ?? ticker.Preset;
        if (configured != null)
            return configured;

        return _selectedPreset;
    }

    private bool TryResolvePreset(string? presetId, out GamePresetPrototype? preset)
    {
        if (!string.IsNullOrWhiteSpace(presetId) &&
            _prototypeManager.TryIndex(presetId, out preset))
        {
            return true;
        }

        preset = null;
        return false;
    }

    private List<PlanetCandidate> GetEligiblePlanets(GamePresetPrototype preset, int playerCount)
    {
        var candidates = new List<PlanetCandidate>();
        var usedMapIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(preset.PlanetPool) &&
            _prototypeManager.TryIndex<GamePlanetPoolPrototype>(preset.PlanetPool, out var pool) &&
            pool.Planets.Count > 0)
        {
            AddEligiblePlanets(pool.Planets, playerCount, candidates, usedMapIds);
            if (candidates.Count > 0)
                return candidates;
        }

        if (preset.SupportedPlanets is { Count: > 0 })
            AddEligiblePlanets(preset.SupportedPlanets, playerCount, candidates, usedMapIds);

        return candidates;
    }

    private void AddEligiblePlanets(
        IReadOnlyList<string> planetIds,
        int playerCount,
        List<PlanetCandidate> candidates,
        HashSet<string> usedMapIds)
    {
        foreach (var planetId in planetIds)
        {
            if (!_prototypeManager.TryIndex<EntityPrototype>(planetId, out var entity) ||
                !entity.TryComp(out RMCPlanetMapPrototypeComponent? planet, _componentFactory))
            {
                _sawmill.Warning($"[AuRoundSystem] Planet prototype '{planetId}' has no RMC planet map component.");
                continue;
            }

            if (!AuRoundSelectionRules.IsPlayerCountAllowed(playerCount, planet.MinPlayers, planet.MaxPlayers))
                continue;

            if (string.IsNullOrWhiteSpace(planet.MapId) ||
                !_prototypeManager.TryIndex<GameMapPrototype>(planet.MapId, out _))
            {
                _sawmill.Warning($"[AuRoundSystem] Planet prototype '{planetId}' has no valid GameMapPrototype MapId.");
                continue;
            }

            if (usedMapIds.Add(planet.MapId))
                candidates.Add(new PlanetCandidate(planetId, planet));
        }
    }

    private List<PlatoonPrototype> GetFactionPlatoons(
        RMCPlanetMapPrototypeComponent planet,
        AuRoundVoteBranch faction)
    {
        var ids = faction == AuRoundVoteBranch.Govfor
            ? planet.PlatoonsGovfor
            : planet.PlatoonsOpfor;
        var platoons = new List<PlatoonPrototype>(ids.Count);
        foreach (var id in ids)
        {
            if (_prototypeManager.TryIndex(id, out PlatoonPrototype? platoon))
                platoons.Add(platoon);
            else
                _sawmill.Warning($"[AuRoundSystem] Planet '{planet.MapId}' references unknown platoon '{id}'.");
        }

        return platoons;
    }

    private List<string> GetValidShips(PlatoonPrototype platoon)
    {
        var ships = new List<string>(platoon.PossibleShips.Count);
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var shipId in platoon.PossibleShips)
        {
            if (_prototypeManager.TryIndex<GameMapPrototype>(shipId, out _) && used.Add(shipId))
                ships.Add(shipId);
            else if (!used.Contains(shipId))
                _sawmill.Warning($"[AuRoundSystem] Platoon '{platoon.ID}' references unknown ship map '{shipId}'.");
        }

        return ships;
    }

    private void ResetMutableSelection()
    {
        _state.Reset();
        SelectedPlanetMap = null;
        var platoons = GetPlatoonSpawnRuleSystem();
        platoons.SelectedGovforPlatoon = null;
        platoons.SelectedOpforPlatoon = null;
    }

    private void ClearWorldSelection()
    {
        ClearPlanetAndFactionSelection();
        _state.SelectedThreat = null;
        _state.ResetDistressSignalThirdPartyLock();
        _selectedThirdParties.Clear();
    }

    private void ResetThirdPartySelectionForPlanetChange()
    {
        _state.ResetDistressSignalThirdPartyLock();
        _selectedThirdParties.Clear();
    }

    private void ApplyPlanetSelection(PlanetCandidate selected)
    {
        if (!string.Equals(_selectedPlanetId, selected.Id, StringComparison.OrdinalIgnoreCase))
            ResetThirdPartySelectionForPlanetChange();

        _state.SetPlanet(selected.Id, selected.Planet);
        SetCamoType();
    }

    private void ClearPlanetAndFactionSelection()
    {
        _selectedPlanet = null;
        _selectedPlanetId = null;
        SetFactionPlatoon(AuRoundVoteBranch.Govfor, null);
        SetFactionPlatoon(AuRoundVoteBranch.Opfor, null);
        SetFactionShip(AuRoundVoteBranch.Govfor, null);
        SetFactionShip(AuRoundVoteBranch.Opfor, null);
    }

    private void FinalizeDerivedSelections()
    {
        if (_selectedPreset?.ThreatSelectionMode != CmuThreatSelectionMode.PreRoundstart)
        {
            _state.SelectedThreat = null;
        }
        else if (!IsSelectedThreatAllowedForCurrentContext())
        {
            _state.SelectedThreat = null;
            ChooseThreat(_selectedPlanet);
        }

        PreselectThirdParties();
    }

    private bool IsSelectedThreatAllowedForCurrentContext()
    {
        if (_state.SelectedThreat is not { } selected ||
            _selectedPreset == null ||
            _selectedPlanet == null)
        {
            return false;
        }

        var listed = false;
        foreach (var allowed in _selectedPlanet.AllowedThreats)
        {
            if (!allowed.Id.Equals(selected.ID, StringComparison.OrdinalIgnoreCase))
                continue;

            listed = true;
            break;
        }

        if (!listed)
            return false;

        var platoons = GetPlatoonSpawnRuleSystem();
        return ThreatVoteSelection.IsThreatAllowed(
            selected,
            _selectedPreset.ID,
            platoons.SelectedGovforPlatoon?.ID,
            platoons.SelectedOpforPlatoon?.ID,
            _playerManager.PlayerCount);
    }

    private PlatoonSpawnRuleSystem GetPlatoonSpawnRuleSystem()
    {
        return _platoonSpawnRule ??=
            _entityManager.EntitySysManager.GetEntitySystem<PlatoonSpawnRuleSystem>();
    }

    private PlatoonPrototype? GetFactionPlatoon(AuRoundVoteBranch faction)
    {
        var platoons = GetPlatoonSpawnRuleSystem();
        return faction == AuRoundVoteBranch.Govfor
            ? platoons.SelectedGovforPlatoon
            : platoons.SelectedOpforPlatoon;
    }

    private void SetFactionPlatoon(AuRoundVoteBranch faction, PlatoonPrototype? platoon)
    {
        var platoons = GetPlatoonSpawnRuleSystem();
        if (faction == AuRoundVoteBranch.Govfor)
            platoons.SelectedGovforPlatoon = platoon;
        else
            platoons.SelectedOpforPlatoon = platoon;

        _intel.SetTeamTechTreeOverride(
            faction == AuRoundVoteBranch.Govfor ? Team.GovFor : Team.OpFor,
            platoon?.TechTree);
    }

    private string? GetFactionShip(AuRoundVoteBranch faction)
    {
        return faction == AuRoundVoteBranch.Govfor
            ? _selectedGovforShip
            : _selectedOpforShip;
    }

    private void SetFactionShip(AuRoundVoteBranch faction, string? ship)
    {
        if (faction == AuRoundVoteBranch.Govfor)
            _selectedGovforShip = ship;
        else
            _selectedOpforShip = ship;
    }

    private static bool FactionIsEnabled(GamePresetPrototype preset, AuRoundVoteBranch faction)
    {
        return faction == AuRoundVoteBranch.Govfor
            ? preset.RequiresGovforVote || preset.UsesGovforPlatoon
            : preset.RequiresOpforVote || preset.UsesOpforPlatoon;
    }

    private static bool FactionRequiresVote(GamePresetPrototype preset, AuRoundVoteBranch faction)
    {
        return faction == AuRoundVoteBranch.Govfor
            ? preset.RequiresGovforVote
            : preset.RequiresOpforVote;
    }

    private static bool FactionUsesShip(
        RMCPlanetMapPrototypeComponent planet,
        AuRoundVoteBranch faction)
    {
        return faction == AuRoundVoteBranch.Govfor
            ? planet.GovforInShip
            : planet.OpforInShip;
    }

    private static string GetFactionName(AuRoundVoteBranch faction)
    {
        return faction == AuRoundVoteBranch.Govfor ? "Govfor" : "Opfor";
    }

    private static string? GetDefaultPlatoonId(
        RMCPlanetMapPrototypeComponent planet,
        AuRoundVoteBranch faction)
    {
        return faction == AuRoundVoteBranch.Govfor
            ? planet.DefaultGovforPlatoon
            : planet.DefaultOpforPlatoon;
    }

    private static PlatoonPrototype GetDefaultPlatoon(
        IReadOnlyList<PlatoonPrototype> candidates,
        RMCPlanetMapPrototypeComponent planet,
        AuRoundVoteBranch faction)
    {
        var ids = new List<string>(candidates.Count);
        foreach (var candidate in candidates)
        {
            ids.Add(candidate.ID);
        }

        var selectedId = AuRoundSelectionRules.SelectCandidate(
            ids,
            null,
            GetDefaultPlatoonId(planet, faction));
        return FindPlatoon(candidates, selectedId) ?? candidates[0];
    }

    private static PlanetCandidate? FindPlanetById(
        IReadOnlyList<PlanetCandidate> candidates,
        string? planetId)
    {
        if (string.IsNullOrWhiteSpace(planetId))
            return null;

        foreach (var candidate in candidates)
        {
            if (candidate.Id.Equals(planetId, StringComparison.OrdinalIgnoreCase))
                return candidate;
        }

        return null;
    }

    private static PlanetCandidate? FindPlanetByMapId(
        IReadOnlyList<PlanetCandidate> candidates,
        string? mapId)
    {
        if (string.IsNullOrWhiteSpace(mapId))
            return null;

        foreach (var candidate in candidates)
        {
            if (candidate.Planet.MapId.Equals(mapId, StringComparison.OrdinalIgnoreCase))
                return candidate;
        }

        return null;
    }

    private static PlatoonPrototype? FindPlatoon(
        IReadOnlyList<PlatoonPrototype> candidates,
        PlatoonPrototype? selected)
    {
        return FindPlatoon(candidates, selected?.ID);
    }

    private static PlatoonPrototype? FindPlatoon(
        IReadOnlyList<PlatoonPrototype> candidates,
        string? selectedId)
    {
        if (string.IsNullOrWhiteSpace(selectedId))
            return null;

        foreach (var candidate in candidates)
        {
            if (candidate.ID.Equals(selectedId, StringComparison.OrdinalIgnoreCase))
                return candidate;
        }

        return null;
    }

    private static T? GetVoteWinner<T>(VoteFinishedEventArgs args)
        where T : class
    {
        if (args.SelectedWinner is T selected)
            return selected;
        if (args.Winner is T winner)
            return winner;
        if (args.Winners.Length > 0 && args.Winners[0] is T first)
            return first;

        return null;
    }
}

internal sealed class AuRoundVoteContinuationEvent(int sequenceId, Action continuation) : EntityEventArgs
{
    public int SequenceId { get; } = sequenceId;
    public Action Continuation { get; } = continuation;
}
