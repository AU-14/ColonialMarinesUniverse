namespace Content.Client.CMU14.ThreeD.Scene;

/// <summary>RSI authoring accepts texture-relative and resource-rooted paths.</summary>
public static class CMU3DSourceReference
{
    public static bool Matches(string? actual, string? reference)
    {
        if (actual == null || reference == null)
            return false;
        var actualStart = RelativeStart(actual);
        var referenceStart = RelativeStart(reference);
        var length = actual.Length - actualStart;
        return length == reference.Length - referenceStart &&
               string.CompareOrdinal(actual, actualStart, reference, referenceStart, length) == 0;
    }

    // Compare by offsets so resource normalization stays sandbox-verifiable.
    private static int RelativeStart(string path)
    {
        var start = 0;
        while (start < path.Length && path[start] == '/')
            start++;
        return path.Length - start >= 9 && string.CompareOrdinal(path, start, "Textures/", 0, 9) == 0
            ? start + 9
            : start;
    }
}
