using Content.Shared.CMU14.Radio;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Radio.ANPRC;

/// <summary>
///     COMSEC: whether the traffic is encrypted, what with, and the three things that can be
///     done to the fill. None of the three can be undone, so each asks twice.
/// </summary>
public sealed class ANPRCSecurityPage : BoxContainer
{
    private readonly Label _status;
    private readonly RichTextLabel _statusDetail;
    private readonly Label _fill;
    private readonly RichTextLabel _howTo;
    private readonly RichTextLabel _actionsHelp;
    private readonly ANPRCConfirmButton _zeroize;
    private readonly ANPRCConfirmButton _destroy;
    private readonly ANPRCConfirmButton _recrypto;

    public ANPRCSecurityPage(ANPRCRadioActions radio)
    {
        Orientation = LayoutOrientation.Vertical;
        SeparationOverride = 6;

        var status = ANPRCUi.Column(3, 6);
        _status = ANPRCUi.Label(string.Empty, ANPRCUi.Text, ANPRCUi.Mono(13, true));
        _fill = ANPRCUi.Label(string.Empty, ANPRCUi.TextDim, ANPRCUi.Mono(10));
        _statusDetail = ANPRCUi.Wrapped(string.Empty, ANPRCUi.Text);
        status.AddChild(_status);
        status.AddChild(_fill);
        status.AddChild(_statusDetail);
        AddChild(ANPRCUi.Card(status));

        _howTo = ANPRCUi.Wrapped(Loc.GetString("anprc-op-sec-how-to"), ANPRCUi.TextDim);
        AddChild(_howTo);

        AddChild(ANPRCUi.Heading(Loc.GetString("anprc-op-sec-actions")));

        _actionsHelp = ANPRCUi.Wrapped(Loc.GetString("anprc-op-sec-actions-help"), ANPRCUi.TextDim);
        AddChild(_actionsHelp);

        _zeroize = new ANPRCConfirmButton(
            Loc.GetString("anprc-op-sec-zeroize"),
            Loc.GetString("anprc-op-sec-zeroize-confirm"),
            Loc.GetString("anprc-op-sec-zeroize-tooltip"));
        _zeroize.OnConfirmed += radio.CryptoZeroize;

        _destroy = new ANPRCConfirmButton(
            Loc.GetString("anprc-op-sec-destroy"),
            Loc.GetString("anprc-op-sec-destroy-confirm"),
            Loc.GetString("anprc-op-sec-destroy-tooltip"));
        _destroy.OnConfirmed += radio.CryptoDestroy;

        _recrypto = new ANPRCConfirmButton(
            Loc.GetString("anprc-op-sec-recrypto"),
            Loc.GetString("anprc-op-sec-recrypto-confirm"),
            Loc.GetString("anprc-op-sec-recrypto-tooltip"));
        _recrypto.OnConfirmed += radio.CryptoRecrypto;

        AddChild(_zeroize);
        AddChild(_destroy);
        AddChild(_recrypto);
    }

    public void Update(ANPRCPanelData data)
    {
        var state = data.State;


        if (state.Mode == RadioMode.PlainText)
        {
            _status.Text = Loc.GetString("anprc-op-sec-status-plain");
            _status.FontColorOverride = ANPRCUi.Warn;
            ANPRCUi.SetWrapped(_statusDetail, Loc.GetString("anprc-op-sec-detail-plain"), ANPRCUi.Text);
        }
        else if (data.Secured)
        {
            _status.Text = Loc.GetString("anprc-op-sec-status-secure");
            _status.FontColorOverride = ANPRCUi.Good;
            ANPRCUi.SetWrapped(_statusDetail, Loc.GetString("anprc-op-sec-detail-secure"), ANPRCUi.Text);
        }
        else if (data.HasFill)
        {
            _status.Text = Loc.GetString("anprc-op-sec-status-stale");
            _status.FontColorOverride = ANPRCUi.Warn;
            ANPRCUi.SetWrapped(_statusDetail, Loc.GetString("anprc-op-sec-detail-stale"), ANPRCUi.Text);
        }
        else
        {
            _status.Text = Loc.GetString("anprc-op-sec-status-none");
            _status.FontColorOverride = ANPRCUi.Bad;
            ANPRCUi.SetWrapped(_statusDetail, Loc.GetString("anprc-op-sec-detail-none"), ANPRCUi.Text);
        }

        _fill.Text = data.HasFill
            ? Loc.GetString("anprc-op-sec-fill",
                ("designation", state.CryptoDesignation),
                ("faction", state.CryptoFaction.ToUpperInvariant()))
            : Loc.GetString("anprc-op-sec-fill-none");

        _zeroize.Disabled = !data.Online || !data.HasFill;
        _destroy.Disabled = !data.Online || !data.HasFill;
        _recrypto.Disabled = !data.Online || !data.Secured;
    }
}
