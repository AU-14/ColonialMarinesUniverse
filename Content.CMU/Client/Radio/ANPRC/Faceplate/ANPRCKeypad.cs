using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.CMU14.Radio.ANPRC;

/// <summary>
///     Every key the set's pad carries. The digits do double duty exactly as they do on the real
///     radio: they type into whatever the screen is prompting for - numbers against the frequency
///     template, or letters off the group engraved above each key - and when the screen is not
///     asking for anything they do the function lettered above them.
///
///     The preset rocker walks the cursor down the screen and ENT works the line under it, which
///     is how a set with no arrow keys is driven.
/// </summary>
public enum ANPRCKey : byte
{
    Digit0,
    Digit1,
    Digit2,
    Digit3,
    Digit4,
    Digit5,
    Digit6,
    Digit7,
    Digit8,
    Digit9,
    Clear,
    Enter,
    PageUp,
    PageDown,
    PresetUp,
    PresetDown,
}

/// <summary>
///     The moulded keypad off the front of the set: page rocker down the left, preset rocker
///     down the right, CLR and ENT on the bottom rank, the numbers between them. It reports
///     which key was struck and nothing else - what a key means is the panel's business.
/// </summary>
public sealed class ANPRCKeypad : Control
{
    private const float KeyWidth = 56f;
    private const float KeyHeight = 30f;

    public event Action<ANPRCKey>? OnKey;

    private readonly Dictionary<ANPRCKey, ANPRCKeyButton> _keys = new();

    /// <summary>What each cap's small legend says when the pad is not typing anything.</summary>
    private readonly Dictionary<ANPRCKey, string> _legends = new();

    public IReadOnlyDictionary<ANPRCKey, ANPRCKeyButton> Keys => _keys;

    public ANPRCKeypad()
    {
        var grid = new GridContainer
        {
            Columns = 5,
            HSeparationOverride = 4,
            VSeparationOverride = 4,
            HorizontalAlignment = HAlignment.Center,
        };

        AddChild(grid);

        // the legends are the real set's: the letter group each digit types, and the function
        // it runs when nothing is being typed
        grid.AddChild(Key(ANPRCKey.PageUp, "+ PG", Loc.GetString("anprc-fp-key-fn-scrn")));
        grid.AddChild(Key(ANPRCKey.Digit1, "1", "ABC " + Loc.GetString("anprc-fp-key-fn-call")));
        grid.AddChild(Key(ANPRCKey.Digit2, "2", "DEF " + Loc.GetString("anprc-fp-key-fn-lt")));
        grid.AddChild(Key(ANPRCKey.Digit3, "3", "GHI " + Loc.GetString("anprc-fp-key-fn-mode")));
        grid.AddChild(Key(ANPRCKey.PresetUp, "+ PRE", Loc.GetString("anprc-fp-key-fn-curs")));

        grid.AddChild(Key(ANPRCKey.PageDown, "- PG", Loc.GetString("anprc-fp-key-fn-scrn")));
        grid.AddChild(Key(ANPRCKey.Digit4, "4", "JKL " + Loc.GetString("anprc-fp-key-fn-sql")));
        grid.AddChild(Key(ANPRCKey.Digit5, "5", "MNO " + Loc.GetString("anprc-fp-key-fn-zero")));
        grid.AddChild(Key(ANPRCKey.Digit6, "6", "PQR " + Loc.GetString("anprc-fp-key-fn-pwr")));
        grid.AddChild(Key(ANPRCKey.PresetDown, "- PRE", Loc.GetString("anprc-fp-key-fn-curs")));

        grid.AddChild(Key(ANPRCKey.Clear, Loc.GetString("anprc-fp-key-clr"), Loc.GetString("anprc-fp-key-fn-back")));
        grid.AddChild(Key(ANPRCKey.Digit7, "7", "STU " + Loc.GetString("anprc-fp-key-fn-opt")));
        grid.AddChild(Key(ANPRCKey.Digit8, "8", "VWX " + Loc.GetString("anprc-fp-key-fn-pgm")));
        grid.AddChild(Key(ANPRCKey.Digit9, "9", "YZ? " + Loc.GetString("anprc-fp-key-fn-sec")));
        grid.AddChild(Key(ANPRCKey.Enter, Loc.GetString("anprc-fp-key-ent"), Loc.GetString("anprc-fp-key-fn-work")));

        grid.AddChild(new Control { MinWidth = KeyWidth });
        grid.AddChild(new Control { MinWidth = KeyWidth });
        grid.AddChild(Key(ANPRCKey.Digit0, "0", "    " + Loc.GetString("anprc-fp-key-fn-log")));
        grid.AddChild(new Control { MinWidth = KeyWidth });
        grid.AddChild(new Control { MinWidth = KeyWidth });
    }

    private ANPRCKeyButton Key(ANPRCKey key, string primary, string secondary)
    {
        var button = new ANPRCKeyButton(primary, secondary, minWidth: KeyWidth, minHeight: KeyHeight);
        button.OnPressed += _ => OnKey?.Invoke(key);

        _keys[key] = button;
        _legends[key] = secondary;

        return button;
    }

    /// <summary>
    ///     The letters each number key writes, and the digit it ends on. Pressing a key steps
    ///     through this in order, so 1 gives A, B, C, then 1 itself.
    ///
    ///     This is the pad's own lettering, so it lives here rather than in the panel - the caps
    ///     and the keying have to agree about what each key writes, and there is no way for them
    ///     to drift if they read the same table.
    /// </summary>
    public static string LetterCycle(ANPRCKey key) => key switch
    {
        ANPRCKey.Digit1 => "ABC1",
        ANPRCKey.Digit2 => "DEF2",
        ANPRCKey.Digit3 => "GHI3",
        ANPRCKey.Digit4 => "JKL4",
        ANPRCKey.Digit5 => "MNO5",
        ANPRCKey.Digit6 => "PQR6",
        ANPRCKey.Digit7 => "STU7",
        ANPRCKey.Digit8 => "VWX8",
        ANPRCKey.Digit9 => "YZ9",
        // a station callsign wants a space and a dash - HAVOC 6, LIMA-6 - and neither is
        // printed on any other key
        ANPRCKey.Digit0 => " -0",
        _ => string.Empty,
    };

    /// <summary>What that key's group is printed as on the cap while letters are being keyed.</summary>
    public static string LetterLegend(ANPRCKey key) => key switch
    {
        ANPRCKey.Digit0 => "SP -",
        _ => LetterCycle(key) is { Length: > 1 } cycle ? cycle[..^1] : string.Empty,
    };

    /// <summary>
    ///     Relabel the pad for whatever it is typing into.
    ///
    ///     Keying a frequency greys the lettered functions out, because only the numbers matter
    ///     and the pad should look like it is typing rather than running shortcuts. Keying text
    ///     does the opposite: the caps show the letter group each key writes and light it, since
    ///     an operator cannot key a label off keys that only say 1 to 9.
    /// </summary>
    public void SetEntryMode(ANPRCEntryMode? mode)
    {
        foreach (var (key, button) in _keys)
        {
            if (!IsDigit(key))
                continue;

            switch (mode)
            {
                case ANPRCEntryMode.Frequency:
                    button.SetSecondaryVisible(false);
                    button.SecondaryLit = false;

                    break;

                case ANPRCEntryMode.Letters:
                    button.SecondaryText = LetterLegend(key);
                    button.SetSecondaryVisible(true);
                    button.SecondaryLit = true;

                    break;

                default:
                    button.SecondaryText = _legends[key];
                    button.SetSecondaryVisible(true);
                    button.SecondaryLit = false;

                    break;
            }
        }
    }

    public void SetDisabled(bool disabled)
    {
        foreach (var (key, button) in _keys)
        {
            // the power key has to stay live on a dead set, it is what brings it back
            button.Disabled = disabled && key != ANPRCKey.Digit6;
        }
    }

    public static bool IsDigit(ANPRCKey key) => key <= ANPRCKey.Digit9;

    public static int DigitOf(ANPRCKey key) => (int) key;
}
