// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 CatoChristopherMrow
// SPDX-License-Identifier: AGPL-3.0-only
namespace Content.Shared.Movement.Events;

/// <summary>
/// Raised when selecting tile movement properties so a non-tile surface can identify itself as virtual ground.
/// </summary>
[ByRefEvent]
public record struct IsVirtualGroundForMovementEvent
{
    public bool Grounded;
}
