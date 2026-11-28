using System.Linq;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.CorporateWork;

[Serializable, NetSerializable]
public enum CMUTransmissionsTerminalUi : byte
{
    Key,
}

[Serializable, NetSerializable]
public readonly record struct CMUTransmissionsTierInfo(string Name, int Size, int Buffer, int Sequences, int Reward, int? TimeLimitSeconds);

/// <param name="Completed">How many of the target sequences ended up in the buffer.</param>
[Serializable, NetSerializable]
public readonly record struct CMUTransmissionsResult(int Completed, int Total, int Payout, bool TimedOut);

[Serializable, NetSerializable]
public sealed class CMUTransmissionsTerminalBuiState(
    List<string> codes,
    List<CMUTransmissionsTierInfo> tiers,
    int? activeTier,
    int[] grid,
    List<int[]> sequences,
    List<int> picks,
    TimeSpan? deadline,
    TimeSpan nextCaseAt,
    CMUTransmissionsResult? lastResult) : BoundUserInterfaceState
{
    public readonly List<string> Codes = codes;
    public readonly List<CMUTransmissionsTierInfo> Tiers = tiers;
    public readonly int? ActiveTier = activeTier;
    public readonly int[] Grid = grid;
    public readonly List<int[]> Sequences = sequences;
    public readonly List<int> Picks = picks;
    public readonly TimeSpan? Deadline = deadline;
    public readonly TimeSpan NextCaseAt = nextCaseAt;
    public readonly CMUTransmissionsResult? LastResult = lastResult;
}

[Serializable, NetSerializable]
public sealed class CMUTransmissionsStartBuiMsg(int tier) : BoundUserInterfaceMessage
{
    public readonly int Tier = tier;
}

/// <summary>
///     Picks a cell of the grid, by index row by row.
/// </summary>
[Serializable, NetSerializable]
public sealed class CMUTransmissionsPickBuiMsg(int cell) : BoundUserInterfaceMessage
{
    public readonly int Cell = cell;
}

/// <summary>
///     Ends the case with what's in the buffer now.
/// </summary>
[Serializable, NetSerializable]
public sealed class CMUTransmissionsUploadBuiMsg : BoundUserInterfaceMessage;

/// <summary>
///     The rules of a case, shared so the window can show which cells can be picked next.
/// </summary>
public static class CMUTransmissionsRules
{
    /// <summary>
    ///     Whether <paramref name="cell"/> can be picked after <paramref name="picks"/>: the top row
    ///     first, then alternately the last pick's column and its row, never a cell twice.
    /// </summary>
    public static bool CanPick(int size, IReadOnlyList<int> picks, int cell)
    {
        if (cell < 0 || cell >= size * size || picks.Contains(cell))
            return false;

        var row = cell / size;
        var col = cell % size;
        if (picks.Count == 0)
            return row == 0;

        var last = picks[^1];
        return picks.Count % 2 == 1
            ? col == last % size // odd picks so far: stay in the last pick's column
            : row == last / size; // even: stay in its row
    }

    /// <summary>
    ///     Whether <paramref name="sequence"/> appears in <paramref name="buffer"/>, in order and unbroken.
    /// </summary>
    public static bool Contains(IReadOnlyList<int> buffer, int[] sequence)
    {
        for (var start = 0; start + sequence.Length <= buffer.Count; start++)
        {
            var match = true;
            for (var i = 0; i < sequence.Length && match; i++)
                match = buffer[start + i] == sequence[i];

            if (match)
                return true;
        }

        return false;
    }
}
