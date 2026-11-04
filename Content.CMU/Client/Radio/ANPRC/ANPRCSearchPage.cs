using System.Text;
using Content.Shared.Radio;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Radio.ANPRC;

/// <summary>
///     The search receiver: walk the band for other people's nets. It costs the operator every
///     net they hold while it runs, and the page says so before they press anything.
/// </summary>
public sealed class ANPRCSearchPage : BoxContainer
{
    private readonly ANPRCRadioActions _radio;

    private readonly RichTextLabel _intro;
    private readonly Button _toggle;
    private readonly Label _head;
    private readonly RichTextLabel _warning;
    private readonly BoxContainer _contacts;

    private string _key = string.Empty;
    private bool _searching;

    public ANPRCSearchPage(ANPRCRadioActions radio)
    {
        _radio = radio;

        Orientation = LayoutOrientation.Vertical;
        SeparationOverride = 6;

        _intro = ANPRCUi.Wrapped(Loc.GetString("anprc-op-search-intro"), ANPRCUi.TextDim);
        AddChild(_intro);

        _warning = ANPRCUi.Wrapped(string.Empty, ANPRCUi.Warn);
        AddChild(_warning);

        var row = ANPRCUi.Row(6);
        _toggle = new Button { MinWidth = 140 };
        _toggle.OnPressed += _ => _radio.SetSweep(!_searching);
        _head = ANPRCUi.Label(string.Empty, ANPRCUi.LcdDim, ANPRCUi.Mono(12, true));
        _head.VerticalAlignment = VAlignment.Center;
        row.AddChild(_toggle);
        row.AddChild(_head);
        AddChild(row);

        AddChild(ANPRCUi.Heading(Loc.GetString("anprc-op-search-contacts")));

        _contacts = ANPRCUi.Column(3);
        AddChild(_contacts);
    }

    public void Update(ANPRCPanelData data)
    {
        var state = data.State;
        var searching = state.SweepEnabled && data.Online;
        _searching = state.SweepEnabled;


        // the pressed look carries the running state, the press itself asks for the opposite
        _toggle.Pressed = searching;
        _toggle.Disabled = !data.Online;
        _toggle.Text = Loc.GetString(searching ? "anprc-op-search-stop" : "anprc-op-search-start");
        _toggle.ToolTip = data.Online ? null : Loc.GetString("anprc-op-search-offline");

        _head.Text = searching
            ? Loc.GetString("anprc-op-search-head", ("frequency", ANPRCPanelData.FormatFrequency(state.SweepPosition)))
            : Loc.GetString("anprc-op-search-idle");
        _head.FontColorOverride = searching ? ANPRCUi.LcdBright : ANPRCUi.LcdDim;

        ANPRCUi.SetWrapped(_warning, Loc.GetString(searching
                ? "anprc-op-search-running-warning"
                : data.Online ? "anprc-op-search-cost" : "anprc-op-search-offline"),
            searching ? ANPRCUi.Warn : ANPRCUi.TextDim);

        var key = ContactsKey(data);

        if (key == _key)
            return;

        _key = key;
        RebuildContacts(data);
    }

    private static string ContactsKey(ANPRCPanelData data)
    {
        var builder = new StringBuilder();

        foreach (var contact in data.State.SweepContacts)
        {
            builder.Append(contact.Frequency.Kilohertz).Append(contact.Tier).Append(contact.Resolved).Append('|');
        }

        foreach (var slot in data.Slots)
        {
            builder.Append(slot).Append(data.SlotLabel(slot)).Append('|');
        }

        return builder.ToString();
    }

    private void RebuildContacts(ANPRCPanelData data)
    {
        _contacts.RemoveAllChildren();

        var state = data.State;

        if (state.SweepContacts.Count == 0)
        {
            _contacts.AddChild(ANPRCUi.Wrapped(Loc.GetString("anprc-op-search-no-contacts"), ANPRCUi.TextDim));
            return;
        }

        foreach (var contact in state.SweepContacts)
        {
            var card = ANPRCUi.Column(3, 5);

            if (!contact.Resolved)
            {
                var masked = ANPRCPanelData.MaskDigits(
                    ANPRCPanelData.FormatFrequency(contact.Frequency),
                    contact.Tier,
                    contact.TierMax);

                card.AddChild(ANPRCUi.Label(
                    Loc.GetString("anprc-op-search-partial", ("frequency", masked), ("tier", contact.Tier), ("max", contact.TierMax)),
                    ANPRCUi.Warn,
                    ANPRCUi.Mono(11, true)));
                card.AddChild(ANPRCUi.Wrapped(Loc.GetString("anprc-op-search-partial-help"), ANPRCUi.TextDim));

                _contacts.AddChild(ANPRCUi.Card(card));
                continue;
            }

            var frequency = ANPRCPanelData.FormatFrequency(contact.Frequency);

            // an own net was never work. listed so the band reads honestly, dimmed so it never
            // looks like something the operator won
            if (contact.Known)
            {
                card.AddChild(ANPRCUi.Label(
                    Loc.GetString("anprc-op-search-own", ("frequency", frequency), ("net", contact.ChannelName)),
                    ANPRCUi.TextDim,
                    ANPRCUi.Mono(11)));

                _contacts.AddChild(ANPRCUi.Card(card));
                continue;
            }

            card.AddChild(ANPRCUi.Label(
                Loc.GetString("anprc-op-search-fixed", ("frequency", frequency), ("net", contact.ChannelName.ToUpperInvariant())),
                ANPRCUi.Good,
                ANPRCUi.Mono(11, true)));

            // tuning a fix writes its number into a memory, so the operator never copies digits
            // off one screen and back into another
            var tuneRow = ANPRCUi.Row(4);
            tuneRow.AddChild(ANPRCUi.Label(Loc.GetString("anprc-op-search-tune-into"), ANPRCUi.Text));

            var captured = contact.Frequency;
            var any = false;

            foreach (var slot in data.Slots)
            {
                var target = slot;
                tuneRow.AddChild(ANPRCUi.Button(data.SlotLabel(slot), Loc.GetString("anprc-op-search-tune-tooltip"),
                    () => _radio.TuneContact(target, captured)));
                any = true;
            }

            if (!any)
                tuneRow.AddChild(ANPRCUi.Label(Loc.GetString("anprc-op-search-no-memory"), ANPRCUi.Warn));

            card.AddChild(tuneRow);
            _contacts.AddChild(ANPRCUi.Card(card, ANPRCUi.Good));
        }
    }
}
