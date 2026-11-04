namespace Content.Client.CMU14.Radio.ANPRC;

/// <summary>
///     The laminated card taped inside the lid of a real set. The panel is modelled on hardware
///     most people have never touched and is worked from the keypad, so the legends have to be
///     readable without leaving the radio. The glass cannot wrap, so every card line is its own
///     string: a translation re-breaks the text into lines that fit.
/// </summary>
public sealed class ANPRCHelpScreen : ANPRCScreen
{
    // how many numbered lines each paragraph of the card has in the locale file
    private const int KeyingLines = 8;
    private const int SwitchLines = 3;
    private const int ProcedureLines = 6;
    private const int TechniqueLines = 8;

    public override string Title => Loc.GetString("anprc-fp-help-title");

    public override string Status(ANPRCPanelContext context)
        => Loc.GetString(!context.Powered ? "anprc-fp-help-off" : context.Deployed ? "anprc-fp-help-online" : "anprc-fp-stowed");

    private static void Pair(List<ANPRCScreenRow> rows, string key)
        => rows.Add(ANPRCScreenRow.Info(Loc.GetString(key), Loc.GetString(key + "-what")));

    private static void Lines(List<ANPRCScreenRow> rows, string key, int count)
    {
        for (var i = 1; i <= count; i++)
        {
            rows.Add(ANPRCScreenRow.Note(Loc.GetString($"{key}-{i}")));
        }
    }

    public override void Build(ANPRCPanelContext context, List<ANPRCScreenRow> rows)
    {
        rows.Add(ANPRCScreenRow.Heading(Loc.GetString("anprc-fp-help-screen")));
        Pair(rows, "anprc-fp-help-pre");
        Pair(rows, "anprc-fp-help-ent");
        Pair(rows, "anprc-fp-help-clr");
        Pair(rows, "anprc-fp-help-pg");
        rows.Add(ANPRCScreenRow.Note(Loc.GetString("anprc-fp-help-soft-keys")));

        rows.Add(ANPRCScreenRow.Heading(Loc.GetString("anprc-fp-help-keypad")));
        Pair(rows, "anprc-fp-help-key-1");
        Pair(rows, "anprc-fp-help-key-2");
        Pair(rows, "anprc-fp-help-key-3");
        Pair(rows, "anprc-fp-help-key-4");
        Pair(rows, "anprc-fp-help-key-5");
        Pair(rows, "anprc-fp-help-key-6");
        Pair(rows, "anprc-fp-help-key-pages");

        rows.Add(ANPRCScreenRow.Heading(Loc.GetString("anprc-fp-help-keying")));
        Lines(rows, "anprc-fp-help-keying", KeyingLines);

        rows.Add(ANPRCScreenRow.Heading(Loc.GetString("anprc-fp-help-switch")));
        Pair(rows, "anprc-fp-help-switch-off");
        Pair(rows, "anprc-fp-help-switch-ct");
        Pair(rows, "anprc-fp-help-switch-pt");
        Pair(rows, "anprc-fp-help-switch-ld");
        Pair(rows, "anprc-fp-help-switch-z");
        Lines(rows, "anprc-fp-help-switch", SwitchLines);

        rows.Add(ANPRCScreenRow.Heading(Loc.GetString("anprc-fp-help-techniques")));
        Lines(rows, "anprc-fp-help-techniques", TechniqueLines);

        rows.Add(ANPRCScreenRow.Heading(Loc.GetString("anprc-fp-help-procedure")));
        Lines(rows, "anprc-fp-help-procedure", ProcedureLines);
    }
}
