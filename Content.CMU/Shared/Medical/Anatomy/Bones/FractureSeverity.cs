namespace Content.Shared._CMU14.Medical.Anatomy.Bones;

/// <summary>
///     Stage value is monotonically increasing:
///     <c>(byte)Hairline &lt; (byte)Shattered</c>. Treat-then-compare with
///     <see cref="FractureSeverityExtensions.IsAtLeast"/> to keep the ordering
///     contract honest if entries are reshuffled later.
/// </summary>
public enum FractureSeverity : byte
{
    None = 0,
    Hairline,
    Simple,
    Compound,
    Shattered,
}

public static class FractureSeverityExtensions
{
    public static bool IsAtLeast(this FractureSeverity self, FractureSeverity other)
        => (byte)self >= (byte)other;

    public static bool RequiresSurgery(this FractureSeverity self)
        => self.IsAtLeast(FractureSeverity.Compound);
}
