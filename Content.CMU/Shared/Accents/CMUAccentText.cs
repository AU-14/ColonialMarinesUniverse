/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
using System.Linq;
using System.Text.RegularExpressions;

namespace Content.Shared.CMU14.Speech;

/// <summary>Small string helpers shared by the CMU accents.</summary>
public static class CMUAccentText
{
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    // -ing words that have a vowel before the "ing" (so "thing", "king" and "bring" are left alone)
    private static readonly Regex IngWord = new(@"\b(\w*[aeiouy]\w*)(ing)\b", Options);

    // the end of a message: any trailing . ! ? plus whitespace (the match can be empty)
    private static readonly Regex MessageEnd = new(@"([.!?]*)\s*$", RegexOptions.CultureInvariant);

    /// <summary>Builds whole-word, case-insensitive replacements from (word, replacement) pairs.</summary>
    public static (Regex Pattern, MatchEvaluator Replace)[] BuildWords(params (string Word, string Replacement)[] words)
    {
        var result = new (Regex, MatchEvaluator)[words.Length];
        for (var i = 0; i < words.Length; i++)
        {
            var pattern = new Regex($@"\b{Regex.Escape(words[i].Word)}\b", Options);
            var replacement = words[i].Replacement;
            result[i] = (pattern, match => MatchCase(match.Value, replacement));
        }

        return result;
    }

    /// <summary>Applies the replacements in order, keeping the capitalization of each replaced word.</summary>
    public static string ApplyWords((Regex Pattern, MatchEvaluator Replace)[] words, string message)
    {
        var msg = message;
        foreach (var (pattern, replace) in words)
            msg = pattern.Replace(msg, replace);

        return msg;
    }

    /// <summary>
    /// "running" becomes "runnin'", "something" becomes "somethin'". "thing" and "king" are untouched.
    /// The ending copies the capitalization of the word: "GETTING" becomes "GETTIN'", not "GETTin'".
    /// </summary>
    public static string DropG(string message)
        => IngWord.Replace(message, m => m.Groups[1].Value + (m.Groups[2].Value == "ING" ? "IN'" : "in'"));

    /// <summary>
    /// Adds ", tag" to the end of the message ("Sure." becomes "Sure, tag."), unless it already ends in the tag.
    /// </summary>
    public static string AppendTag(string message, string tag, Regex endsWithTag)
    {
        if (string.IsNullOrWhiteSpace(message))
            return message;

        if (endsWithTag.IsMatch(message))
            return message;

        return MessageEnd.Replace(message, ", " + tag + "$1", 1);
    }

    /// <summary>
    /// Copies the capitalization of the original text onto the replacement: ALL CAPS stays all caps and a
    /// capital first letter stays a capital first letter.
    /// </summary>
    public static string MatchCase(string source, string replacement)
    {
        if (source.Length > 1 && !source.Any(char.IsLower))
            return replacement.ToUpperInvariant();

        if (char.IsUpper(source[0]))
            // Strings only, no char: the compiler turns char + string (even char.ToString() + string) into a
            // ReadOnlySpan<char> constructor call, which the sandbox rejects.
            return replacement.Substring(0, 1).ToUpperInvariant() + replacement.Substring(1);

        return replacement;
    }
}
