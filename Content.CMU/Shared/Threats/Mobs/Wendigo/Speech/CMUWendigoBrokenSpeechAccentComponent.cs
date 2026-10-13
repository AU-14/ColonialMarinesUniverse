using Content.Shared.Speech.Components;
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Threats.Mobs.Wendigo.Speech;

/// <summary>
/// Wendigo speech: words break up like brain damage, drunkenness or radio static.
/// "They're in the upper window!" may come out as "IIn.. Uuupperrr.. ... Window!".
/// Each chance is rolled per word; most words survive in some form.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(CMUWendigoBrokenSpeechAccentSystem))]
public sealed partial class CMUWendigoBrokenSpeechAccentComponent : BaseAccentComponent
{
    public const float DefaultDropChance = 0.12f;
    public const float DefaultStretchChance = 0.35f;
    public const float DefaultDoubleChance = 0.25f;
    public const float DefaultTrailChance = 0.3f;
    public const float DefaultCapitalizeChance = 0.2f;

    /// <summary>
    /// Chance a word is lost and replaced with "...". Never two words in a row.
    /// </summary>
    [DataField]
    public float DropChance = DefaultDropChance;

    /// <summary>
    /// Chance one vowel is stretched to two or three times its length.
    /// </summary>
    [DataField]
    public float StretchChance = DefaultStretchChance;

    /// <summary>
    /// Chance the first or last letter is doubled.
    /// </summary>
    [DataField]
    public float DoubleChance = DefaultDoubleChance;

    /// <summary>
    /// Chance ".." trails a word that has no punctuation of its own.
    /// </summary>
    [DataField]
    public float TrailChance = DefaultTrailChance;

    /// <summary>
    /// Chance a word's first letter is capitalised.
    /// </summary>
    [DataField]
    public float CapitalizeChance = DefaultCapitalizeChance;
}
