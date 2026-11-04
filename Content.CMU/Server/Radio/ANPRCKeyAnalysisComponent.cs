namespace Content.Server.CMU14.Radio;

/// <summary>
///     A set's work against enemy COMSEC keys (the faceplate's key analysis). Server only: the banked
///     trials and results go out in the expert state, the keys never do. It lives on the set, so a
///     captured set carries its break with it.
/// </summary>
[RegisterComponent, Access(typeof(ANPRCCryptoSystem))]
public sealed partial class ANPRCKeyAnalysisComponent : Component
{
    /// <summary>Lower-case faction id -> the work against that faction's current key.</summary>
    [ViewVariables]
    public Dictionary<string, ANPRCKeyWork> Factions = new();
}

public sealed class ANPRCKeyWork
{
    /// <summary>The key generation this work was done against. A recrypto starts it over.</summary>
    public int Generation;

    /// <summary>Trials banked from intercepted traffic and not yet spent.</summary>
    public int Depth;

    public readonly List<(string Key, int Hits)> Trials = new();

    public bool Broken;
}
