using System.Linq;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Server.Roles.Jobs;
using Content.Server.AU14.Round;
using Content.Shared.Mind;
using Content.Server.GameTicking;
using Content.Shared._RMC14.Rules;
using Content.Shared.Au14.Util;
using Content.Shared.AU14.Threats;
using Content.Shared.AU14.util;
using Content.Shared.CharacterInfo;
using Content.Shared.Inventory;
using Content.Shared.Objectives;
using Content.Shared.Objectives.Components;
using Content.Shared.Objectives.Systems;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.CharacterInfo;

public sealed class CharacterInfoSystem : EntitySystem
{
    [Dependency] private readonly JobSystem _jobs = default!;
    [Dependency] private readonly MindSystem _minds = default!;
    [Dependency] private readonly RoleSystem _roles = default!;
    [Dependency] private readonly SharedObjectivesSystem _objectives = default!;
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly AuRoundSystem _auRound = default!;
    [Dependency] private readonly PlatoonSpawnRuleSystem _platoons = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private int _knowledgeRoundId = -1;
    private string? _roundKnowledgeLine;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<RequestCharacterInfoEvent>(OnRequestCharacterInfoEvent);
    }

    private void OnRequestCharacterInfoEvent(RequestCharacterInfoEvent msg, EntitySessionEventArgs args)
    {
        if (!args.SenderSession.AttachedEntity.HasValue
            || args.SenderSession.AttachedEntity != GetEntity(msg.NetEntity))
            return;

        var entity = args.SenderSession.AttachedEntity.Value;

        var objectives = new Dictionary<string, List<ObjectiveInfo>>();
        var jobTitle = Loc.GetString("character-info-no-profession");
        var lorePrimerLines = new List<string>();
        string? jobId = null;
        string? briefing = null;
        if (_minds.TryGetMind(entity, out var mindId, out var mind))
        {
            // Get objectives
            foreach (var objective in mind.Objectives)
            {
                var info = _objectives.GetInfo(objective, mindId, mind);
                if (info == null)
                    continue;

                // group objectives by their issuer
                var issuer = Comp<ObjectiveComponent>(objective).LocIssuer;
                if (!objectives.ContainsKey(issuer))
                    objectives[issuer] = new List<ObjectiveInfo>();
                objectives[issuer].Add(info.Value);
            }

            if (_jobs.MindTryGetJobName(mindId, out var jobName))
                jobTitle = jobName;

            if (_jobs.MindTryGetJobId(mindId, out var protoId))
                jobId = protoId?.Id;

            // Get briefing
            briefing = _roles.MindGetBriefing(mindId);
        }

        var isThreatRole = mind != null && IsThreatMind(mind);
        PopulateLorePrimerLines(lorePrimerLines, jobId, isThreatRole);

        // Check inventory and hands for JobTitleChangerComponent
        if (EntityManager.TryGetComponent(entity, out InventoryComponent? _))
        {
            var invSys = EntityManager.System<InventorySystem>();
            foreach (var item in invSys.GetHandOrInventoryEntities(entity))
            {
                if (EntityManager.TryGetComponent<JobTitleChangerComponent>(item, out var changer) && !string.IsNullOrWhiteSpace(changer.JobTitle))
                {
                    jobTitle = changer.JobTitle;
                    break;
                }
            }
        }

        // Check uniform accessories (e.g., armbands) for JobTitleChangerComponent
        if (EntityManager.TryGetComponent(entity, out Content.Shared._RMC14.UniformAccessories.UniformAccessoryHolderComponent? accessoryHolder))
        {
            var containerSys = EntityManager.EntitySysManager.GetEntitySystem<Robust.Shared.Containers.SharedContainerSystem>();
            if (containerSys.TryGetContainer(entity, accessoryHolder.ContainerId, out var container))
            {
                foreach (var accessory in container.ContainedEntities)
                {
                    if (EntityManager.TryGetComponent<JobTitleChangerComponent>(accessory, out var changer) && !string.IsNullOrWhiteSpace(changer.JobTitle))
                    {
                        jobTitle = changer.JobTitle;
                        break;
                    }
                }
            }
        }

        RaiseNetworkEvent(new CharacterInfoEvent(GetNetEntity(entity), jobTitle, objectives, briefing, lorePrimerLines), args.SenderSession);
    }

    private void PopulateLorePrimerLines(List<string> lines, string? jobId, bool isThreatRole)
    {
        var presetId = (_ticker.CurrentPreset?.ID ?? _ticker.Preset?.ID ?? string.Empty).ToLowerInvariant();

        var selectedPlanet = _auRound.GetSelectedPlanet();
        var selectedThreat = _auRound._selectedthreat;

        // Keep one deterministic knowledge line for the whole round.
        EnsureRoundKnowledgeLine(selectedThreat);

        switch (presetId)
        {
            case "insurgency":
                AddMapSentence(lines, selectedPlanet);
                AddInsurgencyPlatoonLine(lines, jobId);
                if (isThreatRole)
                    AddThreatRolePrimer(lines);
                break;
            case "colonyfall":
                AddMapSentence(lines, selectedPlanet);
                if (isThreatRole)
                    AddThreatRolePrimer(lines);
                break;
            case "distresssignal":
                AddDistressPlanetAndMap(lines, selectedPlanet);
                AddInsurgencyPlatoonLine(lines, jobId);

                if (isThreatRole)
                {
                    AddThreatRolePrimer(lines);
                }
                else
                {
                    AddRoundKnowledgeLine(lines);
                }
                break;
            case "forceonforce":
                AddPlatoonLine(lines, _platoons.SelectedGovforPlatoon);
                AddPlatoonLine(lines, _platoons.SelectedOpforPlatoon);
                break;
            default:
                AddMapSentence(lines, selectedPlanet);
                break;
        }

        // Keep output concise and stable.
        lines.RemoveAll(string.IsNullOrWhiteSpace);
        if (lines.Count > 0)
        {
            var uniqueLines = lines.Distinct().ToList();
            lines.Clear();
            lines.AddRange(uniqueLines);
        }
    }

    private void EnsureRoundKnowledgeLine(ThreatPrototype? selectedThreat)
    {
        if (_knowledgeRoundId == _ticker.RoundId)
            return;

        _knowledgeRoundId = _ticker.RoundId;
        _roundKnowledgeLine = null;

        if (selectedThreat == null || selectedThreat.LorePrimer is not { } primerId)
            return;

        if (!_prototypes.TryIndex(primerId, out LorePrimerPrototype? primer) || primer.KnowledgeLevels is null || primer.KnowledgeLevels.Count == 0)
            return;

        var levels = primer.KnowledgeLevels.Values.Distinct().ToList();
        if (levels.Count == 0)
            return;

        var selectedLevel = _random.Pick(levels);
        var candidates = primer.KnowledgeLevels
            .Where(kv => kv.Value <= selectedLevel)
            .Select(kv => kv.Key)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToList();

        if (candidates.Count == 0)
            return;

        _roundKnowledgeLine = _random.Pick(candidates);
    }

    private void AddRoundKnowledgeLine(List<string> lines)
    {
        if (!string.IsNullOrWhiteSpace(_roundKnowledgeLine))
            lines.Add(_roundKnowledgeLine);
    }

    private void AddInsurgencyPlatoonLine(List<string> lines, string? jobId)
    {
        if (string.IsNullOrWhiteSpace(jobId))
            return;

        if (_jobs.TryGetDepartment(jobId, out var department) && department.Faction is { } faction)
        {
            if (faction.Equals("govfor", StringComparison.OrdinalIgnoreCase))
            {
                AddPlatoonLine(lines, _platoons.SelectedGovforPlatoon);
                return;
            }

            if (faction.Equals("opfor", StringComparison.OrdinalIgnoreCase))
            {
                AddPlatoonLine(lines, _platoons.SelectedOpforPlatoon);
                return;
            }
        }

        if (jobId.Contains("GOVFOR", StringComparison.OrdinalIgnoreCase))
            AddPlatoonLine(lines, _platoons.SelectedGovforPlatoon);
        else if (jobId.Contains("OPFOR", StringComparison.OrdinalIgnoreCase))
            AddPlatoonLine(lines, _platoons.SelectedOpforPlatoon);
    }

    private void AddDistressPlanetAndMap(List<string> lines, RMCPlanetMapPrototypeComponent? selectedPlanet)
    {
        AddMapSentence(lines, selectedPlanet);
    }

    private void AddMapSentence(List<string> lines, RMCPlanetMapPrototypeComponent? selectedPlanet)
    {
        if (selectedPlanet == null)
            return;

        if (selectedPlanet.LorePrimer is { } planetPrimerId &&
            _prototypes.TryIndex(planetPrimerId, out LorePrimerPrototype? primer) &&
            !string.IsNullOrWhiteSpace(primer.PlanetText))
        {
            lines.Add(primer.PlanetText);
            return;
        }

        if (!string.IsNullOrWhiteSpace(selectedPlanet.Announcement))
            lines.Add(selectedPlanet.Announcement);
    }

    private void AddPlatoonLine(List<string> lines, PlatoonPrototype? platoon)
    {
        if (platoon == null)
            return;

        if (platoon.LorePrimer is { } platoonPrimerId &&
            _prototypes.TryIndex(platoonPrimerId, out LorePrimerPrototype? primer) &&
            !string.IsNullOrWhiteSpace(primer.PlatoonInfo))
        {
            var platoonInfo = primer.PlatoonInfo.Trim();
            lines.Add(platoonInfo.StartsWith("Platoon:", StringComparison.OrdinalIgnoreCase)
                ? platoonInfo
                : $"Platoon: {platoonInfo}");
            return;
        }

        if (!string.IsNullOrWhiteSpace(platoon.Name))
            lines.Add($"Platoon: {platoon.Name}");
    }

    private bool IsThreatMind(MindComponent mind)
    {
        // Threat players may keep a previous job role; check all job role entries on the mind.
        foreach (var roleUid in mind.MindRoles)
        {
            if (!TryComp<MindRoleComponent>(roleUid, out var roleComp))
                continue;

            if (roleComp.JobPrototype == "AU14JobThreatLeader" || roleComp.JobPrototype == "AU14JobThreatMember")
                return true;
        }

        return false;
    }

    private void AddThreatRolePrimer(List<string> lines)
    {

        lines.Add("You are aligned with the active threat. Keep your identity and goals in mind.");
    }

}

