/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
using System.Text.RegularExpressions;
using Content.Shared.Speech.Components;
using Content.Shared.Speech.EntitySystems;
using Robust.Shared.GameStates;
using Robust.Shared.Random;

namespace Content.Shared.CMU14.Speech;

/// <summary>
/// CMU Texas accent. A drawl: y'all, howdy, fixin' to, dropped g's (goin', somethin') and sometimes ends a
/// message with ", partner". See <see cref="CMUTexasAccentRules"/> for the exact rules.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(CMUTexasAccentSystem))]
public sealed partial class CMUTexasAccentComponent : BaseAccentComponent
{
    /// <summary>Chance (0 to 1) that a message gets ", partner" added to the end.</summary>
    [DataField]
    public float PartnerChance = 0.15f;
}

public sealed partial class CMUTexasAccentSystem : RelayAccentSystem<CMUTexasAccentComponent>
{
    [Dependency] private IRobustRandom _random = default!;

    public override string Accentuate(string message, Entity<CMUTexasAccentComponent>? ent = null)
    {
        var msg = CMUTexasAccentRules.Apply(message);

        var chance = ent?.Comp.PartnerChance ?? 0.15f;
        if (_random.Prob(chance))
            msg = CMUTexasAccentRules.AddPartner(msg);

        return msg;
    }
}

/// <summary>
/// The Texas accent rules as plain string code with no game dependencies. Running them twice on the same
/// message changes nothing.
/// </summary>
public static class CMUTexasAccentRules
{
    private static readonly Regex EndsWithTag = new(@"\bpartner\W*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static string AddPartner(string message)
        => CMUAccentText.AppendTag(message, "partner", EndsWithTag);

    // Whole words only. Add more pairs here: ("english word", "texan word").
    private static readonly (Regex Pattern, MatchEvaluator Replace)[] Words = CMUAccentText.BuildWords(
        // phrases first. ("going to" is left out on purpose: it is also "going to the store".)
        ("gonna", "fixin' to"), ("about to", "fixin' to"),
        ("what the hell", "what in tarnation"), ("what the fuck", "what in tarnation"),
        ("holy shit", "well I'll be"), ("bug hunt", "varmint hunt"),
        ("oh my god", "well I'll be"), ("oh my gosh", "well I'll be"), ("wow", "well I'll be"),
        ("i think", "i reckon"),
        ("you're welcome", "you bet"), ("you are welcome", "you bet"),
        ("good morning", "mornin'"), ("good evening", "evenin'"),
        ("what are you doing", "what're you up to"), ("let's go", "let's mosey"),
        ("a long time", "a month of Sundays"), ("go away", "git on out of here"),
        ("shut up", "hush up"), ("honestly", "honest to goodness"), ("seriously", "honest to goodness"),
        ("you all", "y'all"), ("you guys", "y'all"),
        ("a lot of", "a whole lotta"), ("thank you", "much obliged"), ("thanks", "much obliged"),
        // single words
        ("hello", "howdy"), ("hi", "howdy"), ("yes", "yessir"),
        ("my friend", "ol' pal"), ("my friends", "ol' pals"), ("friend", "partner"), ("friends", "partners"), ("idiot", "fool"),
        ("very", "mighty"), ("really", "real"), ("soda", "coke"),
        ("hey", "howdy"), ("okay", "alrighty"), ("dinner", "supper"), ("barbecue", "BBQ"), ("truck", "pickup"),
        ("guy", "fella"), ("guys", "fellas"), ("girl", "gal"), ("girls", "gals"),
        ("cop", "lawman"), ("cops", "lawmen"), ("nonsense", "hogwash"),
        ("great", "mighty fine"), ("excellent", "mighty fine"), ("extremely", "mighty"),
        ("completely", "plumb"), ("quickly", "right quick"), ("angry", "fit to be tied"),
        // more phrases
        ("goodbye", "see y'all later"), ("bye", "see y'all later"), ("excuse me", "beg your pardon"),
        ("sorry", "pardon me"), ("hurry up", "get a move on"), ("wait a minute", "hold your horses"),
        ("slow down", "hold your horses"), ("definitely", "sure as shootin'"), ("certainly", "sure as shootin'"),
        // people
        ("man", "fella"), ("men", "fellas"), ("woman", "gal"), ("women", "gals"),
        ("dude", "pardner"), ("buddy", "pardner"), ("coward", "yellow-belly"),
        ("police officer", "lawman"), ("criminal", "outlaw"), ("criminals", "outlaws"),
        // how things are
        ("sick", "under the weather"), ("tired", "worn out"), ("awesome", "mighty fine"),
        ("small", "little bitty"), ("tiny", "little bitty"), ("huge", "whopping"),
        // things
        ("hat", "Stetson"), ("shoes", "boots"), ("shoe", "boot"), ("pants", "britches"),
        ("animal", "critter"), ("animals", "critters"), ("bug", "critter"), ("pest", "varmint"), ("steal", "rustle"),
        // more words
        ("hell", "heck"), ("fuck", "dang"), ("fucking", "dadgum"),
        ("shit", "dang"), ("crazy", "plumb loco"), ("stupid", "dumb as a box of rocks"),
        ("amazing", "downright amazin'"), ("totally", "plumb"), ("absolutely", "plumb"), ("almost", "just about"),
        ("a lot", "a whole bunch"), ("lots of", "a whole heap of"),
        ("pistol", "six-shooter"), ("revolver", "six-shooter"), ("gun", "shootin' iron"),
        ("farm", "spread"), ("ranch", "spread"), ("food", "grub"), ("coffee", "joe"),
        ("beer", "cold one"), ("beers", "cold ones"), ("whiskey", "red-eye"),
        ("dollar", "buck"), ("dollars", "bucks"),
        // colony and military life
        ("monster", "varmint"), ("monsters", "varmints"),
        ("colonist", "homesteader"), ("colonists", "homesteaders"), ("colony", "settlement"),
        ("miner", "prospector"), ("miners", "prospectors"), ("medic", "sawbones"), ("doctor", "sawbones"),
        ("shotgun", "scattergun"), ("prisoner", "jailbird"), ("prisoners", "jailbirds"),
        ("ambush", "bushwhack"), ("ambushed", "bushwhacked"), ("retreat", "skedaddle"), ("run away", "skedaddle"),
        ("take cover", "hunker down"), ("hide", "hole up"),
        // swearing
        ("bullshit", "hogwash"), ("asshole", "jackass"), ("bastard", "scoundrel"));

    // "god damn" / "goddamn" -> "got damn". Handled outside the word list so the plain "damn" rule can skip it.
    private static readonly Regex GodDamn = new(@"\bgod\s?damn\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    // "damn it" -> "dadgummit", except right after "got" ("god damn it" becomes "got damn it")
    private static readonly Regex DamnIt = new(@"(?<!\bgot\s)\bdamn it\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    // "damn" -> "dadgum", except right after "got" (so "got damn" is left alone)
    private static readonly Regex Damn = new(@"(?<!\bgot\s)\bdamn\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    // "pal" -> "pardner", except in "ol' pal" (which "my friend" turns into)
    private static readonly Regex Pal = new(@"(?<!\bol'\s)\bpal\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static string Apply(string message)
    {
        var msg = GodDamn.Replace(message, m => CMUAccentText.MatchCase(m.Value, "got damn"));
        msg = CMUAccentText.ApplyWords(Words, msg);
        msg = Pal.Replace(msg, m => CMUAccentText.MatchCase(m.Value, "pardner"));
        msg = DamnIt.Replace(msg, m => CMUAccentText.MatchCase(m.Value, "dadgummit"));
        msg = Damn.Replace(msg, m => CMUAccentText.MatchCase(m.Value, "dadgum"));
        return CMUAccentText.DropG(msg);
    }
}
