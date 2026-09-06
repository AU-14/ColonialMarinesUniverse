using Content.Server.Antag;
using Content.Server.Antag.Components;
using Content.Server._CMU14.Round.Antags.Cannibal;
using Content.Server._CMU14.Round.Antags.StrikeOrganizer;
using Content.Server.GameTicking;
using Content.Server.Roles;
using Content.Shared._CMU14.Round.Antags.Arsonist;
using Content.Shared._CMU14.Round.Antags.BountyHunter;
using Content.Shared._CMU14.Round.Antags.CLFSaboteur;
using Content.Shared._CMU14.Round.Antags.ColonyBounty;
using Content.Shared._CMU14.Round.Antags.CorporateAgent;
using Content.Shared._CMU14.Round.Antags.StrikeOrganizer;
using Content.Shared._CMU14.Round.Antags.Vigilante;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.GameObjects;

namespace Content.Server._CMU14.Round.Antags;

/// <summary>
/// Appends colony antag outcomes to the round end summary: who played which antag
/// and, where tracked, what they achieved.
/// </summary>
public sealed partial class ColonyAntagSummarySystem : EntitySystem
{
    [Dependency] private readonly AntagSelectionSystem _antag = default!;
    [Dependency] private readonly RoleSystem _role = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEndText);
    }

    private void OnRoundEndText(RoundEndTextAppendEvent args)
    {
        var lines = new List<string>();
        var petitionCovered = false;

        var rules = EntityManager.AllEntityQueryEnumerator<AntagSelectionComponent>();
        while (rules.MoveNext(out var ruleUid, out _))
        {
            if (MetaData(ruleUid).EntityPrototype is not { } proto
                || !ColonyAntagsRuleSystem.AntagRulePrototypes.ContainsKey(proto.ID))
                continue;

            foreach (var (mind, data, name) in _antag.GetAntagIdentifiers(ruleUid))
            {
                var mindComp = EntityManager.GetComponentOrNull<MindComponent>(mind);
                var charName = mindComp?.CharacterName ?? name;
                var role = "Unknown";
                foreach (var info in _role.MindGetAllRoleInfo(mind))
                {
                    if (!info.Antagonist)
                        continue;
                    role = Loc.GetString(info.Name);
                    break;
                }

                string detail;
                if (mindComp?.OwnedEntity is { } strikeBody
                    && EntityManager.HasComponent<StrikeOrganizerComponent>(strikeBody)
                    && BestPetition() is { } petition)
                {
                    petitionCovered = true;
                    detail = Loc.GetString("cmu-summary-detail-strike",
                        ("count", petition.count), ("goal", petition.goal));
                }
                else
                    detail = DetailFor(mindComp?.OwnedEntity);

                lines.Add(Loc.GetString("cmu-summary-entry",
                    ("name", charName),
                    ("user", data.UserName),
                    ("role", role),
                    ("detail", detail)));
            }
        }

        if (!petitionCovered)
        {
            var petitions = EntityManager.AllEntityQueryEnumerator<StrikePetitionComponent>();
            while (petitions.MoveNext(out _, out var petition))
            {
                lines.Add(Loc.GetString("cmu-summary-petition",
                    ("count", petition.Signatures.Count), ("goal", petition.Goal)));
            }
        }

        if (lines.Count == 0)
            return;

        args.AddLine("[color=#b0901b][bold]Colony Underworld[/bold][/color]");
        foreach (var line in lines)
            args.AddLine(line);
    }

    private string DetailFor(EntityUid? body)
    {
        if (body == null)
            return Loc.GetString("cmu-summary-detail-none");

        if (EntityManager.TryGetComponent<ArsonistComponent>(body, out var arsonist)
            && arsonist.FiresCount > 0)
            return Loc.GetString("cmu-summary-detail-arsonist", ("count", arsonist.FiresCount));

        if (EntityManager.TryGetComponent<CorporateAgentComponent>(body, out var agent))
            return agent.Completed
                ? Loc.GetString("cmu-summary-detail-agent-complete", ("corporation", agent.Corporation))
                : Loc.GetString("cmu-summary-detail-agent-failed", ("corporation", agent.Corporation));

        if (EntityManager.TryGetComponent<CLFSaboteurComponent>(body, out var saboteur)
            && saboteur.Count > 0)
            return Loc.GetString("cmu-summary-detail-saboteur", ("count", saboteur.Count));

        if (EntityManager.TryGetComponent<CannibalComponent>(body, out var cannibal)
            && cannibal.MealsEaten > 0)
            return Loc.GetString("cmu-summary-detail-cannibal", ("count", cannibal.MealsEaten));

        if (EntityManager.TryGetComponent<BountyHunterComponent>(body, out var hunter))
            return Loc.GetString("cmu-summary-detail-hunter", ("count", hunter.TargetCount));

        if (EntityManager.TryGetComponent<VigilanteComponent>(body, out var vigilante))
            return Loc.GetString("cmu-summary-detail-vigilante", ("count", vigilante.TargetCount));

        if (EntityManager.TryGetComponent<ColonyBountyComponent>(body, out var bounty))
        {
            if (bounty.Paid)
                return bounty.Captured
                    ? Loc.GetString("cmu-summary-detail-bounty-captured")
                    : Loc.GetString("cmu-summary-detail-bounty-killed");
            if (IsDead(body.Value))
                return Loc.GetString("cmu-summary-detail-bounty-dead");
            return Loc.GetString("cmu-summary-detail-bounty-free");
        }

        return IsDead(body.Value)
            ? Loc.GetString("cmu-summary-detail-fate-dead")
            : Loc.GetString("cmu-summary-detail-none");
    }

    private (int count, int goal)? BestPetition()
    {
        (int count, int goal)? best = null;
        var petitions = EntityManager.AllEntityQueryEnumerator<StrikePetitionComponent>();
        while (petitions.MoveNext(out _, out var petition))
        {
            if (best == null || petition.Signatures.Count > best.Value.count)
                best = (petition.Signatures.Count, petition.Goal);
        }
        return best;
    }

    private bool IsDead(EntityUid body)
        => EntityManager.TryGetComponent<MobStateComponent>(body, out var mob)
            && mob.CurrentState is MobState.Dead or MobState.Invalid;
}
