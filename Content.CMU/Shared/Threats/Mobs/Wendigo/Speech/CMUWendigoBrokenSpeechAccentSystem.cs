using System.Text;
using Content.Shared.Random.Helpers;
using Content.Shared.Speech.EntitySystems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Threats.Mobs.Wendigo.Speech;

/// <summary>
/// Breaks a Wendigo's words up per word: stretched vowels, doubled first or last letters,
/// trailing "..", stray capitals and the odd word lost to "...". End punctuation is kept.
/// </summary>
public sealed partial class CMUWendigoBrokenSpeechAccentSystem : RelayAccentSystem<CMUWendigoBrokenSpeechAccentComponent>
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;

    private const string Vowels = "aeiouAEIOU";
    private const string Dropped = "...";
    private const string Trail = "..";

    public override string Accentuate(string message, Entity<CMUWendigoBrokenSpeechAccentComponent>? ent = null)
    {
        if (string.IsNullOrWhiteSpace(message))
            return message;

        var random = ent.HasValue
            ? SharedRandomExtensions.PredictedRandom(_timing, GetNetEntity(ent.Value))
            : _random;

        var comp = ent?.Comp;
        var dropChance = comp?.DropChance ?? CMUWendigoBrokenSpeechAccentComponent.DefaultDropChance;
        var stretchChance = comp?.StretchChance ?? CMUWendigoBrokenSpeechAccentComponent.DefaultStretchChance;
        var doubleChance = comp?.DoubleChance ?? CMUWendigoBrokenSpeechAccentComponent.DefaultDoubleChance;
        var trailChance = comp?.TrailChance ?? CMUWendigoBrokenSpeechAccentComponent.DefaultTrailChance;
        var capitalizeChance = comp?.CapitalizeChance ?? CMUWendigoBrokenSpeechAccentComponent.DefaultCapitalizeChance;

        var words = message.Split(' ');
        var builder = new StringBuilder(message.Length * 2);
        var previousDropped = false;

        for (var i = 0; i < words.Length; i++)
        {
            if (i > 0)
                builder.Append(' ');

            var word = words[i];
            var start = 0;
            while (start < word.Length && !char.IsLetterOrDigit(word[start]))
            {
                start++;
            }

            var end = word.Length;
            while (end > start && !char.IsLetterOrDigit(word[end - 1]))
            {
                end--;
            }

            if (start >= end)
            {
                // Punctuation or whitespace only: keep it as is.
                builder.Append(word);
                continue;
            }

            var lead = word[..start];
            var core = word[start..end];
            var trail = word[end..];

            // A lone word is never lost, and never two in a row, so the line stays readable.
            if (words.Length > 1 && !previousDropped && random.Prob(dropChance))
            {
                previousDropped = true;
                builder.Append(Dropped);
                // Keep end punctuation such as "!" or "?"; dots and commas fold into the "...".
                builder.Append(trail.TrimStart('.', ','));
                continue;
            }

            previousDropped = false;
            builder.Append(lead);
            builder.Append(Break(core, random, stretchChance, doubleChance, capitalizeChance));

            if (trail.Length == 0 && random.Prob(trailChance))
                builder.Append(Trail);
            else
                builder.Append(trail);
        }

        return builder.ToString();
    }

    private static string Break(string core,
        IRobustRandom random,
        float stretchChance,
        float doubleChance,
        float capitalizeChance)
    {
        var shouting = IsAllUpper(core);
        var result = new StringBuilder(core, core.Length + 6);

        if (random.Prob(stretchChance))
        {
            var vowels = 0;
            foreach (var c in core)
            {
                if (Vowels.Contains(c))
                    vowels++;
            }

            if (vowels > 0)
            {
                var pick = random.Next(vowels);
                for (var i = 0; i < core.Length; i++)
                {
                    if (!Vowels.Contains(core[i]) || pick-- > 0)
                        continue;

                    // Two or three times the vowel: "upper" -> "uuupper".
                    var vowel = core[i];
                    var fill = shouting ? vowel : char.ToLowerInvariant(vowel);
                    result.Insert(i + 1, new string(fill, random.Next(1, 3)));
                    break;
                }
            }
        }

        if (random.Prob(capitalizeChance) && char.IsLetter(result[0]))
            result[0] = char.ToUpperInvariant(result[0]);

        if (random.Prob(doubleChance))
        {
            if (random.Prob(0.5f))
            {
                if (char.IsLetter(result[0]))
                    result.Insert(0, result[0]); // "In" -> "IIn"
            }
            else
            {
                var last = result[result.Length - 1];
                if (char.IsLetter(last))
                    result.Append(last, random.Next(1, 3)); // "upper" -> "upperrr"
            }
        }

        return result.ToString();
    }

    private static bool IsAllUpper(string core)
    {
        var letters = 0;
        foreach (var c in core)
        {
            if (!char.IsLetter(c))
                continue;

            if (!char.IsUpper(c))
                return false;

            letters++;
        }

        return letters > 1;
    }
}
