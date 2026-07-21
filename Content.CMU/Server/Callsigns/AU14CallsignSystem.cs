using Content.Server._RMC14.Marines.Roles.Ranks;
using Content.Server.Chat.Systems;
using Content.Server.Radio.EntitySystems;
using Content.Shared._AU14.CCVar;
using Content.Shared._AU14.Callsigns;
using Content.Shared._AU14.Radio;
using Content.Shared._CMU14.Threats.Mobs.CLF;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Marines.Squads;
using Content.Shared._RMC14.Tracker.SquadLeader;
using Content.Shared.Chat;
using Content.Shared.Examine;
using Content.Shared.GameTicking;
using Content.Shared.Radio.Components;
using Content.Shared.Roles;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._AU14.Callsigns;

// assigns radio callsigns from job and squad (6 = leader, 5 = 2IC, 7 = senior NCO,
// ROMEO = RTO, OPS = staff, 1-N = everyone else), masks names with the callsign on
// faction radio and serves the directory console
public sealed partial class AU14CallsignSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IConfigurationManager _config = default!;

    private static readonly HashSet<string> CallsignFactions = ["govfor", "opfor", "clf"];

    private static readonly Dictionary<string, string> DefaultCommandWords = new()
    {
        ["govfor"] = "HAVOC",
        ["opfor"] = "VICTOR",
        ["clf"] = "CELL",
    };

    private readonly Dictionary<string, string> _commandWords = new();
    private readonly Dictionary<EntityUid, string> _squadWords = new();

    // custom callsign groups created from the directory console, per faction
    private readonly Dictionary<string, List<string>> _groups = new();

    // role sections carry their own element words, renamable per faction
    private static readonly Dictionary<string, string> DefaultCategoryWords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AIR"] = "TALON",
        ["ARMOR"] = "DRAGOON",
        ["MP"] = "WARDEN",
        ["MEDICAL"] = "DUSTOFF",
        ["INTEL"] = "PROPHET",
        ["SYNTH"] = "APOLLO",
    };

    private readonly Dictionary<(string Faction, string Category), string> _categoryWords = new();

    private readonly List<(EntityUid Mob, LocId Prefix, LocId? Additional, GameTick Tick)> _prefixRestores = new();

    private bool _commsEnabled;

    public override void Initialize()
    {
        Subs.CVar(_config, AU14CCVars.NewCommsSystem, OnCommsToggled, true);

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeLocalEvent<SquadMemberAddedEvent>(OnSquadMemberAdded);
        SubscribeLocalEvent<FireteamMemberUpdatedEvent>(OnFireteamMemberUpdated);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);

        // mid-round CLF recruits (tattoo gun, admin verb) never see PlayerSpawnCompleteEvent
        SubscribeLocalEvent<CLFMemberComponent, ComponentStartup>(OnCLFMemberStartup);

        // check your own callsign by looking at your headset instead of keying the net
        SubscribeLocalEvent<HeadsetComponent, ExaminedEvent>(OnHeadsetExamined);

        // deleted or despawned mobs must drop off open directory consoles
        SubscribeLocalEvent<AU14CallsignComponent, ComponentShutdown>(OnCallsignShutdown);

        SubscribeLocalEvent<AU14CallsignComponent, EntitySpokeEvent>(
            OnCallsignSpeak,
            before: [typeof(HeadsetSystem)]);

        // after RankSystem, which rewrites VoiceName to "RANK Name"
        SubscribeLocalEvent<AU14CallsignComponent, TransformSpeakerNameEvent>(
            OnCallsignSpeakerName,
            after: [typeof(RankSystem)]);

        InitializeConsole();
    }

    public override void Update(float frameTime)
    {
        if (_prefixRestores.Count == 0)
            return;

        // job prefixes stripped for a transmission get restored the next tick, the send
        // itself happens synchronously inside the speak event
        var tick = _timing.CurTick;

        for (var i = _prefixRestores.Count - 1; i >= 0; i--)
        {
            var (mob, prefix, additional, strippedTick) = _prefixRestores[i];

            if (strippedTick >= tick)
                continue;

            _prefixRestores.RemoveAt(i);

            if (TerminatingOrDeleted(mob))
                continue;

            var restored = EnsureComp<JobPrefixComponent>(mob);
            restored.Prefix = prefix;
            restored.AdditionalPrefix = additional;
            Dirty(mob, restored);
        }
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _commandWords.Clear();
        _squadWords.Clear();
        _groups.Clear();
        _categoryWords.Clear();
        _prefixRestores.Clear();
    }

    private void OnCLFMemberStartup(Entity<CLFMemberComponent> ent, ref ComponentStartup args)
    {
        // round-start CLF jobs have no session attached yet and are handled by
        // OnPlayerSpawnComplete; this catches players converted mid-round
        if (!_commsEnabled || !HasComp<ActorComponent>(ent.Owner))
            return;

        TryAssignSwept(ent.Owner, "clf");
    }

    private void OnFireteamMemberUpdated(ref FireteamMemberUpdatedEvent ev)
    {
        if (!_commsEnabled || !TryComp(ev.Member, out AU14CallsignComponent? callsign))
            return;

        Assign(ev.Member, callsign);
    }

    private void OnHeadsetExamined(Entity<HeadsetComponent> ent, ref ExaminedEvent args)
    {
        if (!_commsEnabled ||
            !TryComp(args.Examiner, out AU14CallsignComponent? callsign) ||
            string.IsNullOrEmpty(callsign.Callsign))
        {
            return;
        }

        args.PushMarkup(Loc.GetString("au14-callsign-headset-examine", ("callsign", callsign.Callsign)));
    }

    private void OnCommsToggled(bool enabled)
    {
        _commsEnabled = enabled;

        if (!enabled)
            return;

        // cvar flipped on mid-round, sweep up everyone who spawned while it was off
        var marines = EntityQueryEnumerator<MarineComponent, ActorComponent>();

        while (marines.MoveNext(out var uid, out var marine, out _))
        {
            TryAssignSwept(uid, marine.Faction);
        }

        var clf = EntityQueryEnumerator<CLFMemberComponent, ActorComponent>();

        while (clf.MoveNext(out var uid, out _, out _))
        {
            TryAssignSwept(uid, "clf");
        }
    }

    private void TryAssignSwept(EntityUid mob, string? faction)
    {
        if (faction == null || !CallsignFactions.Contains(faction))
            return;

        if (TryComp(mob, out AU14CallsignComponent? existing) &&
            !string.IsNullOrEmpty(existing.Callsign))
        {
            return;
        }

        var callsign = EnsureComp<AU14CallsignComponent>(mob);
        callsign.Faction = faction;

        Assign(mob, callsign);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        if (!_commsEnabled)
            return;

        // CLF fighters carry CLFMember instead of a marine faction
        var faction = HasComp<CLFMemberComponent>(ev.Mob)
            ? "clf"
            : CompOrNull<MarineComponent>(ev.Mob)?.Faction;

        if (faction == null || !CallsignFactions.Contains(faction))
            return;

        var callsign = EnsureComp<AU14CallsignComponent>(ev.Mob);
        callsign.Faction = faction;

        if (ev.JobId != null && _prototype.TryIndex<JobPrototype>(ev.JobId, out var job))
            callsign.JobTitle = job.LocalizedName;

        Assign(ev.Mob, callsign);
    }

    private void OnSquadMemberAdded(ref SquadMemberAddedEvent ev)
    {
        if (!TryComp(ev.Member, out AU14CallsignComponent? callsign))
            return;

        Assign(ev.Member, callsign);
    }

    private void OnCallsignShutdown(Entity<AU14CallsignComponent> ent, ref ComponentShutdown args)
    {
        if (!string.IsNullOrEmpty(ent.Comp.Faction))
            PushConsoleStates(ent.Comp.Faction);
    }

    private void Assign(EntityUid uid, AU14CallsignComponent callsign)
    {
        var role = CompOrNull<AU14CallsignRoleComponent>(uid);

        // diplomats and similar roles stay off the net entirely
        if (role is { Exempt: true })
        {
            RemCompDeferred<AU14CallsignComponent>(uid);
            return;
        }

        callsign.Category = role?.Category;

        EntityUid? squad = null;

        if (role is not { CommandElement: true } &&
            CompOrNull<SquadMemberComponent>(uid)?.Squad is { } memberSquad &&
            HasComp<SquadTeamComponent>(memberSquad))
        {
            squad = memberSquad;
        }

        callsign.Squad = squad;

        if (role != null && !string.IsNullOrEmpty(role.Suffix))
        {
            callsign.Suffix = MakeUniqueSuffix(callsign.Faction, squad, callsign.Group, callsign.Category, role.Suffix, uid);
            callsign.RoleSuffix = true;
        }
        else if (!callsign.RoleSuffix || string.IsNullOrEmpty(callsign.Suffix))
        {
            callsign.Suffix = NextFreeNumber(callsign.Faction, squad, callsign.Group, callsign.Category, FireteamNumber(uid), uid);
            callsign.RoleSuffix = false;
        }
        else
        {
            // manually pinned suffix follows them into the new element
            callsign.Suffix = MakeUniqueSuffix(callsign.Faction, squad, callsign.Group, callsign.Category, callsign.Suffix, uid);
        }

        UpdateFullCallsign(uid, callsign);
    }

    // fireteam index becomes the first half of the numeric suffix: fireteam 2's
    // riflemen are "2-1", "2-2"; marines without a fireteam stay in the "1-N" block
    private int FireteamNumber(EntityUid uid)
    {
        return CompOrNull<FireteamMemberComponent>(uid)?.Fireteam + 1 ?? 1;
    }

    private void UpdateFullCallsign(EntityUid uid, AU14CallsignComponent callsign)
    {
        var word = GetElementWord(callsign);

        callsign.Callsign = $"{word} {callsign.Suffix}";

        PushConsoleStates(callsign.Faction);
    }

    // word precedence: task group, then role section, then squad, then command
    public string GetElementWord(AU14CallsignComponent callsign)
    {
        if (callsign.Group != null)
            return callsign.Group;

        if (callsign.Category != null)
            return GetCategoryWord(callsign.Faction, callsign.Category);

        return callsign.Squad is { } squad
            ? GetSquadWord(squad)
            : GetCommandWord(callsign.Faction);
    }

    public string GetCommandWord(string faction)
    {
        if (_commandWords.TryGetValue(faction, out var word))
            return word;

        return DefaultCommandWords.TryGetValue(faction, out var fallback)
            ? fallback
            : faction.ToUpperInvariant();
    }

    public string GetCategoryWord(string faction, string category)
    {
        if (_categoryWords.TryGetValue((faction, category.ToUpperInvariant()), out var word))
            return word;

        return DefaultCategoryWords.TryGetValue(category, out var fallback)
            ? fallback
            : category.ToUpperInvariant();
    }

    public string GetSquadWord(EntityUid squad)
    {
        if (_squadWords.TryGetValue(squad, out var word))
            return word;

        return Name(squad).ToUpperInvariant();
    }

    private string NextFreeNumber(string faction, EntityUid? squad, string? group, string? category, int fireteam, EntityUid exclude)
    {
        for (var n = 1;; n++)
        {
            var candidate = $"{fireteam}-{n}";

            if (!SuffixTaken(faction, squad, group, category, candidate, exclude))
                return candidate;
        }
    }

    private string MakeUniqueSuffix(string faction, EntityUid? squad, string? group, string? category, string wanted, EntityUid exclude)
    {
        if (!SuffixTaken(faction, squad, group, category, wanted, exclude))
            return wanted;

        for (var n = 2;; n++)
        {
            var candidate = $"{wanted} {n}";

            if (!SuffixTaken(faction, squad, group, category, candidate, exclude))
                return candidate;
        }
    }

    // suffixes are unique within their element: a custom group when set, then the
    // role section, then the squad, then the command element
    private bool SuffixTaken(string faction, EntityUid? squad, string? group, string? category, string suffix, EntityUid exclude)
    {
        var query = EntityQueryEnumerator<AU14CallsignComponent>();

        while (query.MoveNext(out var uid, out var other))
        {
            if (uid == exclude)
                continue;

            if (other.Faction != faction ||
                !string.Equals(other.Group, group, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (group == null &&
                !string.Equals(other.Category, category, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (group == null && category == null && other.Squad != squad)
                continue;

            if (string.Equals(other.Suffix, suffix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    // flag the transmission for name masking and strip the job prefix so the radio line
    // is just the callsign. the ANPRC transmit path does its own masking, whichever
    // handler runs first wins safely
    private void OnCallsignSpeak(Entity<AU14CallsignComponent> ent, ref EntitySpokeEvent args)
    {
        if (!_commsEnabled || args.Channel == null || string.IsNullOrEmpty(ent.Comp.Callsign))
            return;

        ent.Comp.RadioMaskTick = _timing.CurTick;

        if (TryComp(ent.Owner, out JobPrefixComponent? prefix))
        {
            _prefixRestores.Add((ent.Owner, prefix.Prefix, prefix.AdditionalPrefix, _timing.CurTick));
            RemComp<JobPrefixComponent>(ent.Owner);
        }
    }

    private void OnCallsignSpeakerName(Entity<AU14CallsignComponent> ent, ref TransformSpeakerNameEvent args)
    {
        if (ent.Comp.RadioMaskTick != _timing.CurTick || string.IsNullOrEmpty(ent.Comp.Callsign))
            return;

        // mid ANPRC transmission the manpack's station callsign wins
        if (TryComp(ent.Owner, out WearingANPRCComponent? wearing) &&
            TryComp(wearing.Radio, out ANPRCRadioComponent? radio) &&
            radio.NameMaskActive)
        {
            return;
        }

        // squad leaders and fireteam leaders stay identifiable to new players:
        // "(SL) ALPHA 6", "(FTL) ALPHA 2-1"
        var tag = CompOrNull<AU14CallsignRoleComponent>(ent.Owner)?.RadioTag;

        if (tag == null && HasComp<FireteamLeaderComponent>(ent.Owner))
            tag = "FTL";

        args.VoiceName = tag == null
            ? ent.Comp.Callsign
            : $"({tag}) {ent.Comp.Callsign}";
    }

}
