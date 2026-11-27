using Content.Shared.CMU14.Photography;

namespace Content.Shared.Fax.Components;

public sealed partial class FaxPrintout
{
    /// <summary>A detached copy of the scanned print. Its ID refers to the existing full-size picture.</summary>
    [DataField]
    public CMUPhotoComponent? Photo;
}
