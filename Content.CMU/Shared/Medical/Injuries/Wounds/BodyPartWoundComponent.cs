using System.Collections.Generic;
using Robust.Shared.GameStates;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.Medical.Injuries.Wounds;

/// <summary>
///     Server-owned per-body-part wound ledger. Clients receive only the
///     compact body-level public examine and overlay projections.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
[Access(typeof(SharedCMUWoundsSystem), typeof(CMUWoundLedgerSystem))]
public sealed partial class BodyPartWoundComponent : Component
{
    /// <summary>
    ///     The sole source of truth for wound and treatment state. Callers
    ///     cross <see cref="CMUWoundLedgerSystem"/> instead of mutating it.
    /// </summary>
    [DataField]
    internal List<CMUWoundEntry> Entries = new();

    [DataField]
    public ExternalBleedTier ExternalBleeding;

    [DataField, AutoPausedField]
    public TimeSpan ExternalBleedSuppressedUntil;

    [DataField, AutoPausedField]
    public TimeSpan NextExternalBleedTick;

    [DataField, AutoPausedField]
    public TimeSpan NextHealTick;
}
