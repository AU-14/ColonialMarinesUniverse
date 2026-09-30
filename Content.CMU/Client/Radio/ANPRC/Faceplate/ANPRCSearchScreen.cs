using System.Linq;

namespace Content.Client.CMU14.Radio.ANPRC;

/// <summary>
///     SRCH: the search receiver and what it has fixed. Walking the band takes the set off every
///     net it holds, so the screen says so on its own line rather than burying it. The faceplate
///     can also park the head on one partial contact (DWELL) to fix it twice as fast, and take
///     bearings on a jammer until two of them cross.
/// </summary>
public sealed class ANPRCSearchScreen : ANPRCScreen
{
    public override string Title => Loc.GetString("anprc-fp-srch-title");

    public override string Status(ANPRCPanelContext context)
    {
        if (!context.Deployed)
            return Loc.GetString("anprc-fp-stowed");

        if (context.State.Expert.SweepDwellKilohertz >= 0)
            return Loc.GetString("anprc-fp-srch-dwelling");

        return Loc.GetString(context.State.SweepEnabled ? "anprc-fp-srch-searching" : "anprc-fp-srch-idle");
    }

    public override void Build(ANPRCPanelContext context, List<ANPRCScreenRow> rows)
    {
        var state = context.State;
        var expert = state.Expert;
        var searching = state.SweepEnabled && context.Online;
        var dwelling = searching && expert.SweepDwellKilohertz >= 0;

        rows.Add(new ANPRCScreenRow
        {
            Label = Loc.GetString(searching ? "anprc-fp-srch-stop" : "anprc-fp-srch-start"),
            Value = Loc.GetString(!context.Deployed ? "anprc-fp-stowed" : searching ? "anprc-fp-srch-running" : "anprc-fp-srch-ready"),
            Lit = searching,
            Style = searching ? ANPRCRowStyle.Warn : ANPRCRowStyle.Normal,
            // the receiver is the one thing here that needs an antenna in the air, and the server
            // refuses to start a sweep off a pack that is neither worn nor staked down
            Activate = context.Online
                ? () =>
                {
                    Radio.SetSweep(!state.SweepEnabled);
                    Host.Acknowledge(Loc.GetString(state.SweepEnabled ? "anprc-fp-ack-search-stopped" : "anprc-fp-ack-search-running"));
                }
                : null,
        });

        rows.Add(ANPRCScreenRow.Info(
            Loc.GetString("anprc-fp-srch-head"),
            searching ? ANPRCPanelContext.FormatFrequency(state.SweepPosition) : "---.---",
            searching ? ANPRCRowStyle.Good : ANPRCRowStyle.Note));

        if (dwelling)
        {
            rows.Add(new ANPRCScreenRow
            {
                Label = Loc.GetString("anprc-fp-srch-release"),
                Value = Loc.GetString("anprc-fp-srch-dwelling"),
                Lit = true,
                Activate = () =>
                {
                    Radio.SetDwell(-1);
                    Host.Acknowledge(Loc.GetString("anprc-fp-ack-dwell-off"));
                },
            });
        }

        rows.Add(searching
            ? ANPRCScreenRow.Info(Loc.GetString("anprc-fp-srch-state"), Loc.GetString("anprc-fp-srch-state-inhibited"), ANPRCRowStyle.Warn)
            : ANPRCScreenRow.Note(Loc.GetString(context.Deployed
                ? "anprc-fp-srch-note-drops"
                : "anprc-fp-srch-note-deploy")));

        rows.Add(ANPRCScreenRow.Heading(Loc.GetString("anprc-fp-srch-contacts")));

        if (state.SweepContacts.Count == 0)
        {
            rows.Add(ANPRCScreenRow.Note(Loc.GetString("anprc-fp-srch-no-contacts")));
        }
        else
        {
            BuildContacts(context, rows, searching);
        }

        // ----- key analysis -----------------------------------------------------------------------

        rows.Add(ANPRCScreenRow.Heading(Loc.GetString("anprc-fp-srch-keys")));

        if (expert.KeyAnalyses.Count == 0)
        {
            rows.Add(ANPRCScreenRow.Note(Loc.GetString("anprc-fp-srch-keys-none")));
        }
        else
        {
            foreach (var work in expert.KeyAnalyses)
            {
                var faction = work.Faction;

                rows.Add(new ANPRCScreenRow
                {
                    Label = faction.ToUpperInvariant(),
                    Value = work.Broken
                        ? Loc.GetString("anprc-fp-key-broken")
                        : Loc.GetString("anprc-fp-key-depth", ("depth", work.Depth), ("max", work.DepthMax)),
                    Style = work.Broken ? ANPRCRowStyle.Good : ANPRCRowStyle.Normal,
                    Activate = () => Host.Push(new ANPRCKeyScreen(faction)),
                });
            }
        }

        // ----- jammer direction finding ------------------------------------------------------------

        rows.Add(ANPRCScreenRow.Heading(Loc.GetString("anprc-fp-srch-jammer")));

        if (!expert.Jammed && !expert.JammerBearingTaken)
        {
            rows.Add(ANPRCScreenRow.Note(Loc.GetString("anprc-fp-srch-jammer-clear")));
            return;
        }

        if (expert.JammerBearingTaken)
        {
            rows.Add(ANPRCScreenRow.Info(
                Loc.GetString("anprc-fp-srch-jammer-first"),
                ANPRCPanelContext.Bearing(expert.JammerBearingDegrees),
                ANPRCRowStyle.Warn));

            var moved = (int) expert.JammerBaselineMoved;
            var needed = (int) expert.JammerBaselineNeeded;

            rows.Add(ANPRCScreenRow.Info(
                Loc.GetString("anprc-fp-srch-jammer-baseline"),
                Loc.GetString("anprc-fp-srch-jammer-baseline-value", ("moved", moved), ("needed", needed)),
                moved >= needed ? ANPRCRowStyle.Good : ANPRCRowStyle.Note));
        }

        rows.Add(new ANPRCScreenRow
        {
            Label = Loc.GetString(expert.JammerBearingTaken ? "anprc-fp-srch-jammer-second" : "anprc-fp-srch-jammer-take"),
            Value = ">",
            Activate = context.Online
                ? () =>
                {
                    Radio.JammerBearing();
                    Host.Acknowledge(Loc.GetString("anprc-fp-ack-bearing"));
                }
                : null,
        });

        rows.Add(ANPRCScreenRow.Note(Loc.GetString("anprc-fp-srch-jammer-note")));
    }

    private void BuildContacts(ANPRCPanelContext context, List<ANPRCScreenRow> rows, bool searching)
    {
        var state = context.State;
        var expert = state.Expert;
        var slot = state.ActiveSlot;
        var canTune = slot >= 0 && state.SlotLabels.ContainsKey(slot);

        foreach (var contact in state.SweepContacts)
        {
            if (!contact.Resolved)
            {
                var masked = ANPRCPanelContext.MaskDigits(
                    ANPRCPanelContext.FormatFrequency(contact.Frequency),
                    contact.Tier,
                    contact.TierMax);

                var kilohertz = contact.Frequency.Kilohertz;
                var dwelled = expert.SweepDwellKilohertz == kilohertz;

                // a partial fix is worth parking the head on: ENT dwells there
                rows.Add(new ANPRCScreenRow
                {
                    Label = "~" + masked,
                    Value = dwelled
                        ? Loc.GetString("anprc-fp-srch-contact-dwell", ("tier", contact.Tier), ("max", contact.TierMax))
                        : Loc.GetString("anprc-fp-srch-contact-partial", ("tier", contact.Tier), ("max", contact.TierMax)),
                    Lit = dwelled,
                    Style = ANPRCRowStyle.Warn,
                    Activate = searching && !dwelled
                        ? () =>
                        {
                            Radio.SetDwell(kilohertz);
                            Host.Acknowledge(Loc.GetString("anprc-fp-ack-dwell-on"));
                        }
                        : null,
                });

                continue;
            }

            var frequency = ANPRCPanelContext.FormatFrequency(contact.Frequency);
            var name = contact.ChannelName.ToUpperInvariant();

            // an own net was never work. it is listed so the band reads honestly, dimmed so it
            // never looks like something the operator won
            if (contact.Known)
            {
                rows.Add(ANPRCScreenRow.Info(frequency, Loc.GetString("anprc-fp-srch-contact-own", ("net", name)), ANPRCRowStyle.Note));
                continue;
            }

            var captured = contact.Frequency;

            rows.Add(new ANPRCScreenRow
            {
                Label = frequency,
                Value = canTune ? name : Loc.GetString("anprc-fp-srch-contact-no-mem", ("net", name)),
                Style = ANPRCRowStyle.Good,
                Activate = canTune
                    ? () =>
                    {
                        Radio.TuneContact(slot, captured);
                        Host.Acknowledge(Loc.GetString("anprc-fp-ack-contact-tuned"));
                    }
                    : null,
            });
        }

        if (!canTune && state.SweepContacts.Any(contact => contact.Resolved && !contact.Known))
            rows.Add(ANPRCScreenRow.Note(Loc.GetString("anprc-fp-srch-note-select-memory")));

        if (searching && state.SweepContacts.Any(contact => !contact.Resolved))
            rows.Add(ANPRCScreenRow.Note(Loc.GetString("anprc-fp-srch-note-dwell")));
    }
}
