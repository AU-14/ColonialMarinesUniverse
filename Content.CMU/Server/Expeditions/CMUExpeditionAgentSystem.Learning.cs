using System.Text.Json;
using Content.Shared.CMU14.Expeditions;
using Content.Shared.GameTicking;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    [Dependency] private IResourceManager _resources = default!;
    private static readonly ResPath ExperienceFile = new("/cmu-expedition-experience.json");
    private Dictionary<string, CMUTacticalExperience> _experience = new();
    private Dictionary<string, CMUTacticalExperience> _roundExperience = new();
    private bool _experienceChanged;

    private void InitializeLearning()
    {
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnLearningRoundRestart);
        try
        {
            if (_resources.UserData.TryReadAllText(ExperienceFile, out var json))
                _experience = JsonSerializer.Deserialize<Dictionary<string, CMUTacticalExperience>>(json) ?? new();
            // Accept only the finite biome/disposition vocabulary, and reject non-finite or malformed tuning data.
            var clean = new Dictionary<string, CMUTacticalExperience>();
            foreach (var biome in Enum.GetValues<CMUExpeditionBiome>())
            foreach (var disposition in Enum.GetValues<CMUExpeditionDisposition>())
            {
                var key = $"{biome}/{disposition}";
                if (_experience.TryGetValue(key, out var value) && value != null && value.Samples >= 0 &&
                    float.IsFinite(value.FlankReturn) && float.IsFinite(value.PeekReturn))
                {
                    value.Samples = Math.Min(10000, value.Samples);
                    value.FlankReturn = Math.Clamp(value.FlankReturn, 0, 1);
                    value.PeekReturn = Math.Clamp(value.PeekReturn, 0, 1);
                    clean[key] = value;
                }
            }
            _experience = clean;
        }
        catch (Exception e)
        {
            Log.Warning($"Expedition experience could not be loaded; using baseline tactics: {e.Message}");
            _experience.Clear();
        }
        CopyExperienceForRound();
    }

    private void CopyExperienceForRound()
    {
        _roundExperience.Clear();
        foreach (var (key, value) in _experience)
            _roundExperience[key] = new CMUTacticalExperience { Samples = value.Samples, FlankReturn = value.FlankReturn, PeekReturn = value.PeekReturn };
    }

    private string? ExperienceKey(EntityUid uid, CMUExpeditionAgentComponent agent)
    {
        if (Transform(uid).GridUid is not { } grid || !TryComp<CMUExpeditionMapComponent>(grid, out var map))
            return null;
        return $"{map.Plan.Biome}/{agent.Disposition}";
    }

    private void LoadExperience(EntityUid uid, CMUExpeditionAgentComponent agent)
    {
        if (agent.LearningLoaded)
            return;
        agent.LearningLoaded = true;
        if (ExperienceKey(uid, agent) is { } key && _experience.TryGetValue(key, out var value))
        {
            agent.LearnedFlankCost = value.FlankCost;
            agent.LearnedDangerCost = value.DangerCost;
        }
    }

    private void RecordTactic(EntityUid uid, CMUExpeditionAgentComponent agent, bool success, bool flank = true)
    {
        if (ExperienceKey(uid, agent) is not { } key)
            return;
        if (!_roundExperience.TryGetValue(key, out var value))
            _roundExperience[key] = value = new CMUTacticalExperience();
        value.Observe(flank, success);
        _experienceChanged = true;
    }

    private void SaveExperience()
    {
        if (!_experienceChanged)
            return;
        try
        {
            _resources.UserData.WriteAllText(ExperienceFile, JsonSerializer.Serialize(_roundExperience));
            _experienceChanged = false;
        }
        catch (Exception e)
        {
            Log.Warning($"Expedition experience could not be saved: {e.Message}");
        }
    }

    private void OnLearningRoundRestart(RoundRestartCleanupEvent args)
    {
        SaveExperience();
        _experience = _roundExperience;
        _roundExperience = new();
        CopyExperienceForRound();
        _grenadeHazards.Clear();
        _reports.Clear();
    }

    public override void Shutdown()
    {
        SaveExperience();
        base.Shutdown();
    }
}
