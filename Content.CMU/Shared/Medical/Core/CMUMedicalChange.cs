using System;
using Robust.Shared.GameObjects;

namespace Content.Shared._CMU14.Medical.Core;

[Flags]
public enum CMUMedicalChangeFlags : ushort
{
    None = 0,
    Anatomy = 1 << 0,
    Wounds = 1 << 1,
    Fractures = 1 << 2,
    Organs = 1 << 3,
    Pain = 1 << 4,
    Treatment = 1 << 5,
    Surgery = 1 << 6,
    Visuals = 1 << 7,
}

/// <summary>
///     Coalesced medical invalidation raised at most once per body per simulation tick.
/// </summary>
[ByRefEvent]
public readonly record struct CMUMedicalChangedEvent(
    EntityUid Body,
    uint Revision,
    CMUMedicalChangeFlags Changes);
