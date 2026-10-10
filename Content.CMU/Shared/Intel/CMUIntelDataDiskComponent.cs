using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Intel;

/// <summary>
/// An encrypted data disk. Use it on an intel computer and crack its code to upload it; the Analyzer Machine
/// can't read disks. The code is a string of digits: each guess reports how many digits are right and in the
/// right place, and how many are right but misplaced.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUIntelDataDiskComponent : Component
{
    /// <summary>Intel points credited to the uploading computer's faction.</summary>
    [DataField, AutoNetworkedField]
    public FixedPoint2 Value = FixedPoint2.New(1);

    [DataField]
    public int CodeLength = 4;

    /// <summary>Each digit of the code is 0 up to this, exclusive.</summary>
    [DataField]
    public int DigitRange = 6;

    [DataField, AutoNetworkedField]
    public bool Uploaded;

    // Server-only puzzle state. The code is never sent to clients.
    public string Code = string.Empty;
    public readonly List<CMUIntelDecryptGuess> History = new();
    public EntityUid? Console;
}

[Serializable, NetSerializable]
public enum CMUIntelDecryptUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed record CMUIntelDecryptGuess(string Guess, int Exact, int Misplaced);

[Serializable, NetSerializable]
public sealed class CMUIntelDecryptBuiState(
    List<CMUIntelDecryptGuess> history,
    int codeLength,
    int digitRange,
    string status,
    bool done) : BoundUserInterfaceState
{
    public readonly List<CMUIntelDecryptGuess> History = history;
    public readonly int CodeLength = codeLength;
    public readonly int DigitRange = digitRange;
    public readonly string Status = status;
    public readonly bool Done = done;
}

[Serializable, NetSerializable]
public sealed class CMUIntelDecryptGuessMessage(string guess) : BoundUserInterfaceMessage
{
    public readonly string Guess = guess;
}
