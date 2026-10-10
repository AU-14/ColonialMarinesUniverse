using Content.Shared.CMU14.Telephone;
using Robust.Shared.Containers;

namespace Content.Shared._RMC14.Telephone;

public abstract partial class SharedRMCTelephoneSystem
{
    /// <summary>
    /// Where a mobile phone keeps its handset while a call is active, so the phone item itself is what gets held.
    /// </summary>
    private const string CMUMobileHandsetContainerId = "cmu_mobile_handset";

    private bool CMUTryParkMobileHandset(Entity<RotaryPhoneComponent> rotary, EntityUid telephone)
    {
        if (!HasComp<CMUMobilePhoneComponent>(rotary))
            return false;

        if (_container.TryGetContainer(rotary, rotary.Comp.ContainerId, out var container))
            _container.Remove(telephone, container);

        var parked = _container.EnsureContainer<ContainerSlot>(rotary, CMUMobileHandsetContainerId);
        _container.Insert(telephone, parked);
        return true;
    }

    private bool CMUTryReturnMobileHandset(EntityUid rotary, EntityUid telephone, BaseContainer container)
    {
        if (!HasComp<CMUMobilePhoneComponent>(rotary))
            return false;

        if (_container.TryGetContainer(rotary, CMUMobileHandsetContainerId, out var parked))
            _container.Remove(telephone, parked);

        _container.Insert(telephone, container);
        return true;
    }

    /// <summary>
    /// Holding a mobile phone counts as holding its handset.
    /// </summary>
    public bool CMUIsHoldingPhone(EntityUid user, EntityUid telephone)
    {
        if (_hands.IsHolding(user, telephone))
            return true;

        return TryComp(telephone, out RMCTelephoneComponent? phone) &&
               phone.RotaryPhone is { } rotary &&
               HasComp<CMUMobilePhoneComponent>(rotary) &&
               _hands.IsHolding(user, rotary);
    }

    /// <summary>
    /// Who is holding a handset, including one parked inside a held mobile phone.
    /// </summary>
    public bool CMUTryGetPhoneHolder(EntityUid telephone, out EntityUid holder)
    {
        holder = default;
        if (!_container.TryGetContainingContainer((telephone, null), out var container))
            return false;

        if (_hands.IsHolding(container.Owner, telephone))
        {
            holder = container.Owner;
            return true;
        }

        var mobile = container.Owner;
        if (!HasComp<CMUMobilePhoneComponent>(mobile) ||
            !_container.TryGetContainingContainer((mobile, null), out var mobileContainer) ||
            !_hands.IsHolding(mobileContainer.Owner, mobile))
        {
            return false;
        }

        holder = mobileContainer.Owner;
        return true;
    }

    /// <summary>
    /// Using a mobile phone in hand: hang up an active call, answer a ringing one, or open the dialer.
    /// </summary>
    public void CMUUseMobilePhone(Entity<RotaryPhoneComponent> ent, EntityUid user)
    {
        if (ent.Comp.Phone is not { } phone)
            return;

        if (TryComp(ent, out RotaryPhoneDialingComponent? dialing))
        {
            HangUpDialing((ent, dialing), phone, null);
            return;
        }

        if (TryComp(ent, out RotaryPhoneReceivingComponent? receiving))
        {
            if (HasPickedUp(ent.Owner))
                HangUpReceiving((ent, receiving), phone, null);
            else
                PickupReceiving((ent, receiving), user);

            return;
        }

        SendUIState(ent);
        _ui.TryOpenUi(ent.Owner, RMCTelephoneUiKey.Key, user);
    }
}
