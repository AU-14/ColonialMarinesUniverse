using Content.Shared._AU14.Radio;
using Robust.Client.UserInterface;

namespace Content.Client._AU14.Radio;

public sealed class ANPRCRadioBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private ANPRCRadioWindow? _window;

    public ANPRCRadioBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<ANPRCRadioWindow>();

        _window.OnSelectSlot += slot => SendMessage(new ANPRCSelectSlotMsg(slot));
        _window.OnTogglePower += () => SendMessage(new ANPRCTogglePowerMsg());
        _window.OnToggleMonitor += () => SendMessage(new ANPRCToggleMonitorMsg());
        _window.OnSetMode += mode => SendMessage(new ANPRCSetModeMsg(mode));
        _window.OnSetScan += enabled => SendMessage(new ANPRCSetScanMsg(enabled));
        _window.OnSetTxPower += power => SendMessage(new ANPRCSetTxPowerMsg(power));
        _window.OnSetSquelch += level => SendMessage(new ANPRCSetSquelchMsg(level));
        _window.OnSetCallsign += callsign => SendMessage(new ANPRCSetCallsignMsg(callsign));
        _window.OnAddSlot += label => SendMessage(new ANPRCAddSlotMsg(label));
        _window.OnDeleteSlot += slot => SendMessage(new ANPRCDeleteSlotMsg(slot));
        _window.OnSetSlotChannel += (s, ch) => SendMessage(new ANPRCSetSlotChannelMsg(s, ch));
        _window.OnClearSlot += slot => SendMessage(new ANPRCClearSlotMsg(slot));
        _window.OnCryptoZeroize += () => SendMessage(new ANPRCCryptoZeroizeMsg());
        _window.OnCryptoDestroy += () => SendMessage(new ANPRCCryptoDestroyMsg());
        _window.OnCryptoRecrypto += () => SendMessage(new ANPRCCryptoRecryptoMsg());
        _window.OnRadioCheck += () => SendMessage(new ANPRCRadioCheckMsg());
        _window.OnOpenDirectory += () => SendMessage(new ANPRCOpenDirectoryMsg());
        _window.OnManualFrequency += (slot, text) => SendMessage(new ANPRCManualFrequencyMsg(slot, text));
        _window.OnSetSweep += enabled => SendMessage(new ANPRCSetSweepMsg(enabled));
        _window.OnTuneContact += (slot, freq) => SendMessage(new ANPRCTuneContactMsg(slot, freq));
        _window.OnPrintLog += intercepts => SendMessage(new ANPRCPrintLogMsg(intercepts));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is not ANPRCRadioState s)
            return;

        _window?.UpdateState(s);
    }
}
