using Content.Shared.CMU14.GearFlavor;
using Content.Shared.Examine;
using Content.Shared.IdentityManagement;
using Content.Shared.Inventory;
using Robust.Shared.Utility;

namespace Content.Shared.CMU14.CharacterDescription;

public sealed class CharacterDescriptionExamineSystem : EntitySystem
{
    [Dependency] private InventorySystem _inventory = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CharacterDescriptionComponent, ExaminedEvent>(OnExamined);
    }

    private const string ShortExamineColor = "#ADD8E6";
    private const string GearFlavorColor = "#C8B88A";

    private static readonly (string Slot, LocId Line)[] GearFlavorSlots =
    [
        ("head", "cmu-gear-flavor-helmet"),
        ("outerClothing", "cmu-gear-flavor-armor"),
    ];

    private void OnExamined(Entity<CharacterDescriptionComponent> ent, ref ExaminedEvent args)
    {
        if (!string.IsNullOrWhiteSpace(ent.Comp.ShortExamine))
        {
            var text = FormattedMessage.EscapeText(ent.Comp.ShortExamine);
            args.PushMarkup($"[color={ShortExamineColor}]{text}[/color]");
        }

        foreach (var (slot, line) in GearFlavorSlots)
        {
            if (!_inventory.TryGetSlotEntity(ent, slot, out var item) ||
                !TryComp(item, out CMUGearFlavorTextComponent? flavor) ||
                string.IsNullOrWhiteSpace(flavor.Text))
            {
                continue;
            }

            var text = FormattedMessage.EscapeText(CMUGearFlavorText.Fill(EntityManager, ent, args.Examiner, flavor.Text));
            var wearer = Identity.Entity(ent, EntityManager, args.Examiner);
            args.PushMarkup($"[color={GearFlavorColor}]{Loc.GetString(line, ("user", wearer), ("text", text))}[/color]");
        }
    }
}
