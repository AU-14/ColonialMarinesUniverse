using System.Linq;
using Content.Shared.CMU14.Radio;
using Content.Shared.Radio;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Radio.ANPRC;

/// <summary>
///     Everything the pages need to read the set, derived once per state push. Keeping "is this
///     set actually usable" in one place means the banner, the readout and the pages can never
///     disagree about it.
/// </summary>
public sealed class ANPRCPanelData(IPrototypeManager prototypes)
{
    public ANPRCRadioState State { get; private set; } = default!;

    public ANPRCPanelInfo Relay => State.Info;

    /// <summary>Worn, or staked down as a retrans station.</summary>
    public bool Deployed { get; private set; }

    public bool Powered { get; private set; }

    /// <summary>On and deployed: the set can put something on the air.</summary>
    public bool Online { get; private set; }

    public bool HasActiveNet { get; private set; }

    /// <summary>Online, on a net and not searching. Typing :r will go out.</summary>
    public bool Ready { get; private set; }

    public RadioChannelPrototype? ActiveChannel { get; private set; }

    public bool ActiveIsDirect { get; private set; }

    public RadioFrequency ActiveFrequency { get; private set; }

    public bool HasFill => !string.IsNullOrEmpty(State.CryptoFaction);

    public bool Secured => HasFill && !State.CryptoStale;

    /// <summary>Standard nets that no memory carries yet, in the order the quick setup loads them.</summary>
    public List<ANPRCStandardNet> MissingStandardNets { get; } = new();

    public void SetState(ANPRCRadioState state)
    {
        State = state;

        ActiveIsDirect = state.FrequencyOverrides.TryGetValue(state.ActiveSlot, out var direct);
        ActiveChannel = null;

        if (ActiveIsDirect)
        {
            ActiveFrequency = direct;
        }
        else if (state.Presets.TryGetValue(state.ActiveSlot, out var channel) &&
                 prototypes.TryIndex(channel, out var proto))
        {
            ActiveChannel = proto;
            ActiveFrequency = PlanFrequency(proto);
        }
        else
        {
            ActiveFrequency = RadioFrequency.Off;
        }

        Deployed = state.IsEquipped || state.Planted;
        Powered = state.Enabled;
        Online = Powered && Deployed;
        HasActiveNet = state.ActiveSlot >= 0 && (ActiveIsDirect || ActiveChannel != null);
        Ready = Online && HasActiveNet && !state.SweepEnabled;

        MissingStandardNets.Clear();

        foreach (var net in state.Info.StandardNets)
        {
            if (!state.Presets.Values.Contains(net.Channel))
                MissingStandardNets.Add(net);
        }
    }

    /// <summary>The live frequency off the round's signal plan, not the book value.</summary>
    public RadioFrequency PlanFrequency(RadioChannelPrototype proto)
    {
        return State.ChannelFrequencies.TryGetValue(proto.ID, out var frequency)
            ? frequency
            : proto.Frequency;
    }

    public IEnumerable<RadioChannelPrototype> Channels()
        => prototypes.EnumeratePrototypes<RadioChannelPrototype>();

    public bool TryChannel(ProtoId<RadioChannelPrototype> id, out RadioChannelPrototype? proto)
        => prototypes.TryIndex(id, out proto);

    public string ChannelName(ProtoId<RadioChannelPrototype> id)
        => prototypes.TryIndex(id, out var proto) ? proto.LocalizedName : id.Id;

    public bool IsRelayed(ProtoId<RadioChannelPrototype> channel)
        => Relay.Relaying && Relay.RelayedNets.Contains(channel);

    public IEnumerable<int> Slots => State.SlotLabels.Keys.OrderBy(key => key);

    public string SlotLabel(int slot)
        => State.SlotLabels.TryGetValue(slot, out var label) ? label : $"P{slot + 1}";

    public bool SlotEmpty(int slot)
        => !State.Presets.ContainsKey(slot) && !State.FrequencyOverrides.ContainsKey(slot);

    /// <summary>The first memory holding something, for a set that has nothing selected.</summary>
    public int? FirstLoadedSlot()
    {
        foreach (var slot in Slots)
        {
            if (!SlotEmpty(slot))
                return slot;
        }

        return null;
    }

    public string Callsign
    {
        get
        {
            if (!string.IsNullOrEmpty(State.Callsign))
                return State.Callsign;

            return string.IsNullOrEmpty(State.WearerCallsign)
                ? Loc.GetString("anprc-unknown-station")
                : State.WearerCallsign;
        }
    }

    // ----- formatting ----------------------------------------------------------------------------

    public static string FormatFrequency(RadioFrequency frequency) => frequency.FormatMegahertz();

    // one set of words for both views, so a translation says FH or UHF the same way everywhere
    public static string ModeShort(RadioMode mode) => ANPRCPanelContext.ModeShort(mode);

    public static string BandName(RadioFrequency frequency) => ANPRCPanelContext.BandName(frequency);

    /// <summary>
    ///     A partial fix with the unearned digits masked: 259.237 walks up through 2XX.XXX,
    ///     25X.XXX, 259.XXX and then the whole number.
    /// </summary>
    public static string MaskDigits(string formatted, int tier, int tierMax)
    {
        if (tier >= tierMax)
            return formatted;

        var earned = Math.Max(0, tier);
        var seen = 0;
        var masked = formatted.ToCharArray();

        for (var i = 0; i < masked.Length; i++)
        {
            if (!char.IsAsciiDigit(masked[i]))
                continue;

            seen++;

            if (seen > earned)
                masked[i] = 'X';
        }

        return new string(masked);
    }
}

/// <summary>
///     Everything a page may ask of the radio. The window owns the messages to the server; this
///     is the handle it lends the pages.
/// </summary>
public sealed class ANPRCRadioActions
{
    public Action<int> SelectSlot = _ => { };
    public Action<string> AddSlot = _ => { };
    public Action<int> DeleteSlot = _ => { };
    public Action<int> ClearSlot = _ => { };
    public Action<int, string> RenameSlot = (_, _) => { };
    public Action<int, ProtoId<RadioChannelPrototype>> SetSlotChannel = (_, _) => { };
    public Action<int, string> ManualFrequency = (_, _) => { };
    public Action QuickSetup = () => { };

    public Action TogglePower = () => { };
    public Action ToggleMonitor = () => { };
    public Action<RadioMode> SetMode = _ => { };
    public Action<bool> SetScan = _ => { };
    public Action<RadioTxPower> SetTxPower = _ => { };
    public Action<int> SetSquelch = _ => { };
    public Action<string> SetCallsign = _ => { };

    public Action CryptoZeroize = () => { };
    public Action CryptoDestroy = () => { };
    public Action CryptoRecrypto = () => { };

    public Action RadioCheck = () => { };
    public Action OpenDirectory = () => { };
    public Action OpenPhone = () => { };

    // expert techniques (faceplate) and the guided panel's way back to AUTO
    public Action<bool> SetBurst = _ => { };
    public Action<bool> SetPowerSave = _ => { };
    public Action<int> SetPriorityWatch = _ => { };
    public Action<bool> SetEmcon = _ => { };
    public Action<int, int> SetRetrans = (_, _) => { };
    public Action PeakAntenna = () => { };
    public Action Otar = () => { };
    public Action<int> SetDwell = _ => { };
    public Action JammerBearing = () => { };
    public Action ReturnToAuto = () => { };
    public Action<bool> SetSweep = _ => { };
    public Action<int, RadioFrequency> TuneContact = (_, _) => { };
    public Action<bool> PrintLog = _ => { };

    /// <summary>Panel-local: jump the panel to one of its pages.</summary>
    public Action<ANPRCPage> ShowPage = _ => { };
}

public enum ANPRCPage : byte
{
    Nets,
    Log,
    Security,
    Search,
    Settings,
}
