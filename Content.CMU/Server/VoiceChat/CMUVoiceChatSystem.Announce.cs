using Content.Server.Administration.Logs;
using Content.Server.CMU14.Chat;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Marines.Announce;
using Content.Shared._RMC14.Marines.ControlComputer;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Marines.Squads;
using Content.Shared._RMC14.Overwatch;
using Content.Shared._RMC14.Survivor;
using Content.Shared._RMC14.Synth;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.CMU14;
using Content.Shared.CMU14.Ambassador;
using Content.Shared.CMU14.VoiceChat;
using Content.Shared.Database;
using System.Linq;
using Content.Shared.Interaction;
using Content.Shared.Tag;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.VoiceChat;

/// <summary>
/// Live voice announcements through the consoles that already send typed announcements. Each console
/// keeps its typed audience: overwatch reaches its squad, the communications computer the faction,
/// the control computer everyone shipside and the colony comms console the colony. Everyone who hears
/// it gets it in the ear through a PA filter, after the console's usual announcement sound.
/// </summary>
public sealed partial class CMUVoiceChatSystem
{
    [Dependency] private SquadSystem _squad = default!;
    [Dependency] private SkillsSystem _skills = default!;
    [Dependency] private WarshipSystem _warship = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private IAdminLogManager _adminLog = default!;

    private static readonly ProtoId<TagPrototype> CommandTabletTag = "CommandTablet";
    private static readonly TimeSpan AnnouncementAudienceRefresh = TimeSpan.FromSeconds(1);
    private const float AnnouncementReach = 1.5f;

    private readonly Dictionary<EntityUid, TimeSpan> _announceCooldowns = new();
    private readonly List<NetUserId> _endedAnnouncements = new();
    private float _announceDuration;
    private float _announceCooldown;

    private enum AnnouncementKind : byte
    {
        Squad,
        Faction,
        Ship,
        Colony,
    }

    private sealed class Announcement(EntityUid console, AnnouncementKind kind, TimeSpan endTime)
    {
        public readonly EntityUid Console = console;
        public readonly AnnouncementKind Kind = kind;
        public readonly TimeSpan EndTime = endTime;
        public readonly List<INetChannel> Audience = new();
        public TimeSpan AudienceExpires;
    }

    private void InitializeAnnouncements()
    {
        Subs.CVar(_cfg, CCVars.CMUVoiceChatAnnounceDuration, value => _announceDuration = value, true);
        Subs.CVar(_cfg, CCVars.CMUVoiceChatAnnounceCooldown, value => _announceCooldown = value, true);

        SubscribeLocalEvent<OverwatchConsoleComponent, CMUVoiceAnnounceToggleBuiMsg>((uid, _, args) =>
            ToggleAnnouncement(uid, AnnouncementKind.Squad, args.Actor));
        SubscribeLocalEvent<MarineCommunicationsComputerComponent, CMUVoiceAnnounceToggleBuiMsg>((uid, _, args) =>
            ToggleAnnouncement(uid, AnnouncementKind.Faction, args.Actor));
        SubscribeLocalEvent<MarineControlComputerComponent, CMUVoiceAnnounceToggleBuiMsg>((uid, _, args) =>
            ToggleAnnouncement(uid, AnnouncementKind.Ship, args.Actor));
        SubscribeLocalEvent<ColonyCommsConsoleComponent, CMUVoiceAnnounceToggleBuiMsg>((uid, _, args) =>
            ToggleAnnouncement(uid, AnnouncementKind.Colony, args.Actor));
    }

    private void ToggleAnnouncement(EntityUid console, AnnouncementKind kind, EntityUid user)
    {
        if (!_actorQuery.TryComp(user, out var actor))
            return;

        var session = actor.PlayerSession;
        var speaker = GetSpeaker(session.UserId);
        if (speaker.Announcement != null)
        {
            EndAnnouncement(session.UserId, speaker);
            return;
        }

        if (!_voice.IsWebConnected(session.UserId))
        {
            _popup.PopupEntity(Loc.GetString("cmu-voice-announce-not-connected"), user, user);
            return;
        }

        if (IsMuted(session.UserId) || !IsVoiceCapable(user))
        {
            _popup.PopupEntity(Loc.GetString("cmu-voice-announce-cannot-speak"), user, user);
            return;
        }

        if (!CanAnnounceFrom(console, kind, user))
        {
            _popup.PopupEntity(Loc.GetString("cmu-voice-announce-denied"), user, user);
            return;
        }

        var now = _timing.CurTime;
        if (_announceCooldowns.TryGetValue(console, out var next) && now < next)
        {
            var seconds = (int) Math.Ceiling((next - now).TotalSeconds);
            _popup.PopupEntity(Loc.GetString("cmu-voice-announce-cooldown", ("seconds", seconds)), user, user);
            return;
        }

        var announcement = new Announcement(console, kind, now + TimeSpan.FromSeconds(_announceDuration));
        speaker.Announcement = announcement;
        RefreshAnnouncementAudience(session, announcement);

        var filter = Filter.Empty().AddPlayers(GetAnnouncementSessions(announcement));
        _audio.PlayGlobal(GetAnnouncementSound(kind), filter, true, AudioParams.Default.WithVolume(-2f));

        _popup.PopupEntity(Loc.GetString("cmu-voice-announce-started", ("seconds", (int) _announceDuration)), user, user);
        _adminLog.Add(LogType.Chat, LogImpact.Low,
            $"{ToPrettyString(user):player} started a live voice announcement through {ToPrettyString(console):console}");
        SendState(session.UserId, false);
    }

    private bool CanAnnounceFrom(EntityUid console, AnnouncementKind kind, EntityUid user)
    {
        if (!_interaction.InRangeUnobstructed(user, console, AnnouncementReach))
            return false;

        switch (kind)
        {
            case AnnouncementKind.Squad:
                return TryComp<OverwatchConsoleComponent>(console, out var overwatch) &&
                       overwatch.CanMessageSquad &&
                       TryGetOverwatchSquad(overwatch, out _);
            case AnnouncementKind.Faction:
                // Same rule as typed announcements: leadership, or a synthetic on a command tablet.
                return TryComp<MarineCommunicationsComputerComponent>(console, out var computer) &&
                       (_skills.HasSkill(user, computer.AnnounceSkill, computer.AnnounceSkillLevel) ||
                        HasComp<SynthComponent>(user) && _tag.HasTag(console, CommandTabletTag));
            default:
                return true;
        }
    }

    /// <summary>
    /// Mirrors SharedOverwatchConsoleSystem.TryGetAccessibleSquad: the selected squad, if this console's group may reach it.
    /// </summary>
    private bool TryGetOverwatchSquad(OverwatchConsoleComponent console, out EntityUid squad)
    {
        squad = default;
        if (!TryGetEntity(console.Squad, out var squadId) ||
            !TryComp<SquadTeamComponent>(squadId, out var team) ||
            !string.Equals(console.Group, "ADMINISTRATOR", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(team.Group, console.Group, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        squad = squadId.Value;
        return true;
    }

    private static SoundSpecifier GetAnnouncementSound(AnnouncementKind kind)
    {
        return kind switch
        {
            AnnouncementKind.Squad => SharedMarineAnnounceSystem.DefaultSquadSound,
            AnnouncementKind.Faction => SharedMarineAnnounceSystem.DefaultAnnouncementSound,
            AnnouncementKind.Ship => SharedMarineAnnounceSystem.AresAnnouncementSound,
            _ => SharedChatSystem.DefaultAnnouncementSound,
        };
    }

    private void UpdateAnnouncements(TimeSpan now)
    {
        var gameNow = _timing.CurTime;
        _endedAnnouncements.Clear();
        foreach (var (user, speaker) in _speakers)
        {
            if (speaker.Announcement is not { } announcement)
                continue;

            if (gameNow >= announcement.EndTime ||
                !_player.TryGetSessionById(user, out var session) ||
                session.Status != SessionStatus.InGame ||
                session.AttachedEntity is not { } uid ||
                TerminatingOrDeleted(announcement.Console) ||
                !CanAnnounceFrom(announcement.Console, announcement.Kind, uid) ||
                !CanSpeakPassive(uid))
            {
                _endedAnnouncements.Add(user);
            }
        }

        foreach (var user in _endedAnnouncements)
        {
            EndAnnouncement(user, _speakers[user]);
        }
    }

    private void EndAnnouncement(NetUserId user, Speaker speaker)
    {
        if (speaker.Announcement is not { } announcement)
            return;

        speaker.Announcement = null;
        _announceCooldowns[announcement.Console] = _timing.CurTime + TimeSpan.FromSeconds(_announceCooldown);

        if (_player.TryGetSessionById(user, out var session) && session.AttachedEntity is { } uid)
            _popup.PopupEntity(Loc.GetString("cmu-voice-announce-ended"), uid, uid);

        SendState(user, false);
    }

    private void SendAnnouncementFrame(ICommonSession session, Speaker speaker, CMUVoiceWebFrame frame, NetEntity source)
    {
        if (speaker.Announcement is not { } announcement)
            return;

        if (_timing.RealTime >= announcement.AudienceExpires)
            RefreshAnnouncementAudience(session, announcement);

        if (announcement.Audience.Count == 0)
            return;

        if (session.AttachedEntity is { } uid)
            SendSpeakerInfo(speaker, uid, announcement.Audience);

        _net.ServerSendToMany(new CMUMsgVoiceFrame
        {
            Source = source,
            Speaker = speaker.Id,
            Sequence = frame.Sequence,
            Flags = frame.Flags,
            Payload = frame.Payload,
            Route = CMUVoiceRoute.Announcement,
        }, announcement.Audience);
    }

    private void RefreshAnnouncementAudience(ICommonSession speakerSession, Announcement announcement)
    {
        announcement.AudienceExpires = _timing.RealTime + AnnouncementAudienceRefresh;
        announcement.Audience.Clear();

        foreach (var session in GetAnnouncementSessions(announcement))
        {
            if (session == speakerSession)
            {
                if (_voice.HearsSelf(session.UserId))
                    announcement.Audience.Add(session.Channel);

                continue;
            }

            if (_voice.Receives(session.UserId) &&
                session.AttachedEntity is { } listener &&
                CanHear(listener))
            {
                announcement.Audience.Add(session.Channel);
            }
        }
    }

    private IEnumerable<ICommonSession> GetAnnouncementSessions(Announcement announcement)
    {
        MapId? shipMap = null;
        string? faction = null;
        EntityUid? squad = null;

        switch (announcement.Kind)
        {
            case AnnouncementKind.Squad:
                if (TryComp<OverwatchConsoleComponent>(announcement.Console, out var overwatch) &&
                    TryGetOverwatchSquad(overwatch, out var squadId))
                {
                    squad = squadId;
                }

                break;
            case AnnouncementKind.Faction:
                var overwatchGroup = TryComp<OverwatchConsoleComponent>(announcement.Console, out var group) ? group.Group : null;
                faction = SharedMarineAnnounceSystem.ResolveAnnouncementFaction(
                    CompOrNull<MarineCommunicationsComputerComponent>(announcement.Console)?.Faction,
                    overwatchGroup);
                break;
            case AnnouncementKind.Ship:
                faction = SharedMarineAnnounceSystem.ResolveAnnouncementFaction(
                    CompOrNull<MarineControlComputerComponent>(announcement.Console)?.Faction);
                shipMap = _warship.TryGetWarshipMap(announcement.Console, out var warshipMap)
                    ? warshipMap
                    : Transform(announcement.Console).MapID;
                break;
        }

        var colony = announcement.Kind == AnnouncementKind.Colony
            ? ColonyAnnouncements.Recipients(EntityManager).Recipients.ToHashSet()
            : null;

        foreach (var session in _player.Sessions)
        {
            if (session.Status != SessionStatus.InGame || session.AttachedEntity is not { } uid)
                continue;

            var include = announcement.Kind switch
            {
                // Typed squad messages reach squad members only.
                AnnouncementKind.Squad => squad is { } squadUid && _squad.IsInSquad(uid, squadUid),
                // Matches MarineAnnounceSystem.AnnounceToMarines: the faction's marines and ghosts,
                // without ambassadors or survivors.
                AnnouncementKind.Faction => _ghostQuery.HasComp(uid) ||
                                            !HasComp<AmbassadorComponent>(uid) &&
                                            !HasComp<RMCSurvivorComponent>(uid) &&
                                            TryComp<MarineComponent>(uid, out var marine) &&
                                            SharedMarineAnnounceSystem.IsMarineAnnouncementRecipient(marine.Faction, faction),
                // Matches the shipwide announcement: everyone on the ship map who is in the faction, and ghosts.
                AnnouncementKind.Ship => Transform(uid).MapID == shipMap &&
                                         (_ghostQuery.HasComp(uid) ||
                                          TryComp<MarineComponent>(uid, out var shipMarine) &&
                                          SharedMarineAnnounceSystem.IsMarineAnnouncementRecipient(shipMarine.Faction, faction)),
                AnnouncementKind.Colony => colony!.Contains(session),
                _ => false,
            };

            if (include)
                yield return session;
        }
    }
}
