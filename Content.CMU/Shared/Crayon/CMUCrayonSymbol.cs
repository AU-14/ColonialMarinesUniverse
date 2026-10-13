using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Crayon;

/// <summary>
/// A player drawn crayon symbol: a square of palette indices, 0 empty, n the palette's (n-1)th colour.
/// </summary>
[DataDefinition, Serializable, NetSerializable]
public sealed partial class CMUCrayonSymbol
{
    public const int Small = 32;
    public const int Large = 64;
    public const int MaxColors = 8;

    [DataField]
    public int Size = Small;

    [DataField]
    public List<Color> Palette = new();

    [DataField]
    public byte[] Pixels = [];

    public bool IsValid()
    {
        if (Size is not (Small or Large) ||
            Pixels.Length != Size * Size ||
            Palette.Count is 0 or > MaxColors)
        {
            return false;
        }

        var used = false;
        foreach (var pixel in Pixels)
        {
            if (pixel > Palette.Count)
                return false;

            used |= pixel != 0;
        }

        return used;
    }

    /// <summary>
    /// The palette colours at least one pixel uses.
    /// </summary>
    public HashSet<Color> UsedColors()
    {
        var used = new HashSet<Color>();
        foreach (var pixel in Pixels)
        {
            if (pixel != 0)
                used.Add(Palette[pixel - 1]);
        }

        return used;
    }
}
