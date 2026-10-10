using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Radio;

// ----- key analysis: breaking an enemy net's COMSEC from the faceplate ------------------------------
// the enemy key is KeyLength symbols from KeySymbols, no symbol twice. every intercepted enemy line
// on a net this set has fixed banks one trial; a trial tests a whole guessed key and answers with how
// many positions are right. the key itself never leaves the server

// one key trial against a faction's current key. costs a banked trial
[Serializable, NetSerializable]
public sealed class ANPRCKeyTrialMsg(string faction, string trial) : BoundUserInterfaceMessage
{
    public readonly string Faction = faction;
    public readonly string Trial = trial;
}

[Serializable, NetSerializable]
public readonly record struct ANPRCKeyTrial(string Key, int Hits);

// what the faceplate shows of one enemy faction's key
[Serializable, NetSerializable]
public sealed class ANPRCKeyAnalysisState
{
    public string Faction = string.Empty;
    public int Depth;
    public int DepthMax;
    public bool Broken;
    public List<ANPRCKeyTrial> Trials = new();
}

public static class ANPRCKeyAnalysis
{
    public const string KeySymbols = "ABCDEFGH";
    public const int KeyLength = 6;

    // positional hits of a trial against a key. both are KeyLength long by the time this is called
    public static int Hits(string key, string trial)
    {
        var hits = 0;

        for (var i = 0; i < KeyLength; i++)
        {
            if (key[i] == trial[i])
                hits++;
        }

        return hits;
    }

    // upper-cased and trimmed, or null when it is not KeyLength symbols from the key alphabet
    public static string? Normalize(string? trial)
    {
        if (trial == null)
            return null;

        var upper = trial.Trim().ToUpperInvariant();

        if (upper.Length != KeyLength)
            return null;

        foreach (var symbol in upper)
        {
            if (!KeySymbols.Contains(symbol))
                return null;
        }

        return upper;
    }
}
