/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
using System.Text.RegularExpressions;
using Content.Shared.Speech.Components;
using Content.Shared.Speech.EntitySystems;
using Robust.Shared.GameStates;
using Robust.Shared.Random;

namespace Content.Shared.CMU14.Speech;

/// <summary>
/// CMU Australian accent. Swaps in Aussie slang (g'day, mate, arvo, brekkie...) and sometimes ends a message
/// with ", mate". See <see cref="CMUAustralianAccentRules"/> for the exact rules.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(CMUAustralianAccentSystem))]
public sealed partial class CMUAustralianAccentComponent : BaseAccentComponent
{
    /// <summary>Chance (0 to 1) that a message gets ", mate" added to the end.</summary>
    [DataField]
    public float MateChance = 0.2f;
}

public sealed partial class CMUAustralianAccentSystem : RelayAccentSystem<CMUAustralianAccentComponent>
{
    [Dependency] private IRobustRandom _random = default!;

    public override string Accentuate(string message, Entity<CMUAustralianAccentComponent>? ent = null)
    {
        var msg = CMUAustralianAccentRules.Apply(message);

        var chance = ent?.Comp.MateChance ?? 0.2f;
        if (_random.Prob(chance))
            msg = CMUAustralianAccentRules.AddMate(msg);

        return msg;
    }
}

/// <summary>
/// The Australian accent rules as plain string code with no game dependencies. Running them twice on the same
/// message changes nothing: the replacement words never match their own source words, and AddMate does nothing
/// if the message already ends in "mate".
/// </summary>
public static class CMUAustralianAccentRules
{
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    // Whole words only. Add more pairs here: ("english word", "aussie word").
    private static readonly (Regex Pattern, MatchEvaluator Replace)[] Words = CMUAccentText.BuildWords(
        // greetings and people
        ("hello", "g'day"), ("hi", "g'day"),
        ("friend", "mate"), ("friends", "mates"), ("buddy", "mate"), ("pal", "mate"),
        ("idiot", "drongo"), ("fool", "galah"), ("pussy", "wuss"), ("cop", "copper"), ("cops", "coppers"),
        ("goodbye", "hooroo"), ("bye", "hooroo"), ("how are you", "how ya goin'"),
        // phrases
        ("no problem", "no worries"), ("thanks", "ta"),
        ("very", "bloody"), ("really", "bloody"),
        ("beautiful", "beaut"), ("excellent", "ripper"), ("definitely", "defo"),
        ("broken", "munted"), ("exhausted", "knackered"), ("crazy", "mad"), ("angry", "shirty"),
        ("drunk", "blotto"), ("great", "ripper"), ("awesome", "ripper"),
        ("honestly", "fair dinkum"), ("seriously", "fair dinkum"),
        // shortened words
        ("afternoon", "arvo"), ("breakfast", "brekkie"), ("barbecue", "barbie"),
        ("sunglasses", "sunnies"), ("mosquito", "mozzie"), ("mosquitoes", "mozzies"),
        ("vegetables", "veggies"), ("sandwich", "sanga"), ("football", "footy"),
        // things
        ("pickup truck", "ute"), ("toilet", "dunny"), ("truck", "ute"), ("hat", "akubra"), ("kangaroo", "roo"),
        ("swimsuit", "bathers"), ("sweater", "jumper"), ("pants", "daks"),
        ("underwear", "undies"), ("sidewalk", "footpath"), ("elevator", "lift"), ("flashlight", "torch"),
        ("vacation", "holiday"), ("pharmacy", "chemist"), ("gas station", "servo"),
        ("gasoline", "petrol"), ("ambulance", "ambo"),
        // food and drink
        ("chocolate", "choccy"), ("cookie", "bikkie"), ("sausage", "snag"),
        ("sausages", "snags"), ("beer", "coldie"), ("beers", "coldies"), ("soda", "soft drink"),
        ("cigarette", "ciggie"), ("food", "tucker"), ("cooler", "esky"),
        // more people and talk
        ("extremely busy", "flat out like a lizard drinking"), ("hard work", "hard yakka"),
        ("guy", "bloke"), ("guys", "blokes"), ("man", "bloke"), ("men", "blokes"),
        ("kid", "nipper"), ("kids", "nippers"),
        ("sick", "crook"), ("tired", "knackered"), ("annoyed", "shirty"),
        ("fantastic", "bonza"), ("wonderful", "bonza"), ("freezing", "brass monkeys"),
        ("a lot of", "heaps of"), ("lots of", "heaps of"),
        // more things
        ("sneakers", "runners"), ("trunk", "boot"), ("apartment", "flat"),
        ("cell phone", "mobile"), ("cellphone", "mobile"),
        ("trash can", "bin"), ("garbage can", "bin"), ("trash", "rubbish"), ("garbage", "rubbish"),
        ("crocodile", "croc"),
        // colony and military life.
        ("sergeant", "sarge"), ("recruit", "new chum"), ("rookie", "new chum"), ("newbie", "new chum"),
        ("colonist", "settler"), ("colonists", "settlers"), ("doctor", "doc"),
        ("monster", "beast"), ("monsters", "beasts"), ("dangerous", "dicey"), ("suspicious", "dodgy"),
        ("fight", "stoush"), ("argument", "blue"), ("try", "have a go"), ("attempt", "have a go"),
        ("retreat", "do a runner"), ("run away", "do a runner"),
        // swearing
        ("holy shit", "strewth"), ("oh my god", "strewth"), ("damn", "bloody"), ("goddamn", "bloody"),
        ("fucking", "bloody"), ("bullshit", "bulldust"), ("bastard", "bugger"), ("asshole", "arsehole"),
        ("ass", "arse"), ("pissed off", "filthy"));

    private static readonly Regex EndsWithMate = new(@"\bmates?\W*$", Options);

    public static string Apply(string message)
        => CMUAccentText.ApplyWords(Words, message);

    /// <summary>"That's great." becomes "That's great, mate." and "Sure" becomes "Sure, mate".</summary>
    public static string AddMate(string message)
        => CMUAccentText.AppendTag(message, "mate", EndsWithMate);
}
