// ReSharper disable CheckNamespace

namespace Content.Shared._RMC14.Telephone;

/// <summary>
///     Raised on a phone when its name is wanted for a directory entry or a call. A handler that sets
///     <see cref="Name"/> replaces the stock "job name (squad)" label - the AN/PRC-117G answers to its
///     callsign, so the phone book cannot be a way around the net's name masking.
/// </summary>
[ByRefEvent]
public record struct AU14GetPhoneNameEvent(string? Name = null);

/// <summary>
///     Raised on every candidate entry while a directory is built for <see cref="Caller"/>. Cancelled
///     entries are left out, which is how a field radio with no link drops off the phone book.
/// </summary>
[ByRefEvent]
public record struct AU14PhoneReachableEvent(EntityUid Caller, bool Cancelled = false);

/// <summary>
///     AU14 seam into the RMC telephone system, so the AN/PRC-117G can be a phone that shares its one
///     handset with the net. Everything here wraps the upstream call state machine rather than duplicating
///     it, which keeps field radios and fixed phones on the same exchange.
/// </summary>
public abstract partial class SharedRMCTelephoneSystem
{
    /// <summary>Makes an existing entity the handset of a rotary phone, for phones whose handset is spawned by someone else.</summary>
    public void AU14BindPhone(Entity<RotaryPhoneComponent> rotary, EntityUid telephone)
    {
        rotary.Comp.Phone = telephone;
        Dirty(rotary);

        if (TryComp(telephone, out RMCTelephoneComponent? phone))
        {
            phone.RotaryPhone = rotary;
            Dirty(telephone, phone);
        }
    }

    public bool AU14InCall(EntityUid rotary)
    {
        return HasComp<RotaryPhoneDialingComponent>(rotary) || HasComp<RotaryPhoneReceivingComponent>(rotary);
    }

    /// <summary>Being called and nobody has answered yet.</summary>
    public bool AU14IsRinging(EntityUid rotary)
    {
        return HasComp<RotaryPhoneReceivingComponent>(rotary) && !HasPickedUp(rotary);
    }

    /// <summary>The phone at the other end of this one's call, if it still has one.</summary>
    public bool AU14TryGetOtherRotary(EntityUid rotary, out EntityUid other)
    {
        if (TryComp(rotary, out RotaryPhoneDialingComponent? dialing) && dialing.Other is { } called)
        {
            other = called;
            return HasComp<RotaryPhoneReceivingComponent>(called);
        }

        if (TryComp(rotary, out RotaryPhoneReceivingComponent? receiving) && receiving.Other is { } caller)
        {
            other = caller;
            return HasComp<RotaryPhoneDialingComponent>(caller);
        }

        other = default;
        return false;
    }

    /// <summary>The handset at the other end of this phone's call.</summary>
    public bool AU14TryGetOtherPhone(EntityUid rotary, out EntityUid otherPhone)
    {
        return TryGetOtherPhone(rotary, out otherPhone);
    }

    public void AU14Answer(EntityUid rotary, EntityUid user)
    {
        if (TryComp(rotary, out RotaryPhoneReceivingComponent? receiving))
            PickupReceiving((rotary, receiving), user);
    }

    /// <summary>Ends whatever call this phone is part of and puts its handset back.</summary>
    public bool AU14HangUp(EntityUid rotary, EntityUid? user)
    {
        if (!TryComp(rotary, out RotaryPhoneComponent? comp) || comp.Phone is not { } phone)
            return false;

        if (TryComp(rotary, out RotaryPhoneDialingComponent? dialing))
            return HangUpDialing((rotary, dialing), phone, user);

        if (TryComp(rotary, out RotaryPhoneReceivingComponent? receiving))
            return HangUpReceiving((rotary, receiving), phone, user);

        return false;
    }

    public void AU14OpenDialer(EntityUid rotary, EntityUid user)
    {
        AU14SendDirectory(rotary);
        _ui.TryOpenUi(rotary, RMCTelephoneUiKey.Key, user);
    }

    public string AU14GetPhoneName(EntityUid phone)
    {
        var ev = new AU14GetPhoneNameEvent();
        RaiseLocalEvent(phone, ref ev);
        return ev.Name ?? GetPhoneName(phone);
    }

    /// <summary>
    ///     The stock directory with the AU14 naming and reachability hooks applied. Sent after the stock one on
    ///     the same tick, so it is the one the client ends up with.
    /// </summary>
    public void AU14SendDirectory(EntityUid phone)
    {
        if (_net.IsClient || !TryComp(phone, out RotaryPhoneComponent? calling))
            return;

        var callingFaction = calling.Faction;
        var phones = new List<RMCPhone>();
        var query = EntityQueryEnumerator<RotaryPhoneComponent>();

        while (query.MoveNext(out var otherId, out var other))
        {
            if (otherId == phone)
                continue;

            var visible = other.CallableByAll ||
                          string.IsNullOrEmpty(other.Faction) ||
                          (!string.IsNullOrEmpty(callingFaction) && other.Faction == callingFaction);

            if (!visible)
                continue;

            var reachable = new AU14PhoneReachableEvent(phone);
            RaiseLocalEvent(otherId, ref reachable);

            if (reachable.Cancelled)
                continue;

            phones.Add(new RMCPhone(GetNetEntity(otherId), other.Category, AU14GetPhoneName(otherId)));
        }

        _ui.SetUiState(phone, RMCTelephoneUiKey.Key,
            new RMCTelephoneBuiState(phones, calling.CanDnd, HasComp<RotaryPhoneDndComponent>(phone)));
    }
}
