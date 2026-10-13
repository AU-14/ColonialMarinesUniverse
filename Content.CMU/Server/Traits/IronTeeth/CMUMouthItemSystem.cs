using Content.Shared.Atmos.Components;
using Content.Shared.Clothing.Components;
using Content.Shared.CMU14.Hearing;
using Content.Shared.CMU14.Traits.IronTeeth;
using Content.Shared.IdentityManagement;
using Content.Shared.Inventory;
using Content.Shared.Popups;

namespace Content.Server.CMU14.Traits.IronTeeth;

/// <summary>
///     Something held in the teeth (a knife, a cigarette) falls out when its holder goes unconscious
///     or into crit, unless they have <see cref="CMUIronTeethComponent"/>. Real masks stay on.
/// </summary>
public sealed class CMUMouthItemSystem : EntitySystem
{
    private const string MouthSlot = "mask";

    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUMuffledHearingComponent, ComponentStartup>(OnWentUnconscious);
    }

    private void OnWentUnconscious(Entity<CMUMuffledHearingComponent> ent, ref ComponentStartup args)
    {
        if (HasComp<CMUIronTeethComponent>(ent))
            return;

        if (!_inventory.TryGetSlotEntity(ent, MouthSlot, out var item) ||
            HasComp<MaskComponent>(item) ||
            HasComp<BreathToolComponent>(item))
        {
            return;
        }

        if (!_inventory.TryUnequip(ent, MouthSlot, silent: true, force: true))
            return;

        _popup.PopupEntity(
            Loc.GetString("cmu-mouth-item-falls-out", ("item", item.Value), ("user", Identity.Entity(ent, EntityManager))),
            ent,
            PopupType.Small);
    }
}
