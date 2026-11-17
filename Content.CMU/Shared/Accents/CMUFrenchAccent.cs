/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
using System.Text.RegularExpressions;
using Content.Shared._RMC14.Language.Prototypes;
using Content.Shared._RMC14.Language.Systems;
using Content.Shared.Speech;
using Content.Shared.Speech.Components;
using Content.Shared.Speech.EntitySystems;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Speech;

/// <summary>
/// CMU French accent. Gives the speaker a faux-French accent:
/// "th" becomes "z" or "t", "h" at the start of a word becomes "'" (the French silent h), and ! ? : ; get a
/// space in front of them. See <see cref="CMUFrenchAccentRules"/> for the exact rules.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(CMUFrenchAccentSystem))]
public sealed partial class CMUFrenchAccentComponent : BaseAccentComponent;

public sealed partial class CMUFrenchAccentSystem : RelayAccentSystem<CMUFrenchAccentComponent>
{
    private static readonly ProtoId<LanguagePrototype> FrenchLanguage = "French";

    [Dependency] private SharedLanguageSystem _language = default!;

    /// <summary>
    /// The accent is skipped while the speaker is speaking French: you do not need a French accent to speak
    /// French. args.Entity is the speaker, also when the accent comes in through worn clothing.
    /// </summary>
    protected override void OnAccent(Entity<CMUFrenchAccentComponent> ent, ref AccentGetEvent args)
    {
        if (_language.GetCurrentLanguage(args.Entity) == FrenchLanguage)
            return;

        base.OnAccent(ent, ref args);
    }

    public override string Accentuate(string message, Entity<CMUFrenchAccentComponent>? ent = null)
        => CMUFrenchAccentRules.Apply(message);
}

/// <summary>
/// The French accent rules as plain string code with no game dependencies, so they are easy to read and to test.
/// Running them twice on the same message changes nothing, which matters because an accent can be applied both
/// directly and through an inventory relay.
/// </summary>
public static class CMUFrenchAccentRules
{
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    // voiced "th" -> "z" in common words (the -> ze, this -> zis), plus "with" -> "wiz". Whole words only, but contractions (they're, that's) still match.
    private static readonly (Regex Pattern, MatchEvaluator Replace)[] Words = CMUAccentText.BuildWords(
        // phrases that contain "the" or a swear word come first
        ("the company", "la compagnie"), ("what the hell", "zut alors"), ("what the fuck", "zut alors"),
        ("holy shit", "merde alors"),
        ("the", "ze"), ("this", "zis"), ("that", "zat"), ("these", "zese"), ("those", "zose"),
        ("they", "zey"), ("them", "zem"), ("their", "zeir"), ("there", "zere"), ("then", "zen"),
        ("than", "zan"), ("thus", "zus"),
        ("with", "wiz"), ("without", "wizout"), ("within", "wizin"),
        // French words. Add more pairs here: ("english word", "french word").
        ("yes", "oui"), ("thanks", "merci"),
        ("friend", "cher"), ("good", "bon"), ("little", "petit"), ("hello", "allo"), ("hi", "allo"),
        ("goodbye", "au revoir"), ("bye", "au revoir"), ("please", "s'il vous plait"),
        ("sorry", "pardon"), ("excuse me", "pardon"), ("of course", "bien sur"), ("maybe", "peut-etre"),
        ("my", "mon"), ("very", "tres"), ("mister", "monsieur"), ("ma'am", "madame"),
        ("beautiful", "belle"),
        ("cheese", "fromage"), ("wine", "vin"), ("coffee", "cafe"), ("water", "eau"),
        // colony and military life
        ("soldier", "soldat"), ("soldiers", "soldats"), ("grunt", "troufion"), ("captain", "capitaine"),
        ("sergeant", "sergent"), ("commander", "commandant"), ("doctor", "docteur"),
        ("colony", "colonie"), ("colonist", "pionnier"), ("colonists", "pionniers"),
        ("mess hall", "cantine"), ("beer", "biere"), ("whiskey", "cognac"),
        ("monster", "monstre"), ("monsters", "monstres"), ("weapon", "arme"), ("weapons", "armes"),
        ("dangerous", "dangereux"), ("attack", "attaque"), ("retreat", "retraite"),
        ("watch out", "attention"), ("look out", "attention"), ("hurry up", "vite"), ("quickly", "vite"),
        // swearing
        ("fuck", "merde"), ("fucking", "sacre"), ("shit", "merde"), ("bullshit", "des conneries"),
        ("damn", "zut"), ("goddamn", "zut alors"), ("hell", "zut"), ("asshole", "connard"), ("bastard", "salaud"));

    // hesitation sounds -> "euh". Not preceded by ' so the contraction 'em is left alone.
    private static readonly Regex Fillers = new(@"(?<![\w'])(?:uh+|um+|erm+|em|er)(?![\w'])", Options);

    // thing, nothing, something, anything... -> "t" (these are voiceless, so they must not be left alone like "mother" below)
    private static readonly Regex ThBeforeIng = new(@"th(?=ing)", Options);

    // every other "th" (think, thanks, three, both...) -> "t", except between two vowels: other, mother,
    // together, whether... are left alone
    private static readonly Regex ThAny = new(@"(?<![aeiou])th|th(?![aeiouy])", Options);

    // the silent French h: "hello" -> "'ello", "Hello" -> "'Ello"
    private static readonly Regex StartH = new(@"(?<![\w'])([Hh])(\w)", RegexOptions.CultureInvariant);

    // French puts a space before ! ? : ;  ("What?" -> "What ?")
    private static readonly Regex SpaceBeforePunctuation = new(@"(?<=\w\w)[!?;:](?!\w)", RegexOptions.CultureInvariant);

    public static string Apply(string message)
    {
        var msg = CMUAccentText.ApplyWords(Words, message);

        msg = Fillers.Replace(msg, m => CMUAccentText.MatchCase(m.Value, "euh"));

        msg = ThBeforeIng.Replace(msg, m => CMUAccentText.MatchCase(m.Value, "t"));
        msg = ThAny.Replace(msg, m => CMUAccentText.MatchCase(m.Value, "t"));

        msg = StartH.Replace(msg, m =>
        {
            var next = m.Groups[2].Value;
            // keep sentence-start capitals: "Hello" -> "'Ello"
            return m.Groups[1].Value == "H" && char.IsLower(next[0])
                ? "'" + next.ToUpperInvariant()
                : "'" + next;
        });

        msg = SpaceBeforePunctuation.Replace(msg, " $&");

        return msg;
    }
}
