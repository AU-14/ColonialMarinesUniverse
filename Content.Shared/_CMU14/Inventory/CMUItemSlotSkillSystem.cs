using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Popups;

namespace Content.Shared._CMU14.Inventory;

public sealed partial class CMUItemSlotSkillSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SkillsSystem _skills = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUItemSlotSkillRequiredComponent, ItemSlotInsertAttemptEvent>(OnInsertAttempt);
        SubscribeLocalEvent<CMUItemSlotSkillRequiredComponent, ItemSlotEjectAttemptEvent>(OnEjectAttempt);
    }

    private void OnInsertAttempt(Entity<CMUItemSlotSkillRequiredComponent> ent, ref ItemSlotInsertAttemptEvent args)
    {
        if (!CanUseSlot(ent, args.User))
            args.Cancelled = true;
    }

    private void OnEjectAttempt(Entity<CMUItemSlotSkillRequiredComponent> ent, ref ItemSlotEjectAttemptEvent args)
    {
        if (!CanUseSlot(ent, args.User))
            args.Cancelled = true;
    }

    private bool CanUseSlot(Entity<CMUItemSlotSkillRequiredComponent> ent, EntityUid? user)
    {
        if (user is not { } userUid || _skills.HasAllSkills(userUid, ent.Comp.Skills))
            return true;

        var message = Loc.GetString(ent.Comp.FailPopup);
        _popup.PopupClient(message, ent, userUid, PopupType.SmallCaution);
        return false;
    }
}
