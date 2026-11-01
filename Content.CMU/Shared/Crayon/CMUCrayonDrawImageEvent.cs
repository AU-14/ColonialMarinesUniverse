namespace Content.Shared.CMU14.Crayon;

/// <summary>
/// Raised on a crayon about to draw its decal somewhere other than the floor, so a custom image can be
/// drawn instead. A handler that draws one sets the image, having spent what it costs; one that can't
/// sets <see cref="Cancelled"/> once it has told the user why.
/// </summary>
[ByRefEvent]
public record struct CMUCrayonDrawImageEvent(EntityUid User)
{
    public bool Cancelled;

    public int Size;

    /// <summary>
    /// A square of palette indices: 0 is empty, n the palette's (n-1)th colour.
    /// </summary>
    public byte[]? Pixels;

    public List<Color>? Palette;
}
