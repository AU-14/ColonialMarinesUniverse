using System.Linq;
using System.Text;
using Content.Shared.CMU14.Radio;

namespace Content.Client.CMU14.Radio.ANPRC;

/// <summary>
///     KEY: working one enemy faction's COMSEC key. Every line the set intercepts on a net it has
///     fixed banks a trial; a trial tests a whole guessed key and the set answers with how many
///     symbols sit in the right place. The screen only lays out what the server has graded - the
///     key never reaches the client.
/// </summary>
public sealed class ANPRCKeyScreen(string faction) : ANPRCScreen
{
    public override string Title => Loc.GetString("anprc-fp-key-title", ("faction", faction.ToUpperInvariant()));

    public override string Status(ANPRCPanelContext context)
    {
        if (Find(context) is not { } work)
            return string.Empty;

        return work.Broken
            ? Loc.GetString("anprc-fp-key-broken")
            : Loc.GetString("anprc-fp-key-depth", ("depth", work.Depth), ("max", work.DepthMax));
    }

    public override void Build(ANPRCPanelContext context, List<ANPRCScreenRow> rows)
    {
        if (Find(context) is not { } work)
        {
            rows.Add(ANPRCScreenRow.Note(Loc.GetString("anprc-fp-key-gone")));
            return;
        }

        if (work.Broken)
        {
            rows.Add(ANPRCScreenRow.Info(
                Loc.GetString("anprc-fp-key-state"),
                Loc.GetString("anprc-fp-key-broken"),
                ANPRCRowStyle.Good));
            rows.Add(ANPRCScreenRow.Note(Loc.GetString("anprc-fp-key-broken-note")));
        }
        else
        {
            var canTest = context.Online && work.Depth > 0;

            rows.Add(new ANPRCScreenRow
            {
                Label = Loc.GetString("anprc-fp-key-test"),
                Value = Loc.GetString("anprc-fp-key-depth", ("depth", work.Depth), ("max", work.DepthMax)),
                Style = work.Depth > 0 ? ANPRCRowStyle.Normal : ANPRCRowStyle.Warn,
                Activate = canTest
                    ? () => Host.BeginEntry(new ANPRCEntry
                    {
                        Prompt = Loc.GetString("anprc-fp-key-prompt"),
                        Mode = ANPRCEntryMode.Letters,
                        MaxLength = ANPRCKeyAnalysis.KeyLength,
                        Commit = text =>
                        {
                            if (ANPRCKeyAnalysis.Normalize(text) is not { } trial)
                            {
                                Host.Acknowledge(Loc.GetString("anprc-fp-ack-key-bad"));
                                return;
                            }

                            Radio.KeyTrial(faction, trial);
                            Host.Acknowledge(Loc.GetString("anprc-fp-ack-key-sent"));
                        },
                    })
                    : null,
            });

            rows.Add(ANPRCScreenRow.Note(Loc.GetString(work.Depth > 0
                ? "anprc-fp-key-rules"
                : "anprc-fp-key-no-depth")));
        }

        rows.Add(ANPRCScreenRow.Heading(Loc.GetString("anprc-fp-key-trials")));

        if (work.Trials.Count == 0)
        {
            rows.Add(ANPRCScreenRow.Note(Loc.GetString("anprc-fp-key-no-trials")));
            return;
        }

        // newest first: the last answer is the one the next guess is built on
        for (var i = work.Trials.Count - 1; i >= 0; i--)
        {
            var trial = work.Trials[i];

            rows.Add(ANPRCScreenRow.Info(
                Spaced(trial.Key),
                Loc.GetString("anprc-fp-key-hits", ("hits", trial.Hits), ("length", ANPRCKeyAnalysis.KeyLength)),
                trial.Hits == ANPRCKeyAnalysis.KeyLength ? ANPRCRowStyle.Good
                : trial.Hits == 0 ? ANPRCRowStyle.Note
                : ANPRCRowStyle.Normal));
        }
    }

    private ANPRCKeyAnalysisState? Find(ANPRCPanelContext context)
    {
        return context.State.Expert.KeyAnalyses.FirstOrDefault(work => work.Faction == faction);
    }

    // "ACBDEG" -> "A C B D E G". Substring, never a char: string + char trips the client sandbox
    private static string Spaced(string key)
    {
        var builder = new StringBuilder(key.Length * 2);

        for (var i = 0; i < key.Length; i++)
        {
            if (i > 0)
                builder.Append(" ");

            builder.Append(key.Substring(i, 1));
        }

        return builder.ToString();
    }
}
