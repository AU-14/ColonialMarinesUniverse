using Content.Server.Chat.Managers;
using Content.Server.IdentityManagement;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.Examine;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Inventory.VirtualItem;
using Robust.Shared.Configuration;
using Robust.Shared.Player;

namespace Content.Server._AU14.Examine
{
    // Sends a detailed equipment breakdown to the examiner's chat log. Deliberately does not
    // touch the examine tooltip (args.PushMarkup/PushText) - this is chat-only, toggleable via
    // CCVars.ExamineLogInChat.
    public sealed class ExaminableCharacterSystem : EntitySystem
    {
        [Dependency] private readonly InventorySystem _inventorySystem = default!;
        [Dependency] private readonly SharedHandsSystem _handsSystem = default!;
        [Dependency] private readonly IdentitySystem _identitySystem = default!;
        [Dependency] private readonly EntityManager _entityManager = default!;
        [Dependency] private readonly IChatManager _chatManager = default!;
        [Dependency] private readonly INetConfigurationManager _netConfigManager = default!;

        // Neutral gray chat highlight, same treatment as system messages get.
        private static readonly ChatDisplayMetadata ExamineChatDisplay = new(
            ChatDisplayKind.System,
            accentColor: Color.FromHex("#a0a0a0"),
            backgroundColorOverride: Color.FromHex("#1e1e1e"));

        private static readonly Dictionary<string, string> SlotLabels = new()
        {
            { "head", "head-" },
            { "eyes", "eyes-" },
            { "mask", "mask-" },
            { "neck", "neck-" },
            { "ears", "ears-" },
            { "ears2", "ears2-" },
            { "jumpsuit", "jumpsuit-" },
            { "outerClothing", "outer-" },
            { "back", "back-" },
            { "gloves", "gloves-" },
            { "belt", "belt-" },
            { "id", "id-" },
            { "shoes", "shoes-" },
            { "suitstorage", "suitstorage-" }
        };

        public override void Initialize()
        {
            SubscribeLocalEvent<ExaminableCharacterComponent, ExaminedEvent>(HandleExamine);
        }

        private void HandleExamine(EntityUid uid, ExaminableCharacterComponent comp, ExaminedEvent args)
        {
            if (!TryComp<ActorComponent>(args.Examiner, out var actorComponent))
                return;

            if (!_netConfigManager.GetClientCVar(actorComponent.PlayerSession.Channel, CCVars.ExamineLogInChat))
                return;

            var selfaware = (args.Examiner == args.Examined);
            var logLines = new List<string>();

            string canseeloc = "examine-can-see";
            string nameloc = "examine-name";

            if (selfaware)
            {
                canseeloc += "-selfaware";
                nameloc += "-selfaware";
            }

            var identity = _identitySystem.GetEntityIdentity(uid);
            var name = Loc.GetString(nameloc, ("name", identity));
            logLines.Add($"[color=gold][font size=10]{name}[/font][/color]");

            var cansee = Loc.GetString(canseeloc, ("ent", uid));
            logLines.Add($"[color=lightgray][font size=10]{cansee}[/font][/color]");

            var wornAnything = false;

            foreach (var (slotName, slotLabelPrefix) in SlotLabels)
            {
                var slotLabel = slotLabelPrefix + "examine";

                if (selfaware)
                    slotLabel += "-selfaware";

                if (!_inventorySystem.TryGetSlotEntity(uid, slotName, out var slotEntity))
                    continue;

                if (_entityManager.TryGetComponent<MetaDataComponent>(slotEntity, out var metaData))
                {
                    var item = Loc.GetString(slotLabel, ("item", metaData.EntityName), ("ent", uid));
                    logLines.Add($"[color=lightgray][font size=10]{item}[/font][/color]");
                    wornAnything = true;
                }
            }

            if (!wornAnything)
            {
                string canseenothingloc = "examine-can-see-nothing";

                if (selfaware)
                    canseenothingloc += "-selfaware";

                var canseenothing = Loc.GetString(canseenothingloc, ("ent", uid));
                logLines.Add($"[color=lightgray][font size=10]{canseenothing}[/font][/color]");
            }

            if (_entityManager.TryGetComponent<HandsComponent>(uid, out var handsComp))
            {
                foreach (var (handId, hand) in _handsSystem.EnumerateHandsInSortedOrder((uid, handsComp)))
                {
                    if (!_handsSystem.TryGetHeldItem((uid, handsComp), handId, out var heldEntity))
                        continue;

                    if (_entityManager.HasComponent<VirtualItemComponent>(heldEntity))
                        continue;

                    if (!_entityManager.TryGetComponent<MetaDataComponent>(heldEntity, out var heldMeta))
                        continue;

                    var handLabel = hand.Location switch
                    {
                        HandLocation.Left => "hand-left-examine",
                        HandLocation.Right => "hand-right-examine",
                        _ => "hand-middle-examine"
                    };

                    if (selfaware)
                        handLabel += "-selfaware";

                    var heldItem = Loc.GetString(handLabel, ("item", heldMeta.EntityName), ("ent", uid));
                    logLines.Add($"[color=lightgray][font size=10]{heldItem}[/font][/color]");
                }
            }

            var combinedLog = string.Join("\n", logLines);
            _chatManager.ChatMessageToOne(ChatChannel.Emotes, combinedLog, combinedLog, EntityUid.Invalid, false, actorComponent.PlayerSession.Channel, recordReplay: false, display: ExamineChatDisplay);
        }
    }
}
