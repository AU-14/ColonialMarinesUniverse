using Robust.Shared.Timing;

namespace Content.Client.CMU14.Radio.ANPRC;

/// <summary>
///     SEC: the fill the set is holding and the things that can be done to it. None of the fill
///     actions can be walked back, so each arms on the first press and fires on the second. The
///     over-the-air rekey is the faceplate's own: after a recrypto it carries the new key out to the
///     side's sets in reach instead of every operator needing a fresh card.
/// </summary>
public sealed class ANPRCComsecScreen(IGameTiming timing) : ANPRCScreen
{
    private static readonly TimeSpan ArmWindow = TimeSpan.FromSeconds(4);

    private enum ANPRCFillAction : byte
    {
        None,
        Zeroize,
        Destroy,
        Recrypto,
        Otar,
    }

    private ANPRCFillAction _armed;
    private TimeSpan _armedUntil;

    /// <summary>Where the function switch is resting, printed so a pull to LD or Z leaves a trace.</summary>
    public ANPRCKnobPosition Switch { get; set; } = ANPRCKnobPosition.Off;

    public override string Title => Loc.GetString("anprc-fp-sec-title");

    public override string Status(ANPRCPanelContext context)
    {
        var state = context.State;
        var hasFill = !string.IsNullOrEmpty(state.CryptoFaction);

        return Loc.GetString(hasFill
            ? state.CryptoStale ? "anprc-fp-sec-superseded" : "anprc-fp-sec-secured"
            : "anprc-fp-sec-unsecured");
    }

    /// <summary>
    ///     Throwing the switch to Z arms the wipe from the hardware rather than from the screen,
    ///     which is what the detent is for. Reports whether there was anything to arm.
    /// </summary>
    public bool ArmZeroize(ANPRCPanelContext context)
    {
        if (!context.Powered || string.IsNullOrEmpty(context.State.CryptoFaction))
            return false;

        _armed = ANPRCFillAction.Zeroize;
        _armedUntil = timing.CurTime + ArmWindow;

        return true;
    }

    private bool IsArmed(ANPRCFillAction action)
        => _armed == action && timing.CurTime <= _armedUntil;

    private void Arm(ANPRCFillAction action, string armedText, Action commit)
    {
        if (IsArmed(action))
        {
            _armed = ANPRCFillAction.None;
            commit();

            return;
        }

        _armed = action;
        _armedUntil = timing.CurTime + ArmWindow;

        Host.Acknowledge(armedText);
    }

    public override void Build(ANPRCPanelContext context, List<ANPRCScreenRow> rows)
    {
        var state = context.State;
        var expert = state.Expert;
        var hasFill = !string.IsNullOrEmpty(state.CryptoFaction);
        var secured = hasFill && !state.CryptoStale;

        // the arming window runs on the clock, so it is checked as the rows are built rather than
        // left to whatever pressed a key last
        if (_armed != ANPRCFillAction.None && timing.CurTime > _armedUntil)
            _armed = ANPRCFillAction.None;

        rows.Add(ANPRCScreenRow.Info(
            Loc.GetString("anprc-fp-sec-switch"),
            Switch switch
            {
                ANPRCKnobPosition.Load => Loc.GetString("anprc-fp-sec-switch-load"),
                ANPRCKnobPosition.Zeroize => Loc.GetString("anprc-fp-sec-switch-zeroize"),
                ANPRCKnobPosition.Off => Loc.GetString("anprc-fp-off"),
                _ => Loc.GetString("anprc-fp-sec-switch-operating", ("position", ANPRCModeKnob.Legend(Switch))),
            },
            Switch switch
            {
                ANPRCKnobPosition.Zeroize => ANPRCRowStyle.Bad,
                ANPRCKnobPosition.Load => ANPRCRowStyle.Warn,
                ANPRCKnobPosition.Off => ANPRCRowStyle.Note,
                _ => ANPRCRowStyle.Good,
            }));

        rows.Add(ANPRCScreenRow.Info(
            Loc.GetString("anprc-fp-sec-fill"),
            hasFill ? state.CryptoDesignation : Loc.GetString("anprc-fp-none"),
            secured ? ANPRCRowStyle.Good : hasFill ? ANPRCRowStyle.Warn : ANPRCRowStyle.Bad));

        if (hasFill)
        {
            rows.Add(ANPRCScreenRow.Info(Loc.GetString("anprc-fp-sec-issued-to"), state.CryptoFaction.ToUpperInvariant()));

            if (state.CryptoStale)
                rows.Add(ANPRCScreenRow.Note(Loc.GetString("anprc-fp-sec-note-superseded")));
        }
        else
        {
            rows.Add(ANPRCScreenRow.Note(Loc.GetString("anprc-fp-sec-note-insert")));
        }

        rows.Add(ANPRCScreenRow.Info(
            Loc.GetString("anprc-fp-sec-traffic"),
            Loc.GetString(secured ? "anprc-fp-sec-encrypted" : "anprc-fp-sec-in-clear"),
            secured ? ANPRCRowStyle.Good : ANPRCRowStyle.Warn));

        rows.Add(ANPRCScreenRow.Heading(Loc.GetString("anprc-fp-sec-fill-actions")));

        rows.Add(new ANPRCScreenRow
        {
            Label = Loc.GetString(IsArmed(ANPRCFillAction.Zeroize) ? "anprc-fp-sec-confirm-wipe" : "anprc-fp-sec-zeroize"),
            Value = Loc.GetString(hasFill ? "anprc-fp-sec-eject" : "anprc-fp-sec-no-fill"),
            Armed = IsArmed(ANPRCFillAction.Zeroize),
            Style = hasFill ? ANPRCRowStyle.Normal : ANPRCRowStyle.Note,
            Activate = context.Powered && hasFill
                ? () => Arm(ANPRCFillAction.Zeroize, Loc.GetString("anprc-fp-ack-zeroize-armed"), () =>
                {
                    Radio.CryptoZeroize();
                    Host.Acknowledge(Loc.GetString("anprc-fp-ack-zeroized"));
                })
                : null,
        });

        rows.Add(new ANPRCScreenRow
        {
            Label = Loc.GetString(IsArmed(ANPRCFillAction.Destroy) ? "anprc-fp-sec-confirm-burn" : "anprc-fp-sec-destroy"),
            Value = Loc.GetString(hasFill ? "anprc-fp-sec-burn" : "anprc-fp-sec-no-fill"),
            Armed = IsArmed(ANPRCFillAction.Destroy),
            Style = hasFill ? ANPRCRowStyle.Normal : ANPRCRowStyle.Note,
            Activate = context.Powered && hasFill
                ? () => Arm(ANPRCFillAction.Destroy, Loc.GetString("anprc-fp-ack-destroy-armed"), () =>
                {
                    Radio.CryptoDestroy();
                    Host.Acknowledge(Loc.GetString("anprc-fp-ack-destroyed"));
                })
                : null,
        });

        rows.Add(new ANPRCScreenRow
        {
            Label = Loc.GetString(IsArmed(ANPRCFillAction.Recrypto) ? "anprc-fp-sec-confirm-recrypto" : "anprc-fp-sec-recrypto"),
            Value = Loc.GetString(secured ? "anprc-fp-sec-supersede" : "anprc-fp-sec-needs-fill"),
            Armed = IsArmed(ANPRCFillAction.Recrypto),
            Style = secured ? ANPRCRowStyle.Normal : ANPRCRowStyle.Note,
            Activate = context.Powered && secured
                ? () => Arm(ANPRCFillAction.Recrypto, Loc.GetString("anprc-fp-ack-recrypto-armed"), () =>
                {
                    Radio.CryptoRecrypto();
                    Host.Acknowledge(Loc.GetString("anprc-fp-ack-recrypto"));
                })
                : null,
        });

        rows.Add(ANPRCScreenRow.Note(Loc.GetString("anprc-fp-sec-note-authority")));
        rows.Add(ANPRCScreenRow.Note(Loc.GetString("anprc-fp-sec-note-supersedes")));

        // ----- over-the-air rekey --------------------------------------------------------------------

        rows.Add(ANPRCScreenRow.Heading(Loc.GetString("anprc-fp-sec-otar")));

        rows.Add(new ANPRCScreenRow
        {
            Label = Loc.GetString(IsArmed(ANPRCFillAction.Otar) ? "anprc-fp-sec-confirm-otar" : "anprc-fp-sec-otar-send"),
            Value = !secured
                ? Loc.GetString("anprc-fp-sec-needs-fill")
                : Loc.GetString("anprc-fp-sec-otar-targets", ("count", expert.OtarTargets)),
            Armed = IsArmed(ANPRCFillAction.Otar),
            Style = expert.OtarReady ? ANPRCRowStyle.Normal : ANPRCRowStyle.Note,
            Activate = context.Online && expert.OtarReady
                ? () => Arm(ANPRCFillAction.Otar, Loc.GetString("anprc-fp-ack-otar-armed"), () =>
                {
                    Radio.Otar();
                    Host.Acknowledge(Loc.GetString("anprc-fp-ack-otar"));
                })
                : null,
        });

        rows.Add(ANPRCScreenRow.Note(Loc.GetString("anprc-fp-sec-note-otar")));
        rows.Add(ANPRCScreenRow.Note(Loc.GetString("anprc-fp-sec-note-otar-risk")));
    }
}
