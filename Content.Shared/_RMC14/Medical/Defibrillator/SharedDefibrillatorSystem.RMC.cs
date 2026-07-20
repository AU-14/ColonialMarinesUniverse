using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Medical.Defibrillator;
using Content.Shared.Inventory;

namespace Content.Shared.Medical;

public abstract partial class SharedDefibrillatorSystem
{
    [Dependency] private InventorySystem _rmcDefibrillatorInventory = default!;
    [Dependency] private SkillsSystem _rmcDefibrillatorSkills = default!;

    private bool CanRMCZap(EntityUid defibrillator, EntityUid target, EntityUid? user)
    {
        if (TryComp(target, out RMCDefibrillatorBlockedComponent? blocked))
        {
            ShowRMCBlockedPopup(defibrillator, target, user, blocked);
            return false;
        }

        var slots = _rmcDefibrillatorInventory.GetSlotEnumerator(target, SlotFlags.OUTERCLOTHING);
        while (slots.MoveNext(out var slot))
        {
            if (!TryComp(slot.ContainedEntity, out blocked))
                continue;

            ShowRMCBlockedPopup(defibrillator, target, user, blocked);
            return false;
        }

        return true;
    }

    private void ShowRMCBlockedPopup(
        EntityUid defibrillator,
        EntityUid target,
        EntityUid? user,
        RMCDefibrillatorBlockedComponent blocked)
    {
        if (user is not { } recipient)
            return;

        _popup.PopupEntity(
            Loc.GetString(blocked.Popup, ("target", target)),
            defibrillator,
            recipient);
    }

    private TimeSpan GetRMCDefibrillatorDuration(EntityUid user, DefibrillatorComponent component)
    {
        return component.DoAfterDuration +
               component.SkillMultiplierDuration *
               _rmcDefibrillatorSkills.GetSkillDelayMultiplier(user, component.Skill);
    }
}
