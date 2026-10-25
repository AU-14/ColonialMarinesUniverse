using Content.Shared._RMC14.Telephone;
using Content.Shared.CMU14.Telephone;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.CMU14.Telephone;

/// <summary>What both phone screens offer the BUI.</summary>
public interface ICMUPhoneWindow
{
    event Action<NetEntity>? OnCall;
    event Action<bool>? OnDnd;
    event Action<CMUEmergencyService>? OnEmergency;

    void UpdateState(RMCTelephoneBuiState state, string phoneName);
}

/// <summary>
///     A phone's screen: the directory of every phone it can call, do-not-disturb and 911. Desk, wall and field
///     phones get <see cref="CMUDeskPhoneWindow"/>; payphone booths use <see cref="CMUPayphoneBui"/>.
/// </summary>
public class CMUTelephoneBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private DefaultWindow? _window;

    protected virtual DefaultWindow CreatePhoneWindow()
    {
        return this.CreateWindow<CMUDeskPhoneWindow>();
    }

    protected override void Open()
    {
        base.Open();
        _window = CreatePhoneWindow();

        if (_window is ICMUPhoneWindow phone)
        {
            phone.OnCall += id => SendPredictedMessage(new RMCTelephoneCallBuiMsg(id));
            phone.OnDnd += dnd => SendPredictedMessage(new RMCTelephoneDndBuiMsg(dnd));
            phone.OnEmergency += service => SendMessage(new CMUEmergencyCallBuiMsg(service));
        }

        Refresh();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        Refresh();
    }

    private void Refresh()
    {
        if (_window is not ICMUPhoneWindow phone || State is not RMCTelephoneBuiState state)
            return;

        var name = EntMan.TryGetComponent(Owner, out MetaDataComponent? meta) ? meta.EntityName : string.Empty;
        _window.Title = name;
        phone.UpdateState(state, name);
    }
}

/// <summary>A payphone booth: the same phone, with the public payphone's screen.</summary>
public sealed class CMUPayphoneBui(EntityUid owner, Enum uiKey) : CMUTelephoneBui(owner, uiKey)
{
    protected override DefaultWindow CreatePhoneWindow()
    {
        var window = this.CreateWindow<CMUPayphoneWindow>();
        if (EntMan.TryGetComponent(Owner, out Content.Shared.CMU14.Telephone.CMUPayphoneComponent? payphone))
            window.Payphone = payphone;
        return window;
    }
}

/// <summary>A mobile phone: the same phone, on a handset's screen.</summary>
public sealed class CMUMobilePhoneBui(EntityUid owner, Enum uiKey) : CMUTelephoneBui(owner, uiKey)
{
    protected override DefaultWindow CreatePhoneWindow()
    {
        return this.CreateWindow<CMUMobilePhoneWindow>();
    }
}
