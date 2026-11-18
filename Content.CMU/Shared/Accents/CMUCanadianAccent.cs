/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
using System.Text.RegularExpressions;
using Content.Shared.Speech.Components;
using Content.Shared.Speech.EntitySystems;
using Robust.Shared.GameStates;
using Robust.Shared.Random;

namespace Content.Shared.CMU14.Speech;

/// <summary>
/// CMU Canadian accent. Swaps in Canadian words (aboot, toque, washroom, pop...) and sometimes ends a message
/// with ", eh". See <see cref="CMUCanadianAccentRules"/> for the exact rules.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(CMUCanadianAccentSystem))]
public sealed partial class CMUCanadianAccentComponent : BaseAccentComponent
{
    /// <summary>Chance (0 to 1) that a message gets ", eh" added to the end.</summary>
    [DataField]
    public float EhChance = 0.25f;
}

public sealed partial class CMUCanadianAccentSystem : RelayAccentSystem<CMUCanadianAccentComponent>
{
    [Dependency] private IRobustRandom _random = default!;

    public override string Accentuate(string message, Entity<CMUCanadianAccentComponent>? ent = null)
    {
        var msg = CMUCanadianAccentRules.Apply(message);

        var chance = ent?.Comp.EhChance ?? 0.25f;
        if (_random.Prob(chance))
            msg = CMUCanadianAccentRules.AddEh(msg);

        return msg;
    }
}

/// <summary>
/// The Canadian accent rules as plain string code with no game dependencies. Running them twice on the same
/// message changes nothing: the replacement words never match their own source words, and AddEh does nothing
/// if the message already ends in "eh".
/// </summary>
public static class CMUCanadianAccentRules
{
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    // Whole words only. Add more pairs here: ("english word", "canadian word").
    private static readonly (Regex Pattern, MatchEvaluator Replace)[] Words = CMUAccentText.BuildWords(
        // pronunciation
        ("about", "aboot"), ("out", "oot"), ("house", "hoose"),
        ("outside", "ootside"), ("without", "withoot"),
        // things
        ("bathroom", "washroom"), ("restroom", "washroom"),
        ("soda", "pop"), ("couch", "chesterfield"), ("sofa", "chesterfield"),
        ("hat", "toque"), ("beanie", "toque"), ("sneakers", "runners"),
        ("two dollars", "toonie"), ("dollar", "loonie"), ("dollars", "loonies"),
        ("napkin", "serviette"), ("trash", "garbage"), ("coffee", "double-double"),
        ("convenience store", "dep"), ("case of beer", "two-four"),
        // people
        ("friend", "buddy"), ("friends", "buddies"), ("pal", "bud"), ("idiot", "hoser"),
        ("cop", "mountie"), ("cops", "mounties"),
        // phrases
        ("awesome", "beauty"), ("fantastic", "beauty"), ("cool", "sweet"), ("definitely", "for sure"), ("totally", "for sure"), ("yes", "yup"),
        // more words
        ("very", "real"), ("excellent", "beauty"), ("stupid", "goofy"), ("angry", "ticked off"),
        ("exhausted", "wiped"), ("coat", "parka"), ("sweatshirt", "hoodie"), ("sweatpants", "track pants"),
        ("swimsuit", "bathing suit"), ("pharmacy", "drugstore"), ("gravy fries", "poutine"),
        ("candy bar", "chocolate bar"), ("beer", "brewski"), ("beers", "brewskis"),
        // phrases
        ("excuse me", "sorry to bother you"), ("thank you", "thanks a bunch"), ("you're welcome", "no problem"),
        ("goodbye", "take 'er easy"), ("bye", "take 'er easy"),
        ("do your best", "give 'er"), ("give it your all", "give 'er"),
        ("absolutely", "for sure"), ("of course", "for sure"), ("certainly", "for sure"),
        // people and feelings
        ("bro", "bud"), ("dude", "bud"), ("mad", "ticked off"), ("annoyed", "ticked off"),
        ("tired", "beat"), ("drunk", "hammered"), ("amazing", "beauty"), ("freezing", "brutal"),
        // things
        ("autumn", "fall"), ("cabin", "cottage"), ("police car", "cruiser"),
        ("toilet", "washroom"), ("latrine", "washroom"),
        ("kilometer", "klick"), ("kilometers", "klicks"), ("kilometre", "klick"), ("kilometres", "klicks"),
        // more phrases. Longer phrases first so they win over the single swear words below.
        ("how are you", "how's it goin'"), ("what's up", "what's goin' on"), ("sure thing", "for sure"),
        ("take it easy", "take 'er easy"), ("calm down", "take 'er easy"),
        ("i guess", "I s'pose"), ("i suppose", "I s'pose"),
        ("what the fuck", "what the frig"), ("holy shit", "holy crap"), ("what the hell", "what the heck"),
        // verbs
        ("tease", "chirp"), ("mock", "chirp"), ("insult", "chirp"), ("quit", "bail"),
        ("throw", "chuck"), ("threw", "chucked"), ("fight", "scrap"),
        // common words
        ("mess hall", "caf"), ("mess", "gong show"), ("chaos", "gong show"), ("disaster", "gong show"), ("fiasco", "gong show"),
        ("eager", "keener"), ("dumb", "goofy"), ("jerk", "hoser"), ("loser", "knob"),
        // swearing
        ("fuck", "frig"), ("fucking", "frigging"), ("shit", "crap"), ("bullshit", "bull"),
        ("damn", "darn"), ("goddamn", "darn"), ("hell", "heck"), ("asshole", "knob"), ("bastard", "hoser"),
        // colony and military life
        ("sergeant", "sarge"), ("recruit", "newbie"), ("rookie", "newbie"), ("doctor", "doc"),
        ("cafeteria", "caf"), ("whiskey", "rye"), ("rations", "grub"),
        ("retreat", "bail"), ("run away", "bail"), ("panic", "lose it"), ("panicking", "losin' it"));

    private static readonly Regex EndsWithEh = new(@"\beh\W*$", Options);

    public static string Apply(string message)
        => CMUAccentText.ApplyWords(Words, message);

    /// <summary>"That's great." becomes "That's great, eh." and "Sure" becomes "Sure, eh".</summary>
    public static string AddEh(string message)
        => CMUAccentText.AppendTag(message, "eh", EndsWithEh);
}
