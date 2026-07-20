using Content.Shared.Speech.EntitySystems;

namespace Content.Server.Speech.EntitySystems;

public sealed partial class RatvarianLanguageSystem : SharedRatvarianLanguageSystem
{
    [Dependency] private StatusEffectsSystem _statusEffects = default!;

    private static readonly ProtoId<StatusEffectPrototype> RatvarianKey = "RatvarianLanguage";

    // This is the word of Ratvar and those who speak it shall abide by His rules:
    /*
     * Any time the word "of" occurs, it's linked to the previous word by a hyphen: "I am-of Ratvar"
     * Any time "th", followed by any two letters occurs, you add a grave (`) between those two letters: "Thi`s"
     * In the same vein, any time "ti" followed by one letter occurs, you add a grave (`) between "i" and the letter: "Ti`me"
     * Wherever "te" or "et" appear and there is another letter next to the "e", add a hyphen between "e" and the letter: "M-etal/Greate-r"
     * Where "gua" appears, add a hyphen between "gu" and "a": "Gu-ard"
     * Where the word "and" appears it's linked to all surrounding words by hyphens: "Sword-and-shield"
     * Where the word "to" appears, it's linked to the following word by a hyphen: "to-use"
     * Where the word "my" appears, it's linked to the following word by a hyphen: "my-light"
     * Any Ratvarian proper noun is not translated: Ratvar, Nezbere, Sevtug, Nzcrentr and Inath-neq
        * This only applies if they're being used as a proper noun: armorer/Nezbere
     */

    private static Regex THPattern = new Regex(@"th\w\B", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static Regex ETPattern = new Regex(@"\Bet", RegexOptions.Compiled);
    private static Regex TEPattern = new Regex(@"te\B",RegexOptions.Compiled);
    private static Regex OFPattern = new Regex(@"(\s)(of)");
    private static Regex TIPattern = new Regex(@"ti\B", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static Regex GUAPattern = new Regex(@"(gu)(a)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static Regex ANDPattern = new Regex(@"\b(\s)(and)(\s)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static Regex TOMYPattern = new Regex(@"(to|my)\s", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static Regex ProperNouns = new Regex(@"(ratvar)|(nezbere)|(sevtuq)|(nzcrentr)|(inath-neq)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public override void Initialize()
    {
        if (refresh)
            Status.TryUpdateStatusEffectDuration(uid, Ratvarian, time);
        else
            Status.TryAddStatusEffectDuration(uid, Ratvarian, time);
    }
}
