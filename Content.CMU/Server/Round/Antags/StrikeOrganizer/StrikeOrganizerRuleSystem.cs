using Content.Server.Administration.Logs;
using Content.Server.Antag;
using Content.Server.CMU14.Systems;
using Content.Server.CMU14.Round.Antags.ColonyBounty;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Shared.CMU14.Round.Antags.StrikeOrganizer;
using Content.Shared.Database;
using Content.Shared.Dataset;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Roles;
using Content.Shared.Roles.Components;
using Robust.Server.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.CMU14.Round.Antags.StrikeOrganizer;

public sealed partial class StrikeOrganizerRuleSystem : GameRuleSystem<StrikeOrganizerRuleComponent>
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private AntagSelectionSystem _antag = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private WantedSystem _wantedSystem = default!;

    private static readonly StrikeStance[] Stances =
    {
        StrikeStance.Aligned,
        StrikeStance.Neutral,
        StrikeStance.Opposed,
    };

    public static readonly ProtoId<DatasetPrototype> GrievanceDataset = "CMUStrikeGrievances";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StrikeOrganizerComponent, ComponentStartup>(OnStrikeOrganizerSpawned);
    }

    protected override void Started(EntityUid uid, StrikeOrganizerRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        // Roll before base.Started: organizer gear and the petition spawn during selection,
        // and everything reads this venture from the rule
        var roll = RollVenture(_random, _proto);
        component.Grievance = roll.Grievance;
        component.ClfStance = roll.Clf;
        component.GovforStance = roll.Govfor;
        component.VentureRolled = true;

        _adminLog.Add(LogType.AntagSelection, LogImpact.Medium,
            $"Strike venture rolled: grievance '{roll.Grievance}', CLF {roll.Clf}, GOVFOR {roll.Govfor}");

        base.Started(uid, component, gameRule, args);
    }

    public static (string Grievance, StrikeStance Clf, StrikeStance Govfor) RollVenture(
        IRobustRandom random, IPrototypeManager proto)
        => (random.Pick(proto.Index(GrievanceDataset).Values), random.Pick(Stances), random.Pick(Stances));

    private void OnStrikeOrganizerSpawned(EntityUid uid, StrikeOrganizerComponent component, ComponentStartup args)
    {
        _wantedSystem.SendPaperToGroup(ColonyCmbFax.MarshalBureauFaxGroup, "AUPaperStrikeOrganizer");

        if (ResolveVenture() is not { } venture)
            return;

        // The venture owns its organizer so petitions, faxes, and succession survive any single paper
        venture.Organizer ??= uid;

        var (grievance, clf, govfor) = StrikeVentureText.Compose(Loc, venture.Grievance, venture.ClfStance, venture.GovforStance);
        _antag.SendBriefing(uid,
            Loc.GetString("cmu-strike-organizer-venture",
                ("grievance", grievance), ("clfStance", clf), ("govforStance", govfor)),
            venture.BriefingColor, venture.BriefingSound);
    }

    /// <summary>
    /// The colony antag summary walks round-start AntagSelection rosters, so mid-round
    /// converts never appear there. Mind-role identities survive gibbed bodies, and the
    /// status report rides on them for the same reason.
    /// </summary>
    protected override void AppendRoundEndText(EntityUid uid, StrikeOrganizerRuleComponent component, GameRuleComponent gameRule, ref RoundEndTextAppendEvent args)
    {
        if (ResolveVenture() is not { } venture)
            return;

        List<string>? entries = null;

        var roles = EntityQueryEnumerator<MindRoleComponent>();
        while (roles.MoveNext(out var roleUid, out _))
        {
            if (MetaData(roleUid).EntityPrototype is not { } proto
                || proto.ID != venture.ParticipantMindRole.Id)
                continue;

            // Mind roles sit in a container on their mind, so the parent is the mind itself
            if (!TryComp<TransformComponent>(roleUid, out var xform)
                || !TryComp<MindComponent>(xform.ParentUid, out var mind))
                continue;

            var status = mind.OwnedEntity is { } body
                && TryComp<MobStateComponent>(body, out var mobState)
                && mobState.CurrentState != MobState.Dead
                ? Loc.GetString("cmu-strike-summary-alive")
                : Loc.GetString("cmu-strike-summary-dead");

            var userName = mind.UserId is { } userId
                && _playerManager.TryGetPlayerData(userId, out var playerData)
                ? playerData.UserName
                : string.Empty;

            entries ??= new List<string>();
            entries.Add(Loc.GetString("cmu-strike-summary-entry",
                ("name", mind.CharacterName ?? string.Empty),
                ("user", userName),
                ("status", status)));
        }

        if (entries is null)
            return;

        args.AddLine(string.Empty);
        args.AddLine(Loc.GetString("cmu-strike-summary-header"));
        foreach (var entry in entries)
            args.AddLine(entry);
    }

    /// <summary>
    /// The round's rolled venture, or null before the rule starts. Petitions that find
    /// no venture are relics of a finished strike. Single owner of venture resolution.
    /// </summary>
    public StrikeOrganizerRuleComponent? ResolveVenture()
    {
        StrikeOrganizerRuleComponent? found = null;
        var query = EntityQueryEnumerator<StrikeOrganizerRuleComponent>();
        while (query.MoveNext(out _, out var rule))
        {
            if (rule.VentureRolled)
            {
                found = rule;
                break;
            }
        }

        return found;
    }
}
