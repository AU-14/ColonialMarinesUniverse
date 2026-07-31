using System.Linq;
using Content.Server._AU14.Examine;
using Content.Server.Chat.Managers;
using Content.Server.Verbs;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.Examine;
using Content.Shared.IdentityManagement;
using Content.Shared.Verbs;
using JetBrains.Annotations;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server.Examine
{
    [UsedImplicitly]
    public sealed partial class ExamineSystem : ExamineSystemShared
    {
        [Dependency] private VerbSystem _verbSystem = default!;
        [Dependency] private readonly IChatManager _chatManager = default!;
        [Dependency] private readonly INetConfigurationManager _netConfigManager = default!;

        private readonly FormattedMessage _entityNotFoundMessage = new();
        private readonly FormattedMessage _entityOutOfRangeMessage = new();

        // Neutral gray chat highlight, same treatment as system messages get.
        private static readonly ChatDisplayMetadata ExamineChatDisplay = new(
            ChatDisplayKind.System,
            accentColor: Color.FromHex("#a0a0a0"),
            backgroundColorOverride: Color.FromHex("#1e1e1e"));

        public override void Initialize()
        {
            base.Initialize();
            _entityNotFoundMessage.AddText(Loc.GetString("examine-system-entity-does-not-exist"));
            _entityOutOfRangeMessage.AddText(Loc.GetString("examine-system-cant-see-entity"));

            SubscribeNetworkEvent<ExamineSystemMessages.RequestExamineInfoMessage>(ExamineInfoRequest);
        }

        public override void SendExamineTooltip(EntityUid player, EntityUid target, FormattedMessage message, bool getVerbs, bool centerAtCursor)
        {
            if (!TryComp<ActorComponent>(player, out var actor))
                return;

            var session = actor.PlayerSession;

            SortedSet<Verb>? verbs = null;
            if (getVerbs)
                verbs = _verbSystem.GetLocalVerbs(target, player, typeof(ExamineVerb));

            var ev = new ExamineSystemMessages.ExamineInfoResponseMessage(
                GetNetEntity(target), 0, message, verbs?.ToList(), centerAtCursor
            );

            RaiseNetworkEvent(ev, session.Channel);
        }

        private void ExamineInfoRequest(ExamineSystemMessages.RequestExamineInfoMessage request, EntitySessionEventArgs eventArgs)
        {
            var player = eventArgs.SenderSession;
            var session = eventArgs.SenderSession;
            var channel = player.Channel;
            var entity = GetEntity(request.NetEntity);

            if (session.AttachedEntity is not {Valid: true} playerEnt
                || !Exists(entity))
            {
                RaiseNetworkEvent(new ExamineSystemMessages.ExamineInfoResponseMessage(
                    request.NetEntity, request.Id, _entityNotFoundMessage), channel);
                return;
            }

            if (!CanExamine(playerEnt, entity))
            {
                RaiseNetworkEvent(new ExamineSystemMessages.ExamineInfoResponseMessage(
                    request.NetEntity, request.Id, _entityOutOfRangeMessage, knowTarget: false), channel);
                return;
            }

            SortedSet<Verb>? verbs = null;
            if (request.GetVerbs)
                verbs = _verbSystem.GetLocalVerbs(entity, playerEnt, typeof(ExamineVerb));

            var text = GetExamineText(entity, player.AttachedEntity);
            RaiseNetworkEvent(new ExamineSystemMessages.ExamineInfoResponseMessage(
                request.NetEntity, request.Id, text, verbs?.ToList()), channel);

            // AU14/CMU: optionally echo the exact same context-menu examine text into chat.
            // Skip this if the target already gets the detailed character breakdown message,
            // so examining a character doesn't produce two chat messages at once.
            var coveredByCharacterBreakdown = HasComp<ExaminableCharacterComponent>(entity)
                && _netConfigManager.GetClientCVar(channel, CCVars.ExamineLogInChat);

            if (!coveredByCharacterBreakdown && _netConfigManager.GetClientCVar(channel, CCVars.ExamineFullTextInChat))
            {
                var markup = text.ToMarkup();
                if (!string.IsNullOrWhiteSpace(FormattedMessage.RemoveMarkupPermissive(markup)))
                {
                    var itemName = FormattedMessage.EscapeText(Identity.Name(entity, EntityManager, playerEnt));
                    var combinedLog = $"[color=gold][bold]{itemName}[/bold][/color]\n{markup}";
                    _chatManager.ChatMessageToOne(ChatChannel.Emotes, combinedLog, combinedLog, EntityUid.Invalid, false, channel, recordReplay: false, display: ExamineChatDisplay);
                }
            }
        }
    }
}
