using Content.Server.Chat.Systems;
using Content.Shared._RMC14.Chat;
using Content.Shared._RMC14.Megaphone;
using Content.Shared.Examine;
using Content.Shared.Ghost;
using Content.Shared.Speech;
using Robust.Server.Console;
using Robust.Server.Player;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._RMC14.Megaphone;

public sealed partial class RMCServerMegaphoneSystem : EntitySystem
{
    [Dependency] private IServerConsoleHost _console = default!;
    [Dependency] private ExamineSystemShared _examine = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IGameTiming _timing = default!;

    private EntityQuery<GhostHearingComponent> _ghostHearingQuery;
    private EntityQuery<TransformComponent> _xformQuery;

    public override void Initialize()
    {
        _ghostHearingQuery = GetEntityQuery<GhostHearingComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();

        SubscribeLocalEvent<ActorComponent, MegaphoneInputEvent>(OnMegaphoneInput);
        SubscribeLocalEvent<ExpandICChatRecipientsEvent>(OnExpandRecipients);
        SubscribeLocalEvent<RMCMegaphoneUserComponent, EntitySpokeEvent>(OnEntitySpoke);
    }

    private void OnMegaphoneInput(Entity<ActorComponent> ent, ref MegaphoneInputEvent ev)
    {
        if (_timing.ApplyingState)
            return;

        if (string.IsNullOrWhiteSpace(ev.Message))
            return;

        var user = GetEntity(ev.Actor);
        EnsureComp<RMCSpeechBubbleSpecificStyleComponent>(user);
        var userComp = EnsureComp<RMCMegaphoneUserComponent>(user);

        if (TryComp<SpeechComponent>(user, out var speech))
        {
            userComp.OriginalSpeechVerb = speech.SpeechVerb;
            userComp.OriginalSpeechSounds = speech.SpeechSounds;
            userComp.OriginalSuffixSpeechVerbs = speech.SuffixSpeechVerbs;

            speech.SpeechVerb = userComp.SpeechVerb;
            speech.SpeechSounds = userComp.MegaphoneSpeechSound;
            speech.SuffixSpeechVerbs = userComp.SuffixSpeechVerbs;
            Dirty(user, speech);

            // Send a message using the say command
            var session = ent.Comp.PlayerSession;
            _console.ExecuteCommand(session, $"say \"{CommandParsing.Escape(ev.Message)}\"");

            // Restore the original speech settings
            speech.SpeechVerb = userComp.OriginalSpeechVerb ?? "Default";
            speech.SpeechSounds = userComp.OriginalSpeechSounds;
            speech.SuffixSpeechVerbs = userComp.OriginalSuffixSpeechVerbs ?? new();
            Dirty(user, speech);
        }

        CleanupMegaphoneUser(user);
    }

    private void OnExpandRecipients(ExpandICChatRecipientsEvent ev)
    {
        if (!TryComp<RMCMegaphoneUserComponent>(ev.Source, out var megaphone) ||
            megaphone.Range <= ev.VoiceRange ||
            !_xformQuery.TryComp(ev.Source, out var sourceXform))
        {
            return;
        }

        var sourceMap = sourceXform.MapID;
        var sourceCoords = sourceXform.Coordinates;

        foreach (var player in _players.Sessions)
        {
            if (player.AttachedEntity is not { Valid: true } playerEntity ||
                ev.Recipients.ContainsKey(player) ||
                !_xformQuery.TryComp(playerEntity, out var listenerXform) ||
                listenerXform.MapID != sourceMap)
            {
                continue;
            }

            if (!sourceCoords.TryDistance(EntityManager, listenerXform.Coordinates, out var distance) ||
                distance >= megaphone.Range)
            {
                continue;
            }

            var observer = _ghostHearingQuery.HasComp(playerEntity);
            var hasLos = observer || _examine.InRangeUnOccluded(ev.Source, playerEntity, megaphone.Range);
            ev.Recipients.TryAdd(player, new ChatSystem.ICChatRecipientData(distance, observer, HasLOS: hasLos));
        }
    }

    private void OnEntitySpoke(Entity<RMCMegaphoneUserComponent> ent, ref EntitySpokeEvent args)
    {
        if (args.Channel != null)
            return;

        // Remove components after the message is sent
        CleanupMegaphoneUser(ent);
    }

    private void CleanupMegaphoneUser(EntityUid user)
    {
        if (HasComp<RMCMegaphoneUserComponent>(user))
            RemComp<RMCMegaphoneUserComponent>(user);

        if (HasComp<RMCSpeechBubbleSpecificStyleComponent>(user))
            RemComp<RMCSpeechBubbleSpecificStyleComponent>(user);
    }
}
