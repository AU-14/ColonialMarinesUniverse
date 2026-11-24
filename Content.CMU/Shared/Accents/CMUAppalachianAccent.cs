/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
using System.Text.RegularExpressions;
using Content.Shared.Speech.Components;
using Content.Shared.Speech.EntitySystems;
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Speech;

/// <summary>
/// CMU Appalachian English accent. Dropped g's (goin', somethin'), a-prefixing after "is/was/are" ("he was
/// a-runnin'"), ain't, reckon, yonder, y'all and young'uns. See <see cref="CMUAppalachianAccentRules"/> for the
/// exact rules.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(CMUAppalachianAccentSystem))]
public sealed partial class CMUAppalachianAccentComponent : BaseAccentComponent;

public sealed partial class CMUAppalachianAccentSystem : RelayAccentSystem<CMUAppalachianAccentComponent>
{
    public override string Accentuate(string message, Entity<CMUAppalachianAccentComponent>? ent = null)
        => CMUAppalachianAccentRules.Apply(message);
}

/// <summary>
/// The Appalachian accent rules as plain string code with no game dependencies. Running them twice on the same
/// message changes nothing.
/// </summary>
public static class CMUAppalachianAccentRules
{
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    // Whole words only. Add more pairs here: ("english word", "appalachian word").
    private static readonly (Regex Pattern, MatchEvaluator Replace)[] Words = CMUAccentText.BuildWords(
        // phrases first
        ("what the hell", "what in tarnation"), ("what the fuck", "what in tarnation"),
        ("holy shit", "land sakes"), ("oh my god", "land sakes"), ("bug hunt", "varmint hunt"),
        ("about to", "fixin' to"), ("gonna", "fixin' to"), ("over there", "yonder"),
        ("you all", "y'all"), ("you guys", "y'all"),
        ("is not", "ain't"), ("are not", "ain't"), ("am not", "ain't"),
        ("isn't", "ain't"), ("aren't", "ain't"),
        // single words
        ("think", "reckon"), ("those", "them"), ("very", "right"), ("quite", "right"),
        ("completely", "plumb"), ("tired", "tuckered"), ("pretty", "purty"),
        ("child", "young'un"), ("children", "young'uns"), ("kids", "young'uns"),
        ("grandmother", "mamaw"), ("grandma", "mamaw"), ("grandfather", "papaw"), ("grandpa", "papaw"),
        ("valley", "holler"), ("bag", "poke"), ("bags", "pokes"), ("dinner", "supper"),
        ("wash", "warsh"), ("washed", "warshed"), ("potato", "tater"), ("tomato", "tomater"),
        ("window", "winder"), ("yellow", "yeller"), ("tomorrow", "tomorrer"), ("follow", "foller"),
        ("borrow", "borrer"), ("pillow", "piller"),
        // more words
        ("carry", "tote"), ("gun", "hog rifle"), ("heard", "heared"), ("saw", "seed"),
        ("bug", "crawdad"), ("water", "warter"),
        // family and folks
        ("mother", "mama"), ("mom", "mama"), ("father", "daddy"), ("dad", "daddy"), ("people", "folks"),
        // places and things
        ("creek", "crick"), ("hollow", "holler"), ("sandwich", "sammich"), ("alcohol", "shine"),
        // how folks are doing
        ("sick", "poorly"), ("angry", "riled"), ("crazy", "tetched"),
        // other words and phrases
        ("a lot of", "a heap of"), ("almost", "nigh on"), ("sure", "shore"), ("doesn't", "don't"),
        // greetings and manners
        ("hello", "howdy"), ("thank you", "much obliged"), ("thanks", "much obliged"),
        // people
        ("girl", "gal"), ("girls", "gals"), ("idiot", "fool"), ("ghost", "haint"), ("ghosts", "haints"),
        // feelings and doings
        ("scared", "skeered"), ("afraid", "skeered"), ("worry", "fret"), ("worried", "fretted"),
        ("argue", "fuss"), ("argued", "fussed"), ("arguing", "fussin'"), ("sit", "set"),
        // words and things
        ("food", "vittles"), ("couch", "davenport"), ("sofa", "davenport"), ("soda", "pop"),
        ("pants", "britches"), ("pig", "hog"), ("pigs", "hogs"), ("beautiful", "purty"),
        ("probably", "likely"), ("maybe", "mebbe"), ("a little", "a mite"),
        // colony and military life
        ("monster", "varmint"), ("monsters", "varmints"), ("colonist", "settler"), ("colonists", "settlers"),
        ("shotgun", "scattergun"), ("whiskey", "shine"), ("liquor", "shine"), ("doctor", "doc"),
        ("supervisor", "bossman"), ("foreman", "bossman"), ("prisoner", "jailbird"), ("scary", "skeery"),
        ("repair", "fix"), ("ambush", "bushwhack"), ("ambushed", "bushwhacked"),
        ("retreat", "light out"), ("run away", "light out"), ("take cover", "hunker down"), ("hide", "hole up"),
        // swearing
        ("shit", "shoot"), ("hell", "heck"), ("damn", "dadburn"), ("goddamn", "dadburned"), ("fucking", "dadburned"),
        ("fuck", "dadgum"), ("bullshit", "hogwash"), ("bastard", "scoundrel"), ("asshole", "scoundrel"));

    // "was running" -> "was a-running": an -ing verb after is/was/are/'s/'re/'m gets an a- in front.
    // The lookahead skips -ing nouns like "is nothing" and "was morning".
    private static readonly Regex APrefix = new(
        @"((?:\b(?:was|were|is|are|am|be|been)|'(?:s|re|m))\s+)(?!\w*(?:thing|morning|evening|ceiling|building|during)\b)(\w*[aeiouy]\w*)(ing)\b",
        Options);

    public static string Apply(string message)
    {
        var msg = CMUAccentText.ApplyWords(Words, message);

        // must run before DropG, which would otherwise turn "running" into "runnin'" first
        msg = APrefix.Replace(msg, m => m.Groups[3].Value == "ING"
            ? m.Groups[1].Value + "A-" + m.Groups[2].Value + "IN'"
            : m.Groups[1].Value + "a-" + m.Groups[2].Value + "in'");

        return CMUAccentText.DropG(msg);
    }
}
