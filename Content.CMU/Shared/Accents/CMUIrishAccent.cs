/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
using System.Text.RegularExpressions;
using Content.Shared._RMC14.Language.Prototypes;
using Content.Shared._RMC14.Language.Systems;
using Content.Shared.Speech;
using Content.Shared.Speech.Components;
using Content.Shared.Speech.EntitySystems;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared.CMU14.Speech;

/// <summary>
/// CMU Irish accent. Swaps in Irish words (grand, eejit, craic, yez, me for my...) and sometimes ends a message
/// with ", so it is". See <see cref="CMUIrishAccentRules"/> for the exact rules.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(CMUIrishAccentSystem))]
public sealed partial class CMUIrishAccentComponent : BaseAccentComponent
{
    /// <summary>Chance (0 to 1) that a message gets ", so it is" added to the end.</summary>
    [DataField]
    public float SoItIsChance = 0.1f;
}

public sealed partial class CMUIrishAccentSystem : RelayAccentSystem<CMUIrishAccentComponent>
{
    private static readonly ProtoId<LanguagePrototype> IrishLanguage = "Irish";

    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedLanguageSystem _language = default!;

    /// <summary>
    /// The accent is skipped while the speaker is speaking Irish: you do not need an Irish accent to speak
    /// Irish. args.Entity is the speaker, also when the accent comes in through worn clothing.
    /// </summary>
    protected override void OnAccent(Entity<CMUIrishAccentComponent> ent, ref AccentGetEvent args)
    {
        if (_language.GetCurrentLanguage(args.Entity) == IrishLanguage)
            return;

        base.OnAccent(ent, ref args);
    }

    public override string Accentuate(string message, Entity<CMUIrishAccentComponent>? ent = null)
    {
        var msg = CMUIrishAccentRules.Apply(message);

        var chance = ent?.Comp.SoItIsChance ?? 0.1f;
        if (_random.Prob(chance))
            msg = CMUIrishAccentRules.AddSoItIs(msg);

        return msg;
    }
}

/// <summary>
/// The Irish accent rules as plain string code with no game dependencies. Running them twice on the same
/// message changes nothing.
/// </summary>
public static class CMUIrishAccentRules
{
    private static readonly Regex EndsWithTag = new(@"\bso it is\W*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static string AddSoItIs(string message)
        => CMUAccentText.AppendTag(message, "so it is", EndsWithTag);

    // Whole words only. Add more pairs here: ("english word", "irish word").
    private static readonly (Regex Pattern, MatchEvaluator Replace)[] Words = CMUAccentText.BuildWords(
        // greetings and phrases
        ("what the fuck", "what the feck"), ("what the hell", "what the feck"), ("holy shit", "holy feck"),
        ("oh my god", "Jaysus"), ("jesus christ", "Jaysus"),
        ("hi", "howya"), ("hey", "howya"),
        ("how are you", "how's the form"), ("how are things", "how's the form"), ("what's up", "what's the craic"),
        ("what's going on", "what's the story"), ("what's happening", "what's the story"),
        ("i don't know", "I haven't a notion"), ("of course", "sure"), ("obviously", "sure"),
        ("you're welcome", "no bother"), ("you are welcome", "no bother"), ("no problem", "no bother"),
        ("hurry up", "move yourself"), ("shut up", "shut yer gob"), ("go away", "go way outta that"),
        ("a lot of", "loads of"), ("lots of", "loads of"), ("a lot", "loads"), ("a little", "a small bit"),
        ("goodbye", "slan"), ("bye", "slan"), ("thank you", "thanks a million"),
        ("kidding", "coddin'"), ("joking", "coddin'"),
        ("you all", "yez"), ("you guys", "yez"),
        // speech
        ("my", "me"), ("very", "awful"), ("really", "fierce"),
        ("good", "grand"), ("fine", "grand"), ("okay", "grand"), ("alright", "grand"),
        ("fun", "craic"),
        // people
        ("guy", "lad"), ("guys", "lads"), ("boy", "lad"), ("boys", "lads"), ("man", "fella"), ("men", "fellas"),
        ("girl", "lass"), ("girls", "lasses"), ("friend", "pal"), ("friends", "pals"),
        ("police", "guards"), ("cop", "guard"), ("cops", "guards"),
        ("idiot", "eejit"), ("fool", "eejit"),
        // how things are
        ("broken", "banjaxed"), ("tired", "wrecked"), ("exhausted", "wrecked"), ("drunk", "langers"),
        ("crazy", "cracked"), ("angry", "raging"), ("hungry", "starvin'"), ("stupid", "thick"),
        ("great", "deadly"), ("awesome", "deadly"), ("terrible", "desperate"), ("horrible", "desperate"),
        // things
        ("toilet", "jacks"), ("bathroom", "jacks"), ("beer", "pint"), ("beers", "pints"),
        ("house", "gaff"), ("trash", "shite"), ("rubbish", "shite"),
        ("sweater", "jumper"), ("candy", "sweets"), ("cookie", "biscuit"), ("cookies", "biscuits"),
        ("fries", "chips"), ("sidewalk", "footpath"), ("elevator", "lift"), ("flashlight", "torch"),
        ("sneakers", "runners"), ("mouth", "gob"), ("bar", "pub"), ("bartender", "barman"),
        ("potato", "spud"), ("potatoes", "spuds"), ("sausage", "banger"), ("sausages", "bangers"),
        ("thing", "yoke"), ("things", "yokes"), ("cell phone", "mobile"),
        ("cellphone", "mobile"), ("vacation", "holidays"), ("truck", "lorry"), ("gasoline", "petrol"),
        ("apartment", "flat"),
        // more feelings and talk
        ("amazing", "class"), ("fantastic", "class"), ("dirty", "manky"), ("disgusting", "manky"),
        ("sick", "poorly"), ("beautiful", "lovely"), ("freezing", "perishing"), ("nonsense", "codology"),
        // colony and military life
        ("mess hall", "canteen"), ("cafeteria", "canteen"), ("whiskey", "the hard stuff"), ("liquor", "the hard stuff"),
        ("food", "scran"), ("rations", "scran"), ("retreat", "leg it"), ("run away", "leg it"),
        ("scared", "afeard"), ("afraid", "afeard"), ("fight", "scrap"), ("nap", "kip"),
        // swearing
        ("bullshit", "codology"), ("asshole", "gobshite"), ("jerk", "gobshite"), ("bastard", "bollix"), ("ass", "arse"),
        // swearing
        ("fuck", "feck"), ("fucking", "fecking"), ("shit", "shite"), ("damn", "feck"));

    public static string Apply(string message)
        => CMUAccentText.ApplyWords(Words, message);
}
