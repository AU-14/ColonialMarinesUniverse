namespace Content.Shared.CMU14.Forensics.Toolmarks;

public enum CMUToolmarkKind : byte
{
    Pry,
    Weld,
}

/// <summary>
///     Raised broadcast when a tool forces something open or welds it, so the tool can leave its marks on it.
/// </summary>
[ByRefEvent]
public readonly record struct CMUToolmarkEvent(EntityUid Target, EntityUid Tool, CMUToolmarkKind Kind);
