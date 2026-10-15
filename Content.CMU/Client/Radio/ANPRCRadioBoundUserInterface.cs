using Content.Client.CMU14.Radio.ANPRC;
using Content.Shared.CMU14.Radio;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.Radio;

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
        _window.OnOpenPhone += () => SendMessage(new ANPRCOpenPhoneMsg());
        _window.OnSetBurst += enabled => SendMessage(new ANPRCSetBurstMsg(enabled));
        _window.OnSetPowerSave += enabled => SendMessage(new ANPRCSetPowerSaveMsg(enabled));
        _window.OnSetPriorityWatch += slot => SendMessage(new ANPRCSetPriorityWatchMsg(slot));
        _window.OnSetEmcon += enabled => SendMessage(new ANPRCSetEmconMsg(enabled));
        _window.OnSetRetrans += (a, b) => SendMessage(new ANPRCSetRetransMsg(a, b));
        _window.OnPeakAntenna += () => SendMessage(new ANPRCPeakAntennaMsg());
        _window.OnOtar += () => SendMessage(new ANPRCOtarMsg());
        _window.OnSetDwell += kilohertz => SendMessage(new ANPRCSetDwellMsg(kilohertz));
        _window.OnJammerBearing += () => SendMessage(new ANPRCJammerBearingMsg());
        _window.OnKeyTrial += (faction, trial) => SendMessage(new ANPRCKeyTrialMsg(faction, trial));
        _window.OnReturnToAuto += () => SendMessage(new ANPRCReturnToAutoMsg());
        _window.OnManualFrequency += (slot, text) => SendMessage(new ANPRCManualFrequencyMsg(slot, text));
        _window.OnSetSweep += enabled => SendMessage(new ANPRCSetSweepMsg(enabled));
        _window.OnTuneContact += (slot, freq) => SendMessage(new ANPRCTuneContactMsg(slot, freq));
        _window.OnPrintLog += intercepts => SendMessage(new ANPRCPrintLogMsg(intercepts));
        _window.OnQuickSetup += () => SendMessage(new ANPRCQuickSetupMsg());
        _window.OnRenameSlot += (slot, label) => SendMessage(new ANPRCRenameSlotMsg(slot, label));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is not ANPRCRadioState s)
            return;

        _window?.UpdateState(s);
    }
}
