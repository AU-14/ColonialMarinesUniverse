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
    // Room for gradients: a few crayons in several tones each.
    public const int MaxColors = 16;

    [DataField]
    public int Size = Small;

    [DataField]
    public List<Color> Palette = new();

    [DataField]
    public byte[] Pixels = [];

    public bool IsValid()
    {
        // Sent by a client, either can arrive missing.
        if (Pixels == null ||
            Palette == null ||
            Size is not (Small or Large) ||
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

    /// <summary>
    /// Whether another symbol draws the same, its colours compared to whole bytes.
    /// </summary>
    public bool SameAs(CMUCrayonSymbol other)
    {
        if (Size != other.Size || Palette.Count != other.Palette.Count || Pixels.Length != other.Pixels.Length)
            return false;

        for (var i = 0; i < Palette.Count; i++)
        {
            if (!CMUCrayonPaletteSystem.Same(Palette[i], other.Palette[i]))
                return false;
        }

        for (var i = 0; i < Pixels.Length; i++)
        {
            if (Pixels[i] != other.Pixels[i])
                return false;
        }

        return true;
    }
}
