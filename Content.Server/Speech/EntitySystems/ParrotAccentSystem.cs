using System.Linq;
using System.Text.RegularExpressions;
using Content.Server.Speech.Components;
using Robust.Shared.Random;

namespace Content.Server.Speech.EntitySystems;

public sealed partial class ParrotAccentSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ParrotAccentComponent, AccentGetEvent>(OnAccentGet);
    }

    private void OnAccentGet(Entity<ParrotAccentComponent> entity, ref AccentGetEvent args)
    {
        args.Message = Accentuate(entity, args.Message);
    }

    public string Accentuate(Entity<ParrotAccentComponent> entity, string message)
    {
        // Sometimes repeat the longest word at the end of the message, after a squawk! SQUAWK! Sometimes!
        if (_random.Prob(entity.Comp.LongestWordRepeatChance))
        {
            // Don't count non-alphanumeric characters as parts of words
            var cleaned = WordCleanupRegex.Replace(message, string.Empty);
            // Split on whitespace and favor words towards the end of the message
            var words = cleaned.Split(null).Reverse();
            // Find longest word
            var longest = words.MaxBy(word => word.Length);
            if (longest?.Length >= entity.Comp.LongestWordMinLength)
            {
                message = EnsurePunctuation(message);

                // Capitalize the first letter of the repeated word
                longest = string.Create(longest.Length, longest, static (span, value) =>
                {
                    span[0] = char.ToUpperInvariant(value[0]);
                    value.AsSpan(1).CopyTo(span[1..]);
                });

                message = $"{message} {GetRandomSquawk(entity)} {longest}!";
                return message; // No more changes, or it's too much
            }
        }

        if (_random.Prob(entity.Comp.SquawkPrefixChance))
        {
            // AWWK! Sometimes add a squawk at the begining of the message
            message = $"{GetRandomSquawk(entity)} {message}";
        }
        else
        {
            // Otherwise add a squawk at the end of the message! RAWWK!
            message = EnsurePunctuation(message);
            message = $"{message} {GetRandomSquawk(entity)}";
        }

        return message;
    }

    /// <summary>
    /// Adds a "!" to the end of the string, if there isn't already a sentence-ending punctuation mark.
    /// </summary>
    private string EnsurePunctuation(string message)
    {
        if (!message.EndsWith('!') && !message.EndsWith('?') && !message.EndsWith('.'))
            return message + '!';
        return message;
    }

    /// <summary>
    /// Returns a random, localized squawk sound.
    /// </summary>
    private string GetRandomSquawk(Entity<ParrotAccentComponent> entity)
    {
        return Loc.GetString(_random.Pick(entity.Comp.Squawks));
    }

    private static readonly Regex WordCleanupRegex = new("[^A-Za-z0-9 -]");
}
