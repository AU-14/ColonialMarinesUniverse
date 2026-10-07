using System.Linq;
using Content.Shared.CMU14.Radio;
using Content.Shared.Radio;
using Robust.Shared.Prototypes;

namespace Content.Client.CMU14.Radio.ANPRC;

/// <summary>
///     PGM: the net memories the set carries and which one it is working. The screen the panel
///     opens on, because it is where an operator spends the round.
/// </summary>
public sealed class ANPRCNetScreen : ANPRCScreen
{
    public override string Title => Loc.GetString("anprc-fp-pgm-title");

    public override string Status(ANPRCPanelContext context)
        => context.State.SlotLabels.Count + "/" + ANPRCRadioComponent.MaxSlots;

    public override void Build(ANPRCPanelContext context, List<ANPRCScreenRow> rows)
    {
        var state = context.State;

        // the standard load, one line on the set the way a stored plan is on the real thing.
        // it fills free memories only and never overwrites a tuned one
        var missing = state.Info.StandardNets
            .Where(net => !state.Presets.Values.Contains(net.Channel))
            .Select(net => net.Label)
            .ToList();

        if (missing.Count > 0)
        {
            rows.Add(new ANPRCScreenRow
            {
                Label = Loc.GetString("anprc-fp-pgm-load-std"),
                Value = string.Join(" ", missing),
                Style = ANPRCRowStyle.Warn,
                Activate = () =>
                {
                    Radio.QuickSetup();
                    Host.Acknowledge(Loc.GetString("anprc-fp-ack-std-loading"));
                },
            });
        }

        if (state.SlotLabels.Count == 0)
            rows.Add(ANPRCScreenRow.Note(Loc.GetString("anprc-fp-pgm-no-nets")));

        foreach (var slot in state.SlotLabels.Keys.OrderBy(key => key))
        {
            var captured = slot;
            var active = slot == state.ActiveSlot;

            rows.Add(new ANPRCScreenRow
            {
                Label = (active ? "*" : " ") + context.SlotLabel(slot),
                Value = context.SlotContents(slot),
                Lit = active,
                Activate = () => Host.Push(new ANPRCMemoryScreen(captured)),
            });
        }

        if (state.SlotLabels.Count < ANPRCRadioComponent.MaxSlots)
        {
            rows.Add(new ANPRCScreenRow
            {
                Label = Loc.GetString("anprc-fp-pgm-add"),
                Value = "+",
                Activate = () => Host.BeginEntry(new ANPRCEntry
                {
                    Prompt = Loc.GetString("anprc-fp-entry-label"),
                    Mode = ANPRCEntryMode.Letters,
                    MaxLength = ANPRCRadioComponent.MaxLabelLength,
                    Commit = label =>
                    {
                        Radio.AddSlot(label);
                        Host.Acknowledge(Loc.GetString("anprc-fp-ack-memory-added", ("label", label)));
                    },
                }),
            });
        }

        rows.Add(ANPRCScreenRow.Note(Loc.GetString("anprc-fp-pgm-note-ent")));
        rows.Add(ANPRCScreenRow.Note(Loc.GetString("anprc-fp-pgm-note-working")));
    }
}

/// <summary>
///     What can be done to one memory. A real set puts the verbs on their own page rather than
///     hanging three buttons off every row, and it means the memory list stays readable.
/// </summary>
public sealed class ANPRCMemoryScreen(int slot) : ANPRCScreen
{
    public override string Title => Loc.GetString("anprc-fp-memory-title");

    public override string Status(ANPRCPanelContext context) => context.SlotLabel(slot);

    public override void Build(ANPRCPanelContext context, List<ANPRCScreenRow> rows)
    {
        var state = context.State;

        // deleted out from under the cursor: back out rather than working a memory that is gone
        if (!state.SlotLabels.ContainsKey(slot))
        {
            Host.Pop();
            return;
        }

        rows.Add(ANPRCScreenRow.Info(Loc.GetString("anprc-fp-memory-loaded"), context.SlotContents(slot)));

        rows.Add(new ANPRCScreenRow
        {
            Label = Loc.GetString("anprc-fp-memory-work"),
            Value = slot == state.ActiveSlot ? Loc.GetString("anprc-fp-memory-active") : string.Empty,
            Lit = slot == state.ActiveSlot,
            Activate = () =>
            {
                Radio.SelectSlot(slot);
                Host.Acknowledge(Loc.GetString("anprc-fp-ack-net-selected", ("label", context.SlotLabel(slot))));
                Host.Pop();
            },
        });

        rows.Add(new ANPRCScreenRow
        {
            Label = Loc.GetString("anprc-fp-memory-tune"),
            Value = ">",
            Activate = () => Host.Push(new ANPRCNetListScreen(slot)),
        });

        rows.Add(new ANPRCScreenRow
        {
            Label = Loc.GetString("anprc-fp-memory-key-freq"),
            Value = ">",
            Activate = () => Host.BeginEntry(new ANPRCEntry
            {
                Prompt = Loc.GetString("anprc-fp-entry-frequency", ("slot", context.SlotLabel(slot))),
                Mode = ANPRCEntryMode.Frequency,
                MaxLength = 6,
                Commit = text => Radio.ManualFrequency(slot, text),
            }),
        });

        rows.Add(new ANPRCScreenRow
        {
            Label = Loc.GetString("anprc-fp-memory-empty"),
            Activate = () =>
            {
                Radio.ClearSlot(slot);
                Host.Acknowledge(Loc.GetString("anprc-fp-ack-memory-emptied"));
            },
        });

        rows.Add(new ANPRCScreenRow
        {
            Label = Loc.GetString("anprc-fp-memory-delete"),
            Activate = () =>
            {
                Radio.DeleteSlot(slot);
                Host.Acknowledge(Loc.GetString("anprc-fp-ack-memory-deleted"));
                Host.Pop();
            },
        });

        rows.Add(new ANPRCScreenRow
        {
            Label = Loc.GetString("anprc-fp-memory-rename"),
            Activate = () => Host.BeginEntry(new ANPRCEntry
            {
                Prompt = Loc.GetString("anprc-fp-entry-label"),
                Mode = ANPRCEntryMode.Letters,
                MaxLength = ANPRCRadioComponent.MaxLabelLength,
                Initial = context.SlotLabel(slot),
                Commit = label =>
                {
                    Radio.RenameSlot(slot, label);
                    Host.Acknowledge(Loc.GetString("anprc-fp-ack-memory-renamed", ("label", label)));
                },
            }),
        });
    }
}

/// <summary>The nets this set is entitled to work, as a list the cursor runs down.</summary>
public sealed class ANPRCNetListScreen(int slot) : ANPRCScreen
{
    public override string Title => Loc.GetString("anprc-fp-netlist-title");

    public override string Status(ANPRCPanelContext context) => context.SlotLabel(slot);

    public override void Build(ANPRCPanelContext context, List<ANPRCScreenRow> rows)
    {
        var state = context.State;
        var operatorFaction = state.OperatorFaction;
        var listed = 0;

        foreach (var proto in context.Channels().OrderBy(proto => proto.LocalizedName))
        {
            if (proto.Frequency == RadioFrequency.Off)
                continue;

            // own-faction nets always list. a foreign net lists once the search receiver has
            // fixed it, which the server proves by putting its frequency in the state at all
            var ownNet = string.IsNullOrEmpty(operatorFaction) ||
                         string.Equals(proto.Faction, operatorFaction, StringComparison.OrdinalIgnoreCase);

            var discovered = !ownNet &&
                             !string.IsNullOrEmpty(proto.Faction) &&
                             state.ChannelFrequencies.ContainsKey(proto.ID);

            if (!ownNet && !discovered)
                continue;

            var captured = proto.ID;
            var frequency = ANPRCPanelContext.FormatFrequency(context.PlanFrequency(proto));

            rows.Add(new ANPRCScreenRow
            {
                Label = discovered
                    ? Loc.GetString("anprc-fp-netlist-intercept", ("net", proto.LocalizedName.ToUpperInvariant()))
                    : proto.LocalizedName.ToUpperInvariant(),
                Value = frequency,
                Style = discovered ? ANPRCRowStyle.Warn : ANPRCRowStyle.Normal,
                Activate = () =>
                {
                    Radio.SetSlotChannel(slot, new ProtoId<RadioChannelPrototype>(captured));
                    Host.Acknowledge(Loc.GetString("anprc-fp-ack-net-loaded"));
                    Host.Pop();
                },
            });

            listed++;
        }

        if (listed == 0)
            rows.Add(ANPRCScreenRow.Note(Loc.GetString("anprc-fp-netlist-none")));
    }
}
