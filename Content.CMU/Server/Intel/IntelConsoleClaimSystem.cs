using Content.Server._CMU14.Threats.Rules;
using Content.Server.AU14.Comms;
using Content.Server.AU14.Systems;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Server.Roles.Jobs;
using Content.Server._RMC14.Marines;
using Content.Shared._CMU14.Intel;
using Content.Shared._RMC14.Intel;
using Content.Shared.CCVar;
using Content.Shared._RMC14.Marines;
using Content.Shared.GameTicking;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC.Components;
using Content.Shared.Paper;
using Content.Shared.Popups;
using Robust.Shared.Configuration;

namespace Content.Server._CMU14.Intel;

public sealed partial class IntelConsoleClaimSystem : EntitySystem
{
    [Dependency] private ColonyCommsConsoleSystem _colonyComms = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private JobSystem _jobs = default!;
    [Dependency] private MarineAnnounceSystem _marineAnnounce = default!;
    [Dependency] private MindSystem _mindSys = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private ThreatRuleHelper _threatRuleHelper = default!;
    [Dependency] private WantedSystem _wantedSys = default!;

    private const string MarshalBureauFaxGroup = "marshal-bureau";
    private const string MilitaryFaxGroup = "military-command";
    private const string WarshipFaxGroup = "warship-command";
    private const string GovforFaxTitle = "CLF Cell Compromised";
    private const string GovforFaxStampState = "paper_stamp-provost-inspector";
    private const string GovforFaxPaperPrototype = "CMUPaperPROVOST";
    private bool _huntTriggered;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        SubscribeLocalEvent<ClaimableIntelConsoleComponent, IntelConsoleClaimDoAfterEvent>(OnClaimDoAfter);
    }

    private string? GetCurrentPresetId() => _ticker.CurrentPreset?.ID ?? _ticker.Preset?.ID;
    private void OnRoundRestartCleanup(RoundRestartCleanupEvent _) => _huntTriggered = false;

    private void OnClaimDoAfter(Entity<ClaimableIntelConsoleComponent> ent, ref IntelConsoleClaimDoAfterEvent args)
    {
        if (args.Handled) return;
        args.Handled = true;

        if (args.Cancelled
                || _huntTriggered
                || !HasComp<IntelConsoleComponent>(ent)
                || !TryComp(args.User, out MarineComponent? marine)
                || !ent.Comp.CanCompleteClaim(marine.Faction, GetCurrentPresetId()))
            return;

        ent.Comp.Claimed = true;
        _huntTriggered = true;
        Dirty(ent);

        _popup.PopupEntity(
            Loc.GetString("cmu-intel-console-claim-complete"),
            ent.Owner,
            args.User,
            PopupType.Large);
        Logger.GetSawmill("objectives").Info(
            $"{ToPrettyString(args.User):user} claimed {ToPrettyString(ent):console} for team '{ent.Comp.ClaimingTeam}'");

        TriggerClfHunt(ent);
    }

    private void TriggerClfHunt(Entity<ClaimableIntelConsoleComponent> ent)
    {
        var roster = BuildClfRoster();
        var faxContent = IntelConsoleClaimFax.BuildGovforFaxContent(roster);
        var stampedBy = new List<StampDisplayInfo>
        {
            new() { StampedColor = Color.FromHex("#cb0000"), StampedName = "HUMINT CLASSIFIED" }
        };

        var faxDelivered = _wantedSys.SendFaxToGroup(MarshalBureauFaxGroup, GovforFaxTitle, faxContent, GovforFaxStampState, stampedBy, GovforFaxPaperPrototype);
        faxDelivered |= _wantedSys.SendFaxToGroup(MilitaryFaxGroup, GovforFaxTitle, faxContent, GovforFaxStampState, stampedBy, GovforFaxPaperPrototype);
        faxDelivered |= _wantedSys.SendFaxToGroup(WarshipFaxGroup, GovforFaxTitle, faxContent, GovforFaxStampState, stampedBy, GovforFaxPaperPrototype);

        var forceAnnouncement = _cfg.GetCVar(CCVars.CMUIntelClaimForceGovforAnnouncement);
        if (forceAnnouncement || !faxDelivered)
        {
            _marineAnnounce.AnnounceToMarines(
                IntelConsoleClaimFax.BuildGovforAnnouncementText(roster),
                null,
                faction: ent.Comp.ClaimingTeam);
        }

        _colonyComms.BroadcastColonyAlert(ent.Owner, IntelConsoleClaimFax.BuildColonyAnnouncementText());
    }

    private List<ClfRosterEntry> BuildClfRoster()
    {
        var roster = new List<ClfRosterEntry>();
        var query = EntityQueryEnumerator<MobStateComponent, NpcFactionMemberComponent>();
        while (query.MoveNext(out var uid, out var mobState, out var faction))
        {
            if (!ThreatRuleHelper.HasFaction(faction, "clf"))
                continue;

            if (_threatRuleHelper.IsExcludedFromVictory(uid, mobState))
                continue;

            if (_threatRuleHelper.IsEvacuated(uid))
                continue;

            if (!_mindSys.TryGetMind(uid, out var mindId, out _))
                continue;

            roster.Add(new ClfRosterEntry(MetaData(uid).EntityName, _jobs.MindTryGetJobName(mindId)));
        }

        roster.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return roster;
    }
}
