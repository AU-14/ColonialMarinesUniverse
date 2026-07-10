using Content.Server._RMC14.Marines.Roles.Ranks;
using Content.Server.Chat.Systems;
using Content.Server.Radio.EntitySystems;
using Content.Shared._AU14.CCVar;
using Content.Shared._AU14.Callsigns;
using Content.Shared._AU14.Radio;
using Content.Shared._CMU14.Threats.Mobs.CLF;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Marines.Squads;
using Content.Shared.Chat;
using Content.Shared.GameTicking;
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

    private readonly List<(EntityUid Mob, LocId Prefix, LocId? Additional, GameTick Tick)> _prefixRestores = new();

    private bool _commsEnabled;

    public override void Initialize()
    {
        Subs.CVar(_config, AU14CCVars.NewCommsSystem, OnCommsToggled, true);

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeLocalEvent<SquadMemberAddedEvent>(OnSquadMemberAdded);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);

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
        _prefixRestores.Clear();
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

    private void Assign(EntityUid uid, AU14CallsignComponent callsign)
    {
        var role = CompOrNull<AU14CallsignRoleComponent>(uid);

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
            callsign.Suffix = MakeUniqueSuffix(callsign.Faction, squad, role.Suffix, uid);
            callsign.RoleSuffix = true;
        }
        else if (!callsign.RoleSuffix || string.IsNullOrEmpty(callsign.Suffix))
        {
            callsign.Suffix = NextFreeNumber(callsign.Faction, squad, uid);
            callsign.RoleSuffix = false;
        }
        else
        {
            // manually pinned suffix follows them into the new element
            callsign.Suffix = MakeUniqueSuffix(callsign.Faction, squad, callsign.Suffix, uid);
        }

        UpdateFullCallsign(uid, callsign);
    }

    private void UpdateFullCallsign(EntityUid uid, AU14CallsignComponent callsign)
    {
        var word = callsign.Squad is { } squad
            ? GetSquadWord(squad)
            : GetCommandWord(callsign.Faction);

        callsign.Callsign = $"{word} {callsign.Suffix}";
        Dirty(uid, callsign);

        PushConsoleStates(callsign.Faction);
    }

    public string GetCommandWord(string faction)
    {
        if (_commandWords.TryGetValue(faction, out var word))
            return word;

        return DefaultCommandWords.TryGetValue(faction, out var fallback)
            ? fallback
            : faction.ToUpperInvariant();
    }

    public string GetSquadWord(EntityUid squad)
    {
        if (_squadWords.TryGetValue(squad, out var word))
            return word;

        return Name(squad).ToUpperInvariant();
    }

    private string NextFreeNumber(string faction, EntityUid? squad, EntityUid exclude)
    {
        for (var n = 1;; n++)
        {
            var candidate = $"1-{n}";

            if (!SuffixTaken(faction, squad, candidate, exclude))
                return candidate;
        }
    }

    private string MakeUniqueSuffix(string faction, EntityUid? squad, string wanted, EntityUid exclude)
    {
        if (!SuffixTaken(faction, squad, wanted, exclude))
            return wanted;

        for (var n = 2;; n++)
        {
            var candidate = $"{wanted} {n}";

            if (!SuffixTaken(faction, squad, candidate, exclude))
                return candidate;
        }
    }

    private bool SuffixTaken(string faction, EntityUid? squad, string suffix, EntityUid exclude)
    {
        var query = EntityQueryEnumerator<AU14CallsignComponent>();

        while (query.MoveNext(out var uid, out var other))
        {
            if (uid == exclude)
                continue;

            if (other.Faction == faction &&
                other.Squad == squad &&
                string.Equals(other.Suffix, suffix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
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

        args.VoiceName = ent.Comp.Callsign;
    }

    // no-op stubs, the directory console partial fills these in
    private void InitializeConsole()
    {
    }

    private void PushConsoleStates(string faction)
    {
    }
}
