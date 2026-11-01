using Content.Shared.Hands.Components;
using Content.Shared.IdentityManagement;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Inventory.VirtualItem;

namespace Content.Server.CMU14.Examine
{
    // Builds a detailed equipment breakdown for the examiner's chat log. Deliberately does not
    // touch the examine tooltip (args.PushMarkup/PushText) - this is chat-only, toggleable via
    // CCVars.ExamineLogInChat. The examine system sends it together with the rest of the examine text.
    public sealed partial class ExaminableCharacterSystem : EntitySystem
    {
        [Dependency] private  InventorySystem _inventorySystem = default!;
        [Dependency] private  SharedHandsSystem _handsSystem = default!;
        [Dependency] private  IdentitySystem _identitySystem = default!;
        [Dependency] private  EntityManager _entityManager = default!;

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

        /// <summary>
        /// The equipment breakdown for the examiner's chat log: name, what they're wearing and what they're holding.
        /// The examine system sends it along with the rest of the examine text.
        /// </summary>
        public string BuildBreakdown(EntityUid uid, EntityUid examiner)
        {
            var selfaware = examiner == uid;
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

            return string.Join("\n", logLines);
        }
    }
}
