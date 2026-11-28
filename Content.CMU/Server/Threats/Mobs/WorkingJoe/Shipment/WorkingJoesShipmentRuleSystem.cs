using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.CMU14.Round.Antags.EarlyWarning;
using Content.Server.CMU14.Threats.GrandMachine;
using Content.Server.CMU14.Threats.Mobs.WorkingJoe.Hijack;
using Content.Server.GameTicking.Rules;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.CMU14.Threats.Mobs.WorkingJoe.Hijack;
using Content.Shared.Database;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Roles;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.CMU14.Threats.Mobs.WorkingJoe.Shipment;

/// <summary>
/// When a Working Joes shipment deploys: the leader is promoted to an Early Warning Joe (or marked to be promoted
/// when a mind arrives), and enough seated members are promoted that the round has
/// <see cref="CMUWorkingJoesShipmentRuleComponent.EWJoeTarget"/> Early Warning Joes counting the rolled ones, but
/// never fewer than the leader. Admin-made and threat-made Early Warning Joes do not reduce the count. Every Early
/// Warning Joe in the round then gets the conversion Offer. Promoted bodies stay threat units.
/// </summary>
public sealed partial class WorkingJoesShipmentRuleSystem : GameRuleSystem<CMUWorkingJoesShipmentRuleComponent>
{
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private IBanManager _ban = default!;
    [Dependency] private EWJoeRuleSystem _earlyWarning = default!;
    [Dependency] private CMUHijackedJoeSystem _hijack = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private GrandMachineRadioSystem _radio = default!;
    [Dependency] private SharedRoleSystem _role = default!;

    /// <summary>The antag whose role bans keep a player from being promoted.</summary>
    private static readonly ProtoId<AntagPrototype> EWJoeAntag = "EWJoeRole";

    private static readonly EntProtoId HijackedJoeMindRole = "CMUMindRoleHijackedJoe";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ThreatDeployedEvent>(OnThreatDeployed);
        SubscribeLocalEvent<CMUPendingEWJoeComponent, MindAddedMessage>(OnPendingMindAdded);
    }

    private void OnThreatDeployed(ref ThreatDeployedEvent args)
    {
        var rules = QueryActiveRules();
        while (rules.MoveNext(out _, out _, out var rule, out _))
        {
            if (rule.ThreatId.Id != args.Threat.ID)
                continue;

            Deploy(rule, args.Leaders, args.Members);
            return;
        }
    }

    /// <summary>
    /// The promotion plan. Public so tests can drive it without the shipment pipeline.
    /// </summary>
    public void Deploy(CMUWorkingJoesShipmentRuleComponent rule, IReadOnlyList<EntityUid> leaders, IReadOnlyList<EntityUid> members)
    {
        var rolled = CountRolledJoes();
        var total = Math.Max(1, rule.EWJoeTarget - rolled);
        var promoted = new HashSet<EntityUid>();
        var pending = 0;

        foreach (var leader in leaders)
        {
            if (TerminatingOrDeleted(leader))
                continue;

            if (!_mind.TryGetMind(leader, out _, out var mind))
            {
                EnsureComp<CMUPendingEWJoeComponent>(leader).StartingLevel = rule.EWJoeStartingLevel;
                pending++;
                continue;
            }

            if (IsBanned(mind))
                continue;

            Promote(leader, rule.EWJoeStartingLevel);
            promoted.Add(leader);
        }

        // nobody can lead: a seated member takes the leader's promotion
        var candidates = members
            .Where(member => !TerminatingOrDeleted(member)
                && _mind.TryGetMind(member, out _, out var mind)
                && !IsBanned(mind))
            .ToList();
        RobustRandom.Shuffle(candidates);

        if (promoted.Count == 0 && pending == 0 && candidates.Count > 0)
        {
            Promote(candidates[0], rule.EWJoeStartingLevel);
            promoted.Add(candidates[0]);
            candidates.RemoveAt(0);
        }

        var extras = total - 1;
        foreach (var member in candidates)
        {
            if (extras <= 0)
                break;

            if (promoted.Contains(member))
                continue;

            Promote(member, rule.EWJoeStartingLevel);
            promoted.Add(member);
            extras--;
        }

        _adminLogger.Add(LogType.AntagSelection, LogImpact.High,
            $"Working Joes shipment deployed: {rolled} rolled Early Warning Joe(s), {promoted.Count} promoted, {pending} pending leader(s), target {rule.EWJoeTarget}");

        GrantConversion();
    }

    /// <summary>Early Warning Joes the antag rules rolled this round; admin-made and threat-made ones do not count.</summary>
    public int CountRolledJoes()
    {
        var count = 0;
        var query = EntityQueryEnumerator<EWJoeRoleComponent>();
        while (query.MoveNext(out var role))
        {
            if (role.Source == EWJoeSource.Rolled)
                count++;
        }

        return count;
    }

    /// <summary>
    /// Every Early Warning Joe, natural ones included, joins the shipment: it can convert dead synthetics with Offer
    /// and speaks on the Grand Machine channel the hijacked Joes use. A natural Joe otherwise only gets that channel
    /// with its first thrall; the channel set is a set, so a later grant by melding changes nothing.
    /// </summary>
    private void GrantConversion()
    {
        var query = EntityQueryEnumerator<EWJoeComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            EnsureComp<CMUJoeConversionComponent>(uid);
            if (!_radio.HasChannel(uid))
                _radio.GrantChannel(uid);
        }
    }

    private void Promote(EntityUid body, int level)
    {
        // promotion needs a mind and an unpromoted body; check first, so a refusal leaves the hijack intact
        if (HasComp<EWJoeComponent>(body) || !_mind.TryGetMind(body, out _, out _))
            return;

        _hijack.ReleaseForPromotion(body);
        _earlyWarning.PromoteToEWJoe(body, level, EWJoeSource.Threat);
        RemCompDeferred<CMUHijackedJoeComponent>(body);
        EnsureComp<CMUJoeThreatUnitComponent>(body);
        EnsureComp<CMUJoeConversionComponent>(body);

        _adminLogger.Add(LogType.AntagSelection, LogImpact.High,
            $"{ToPrettyString(body):body} of the Working Joes shipment was promoted to an Early Warning Joe at level {level}");
    }

    private bool IsBanned(MindComponent mind)
    {
        return mind.UserId is { } user && IsBanned(user);
    }

    private bool IsBanned(NetUserId user)
    {
        return _ban.GetAntagBans(user)?.Contains(EWJoeAntag) == true;
    }

    /// <summary>
    /// The unclaimed leader is promoted as soon as a player takes it. A player banned from the Early Warning Joe
    /// stays a hijacked Joe instead.
    /// </summary>
    private void OnPendingMindAdded(Entity<CMUPendingEWJoeComponent> ent, ref MindAddedMessage args)
    {
        var level = ent.Comp.StartingLevel;
        RemCompDeferred<CMUPendingEWJoeComponent>(ent);

        if (IsBanned(args.Mind.Comp))
        {
            if (!_role.MindHasRole<CMUHijackedJoeRoleComponent>(args.Mind.Owner))
                _role.MindAddRole(args.Mind.Owner, HijackedJoeMindRole, args.Mind.Comp, silent: true);

            _adminLogger.Add(LogType.AntagSelection, LogImpact.Medium,
                $"{ToPrettyString(ent):body} of the Working Joes shipment was not promoted: its player is banned from the Early Warning Joe");
            return;
        }

        Promote(ent, level);
        GrantConversion();
    }
}
