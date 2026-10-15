using System.Collections.Generic;
using Robust.Shared.GameStates;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Medical.Injuries.Wounds;

/// <summary>
///     Server-owned per-body-part wound ledger. Clients receive only the
///     compact body-level public examine and overlay projections.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
[Access(typeof(SharedCMUWoundsSystem), typeof(CMUWoundLedgerSystem))]
public sealed partial class BodyPartWoundComponent : Component
{
    internal Action<BodyPartWoundComponent>? ScheduleChanged;
    internal EntityUid ScheduledOwner;
    /// <summary>
    ///     The sole source of truth for wound and treatment state. Callers
    ///     cross <see cref="CMUWoundLedgerSystem"/> instead of mutating it.
    /// </summary>
    [DataField]
    internal List<CMUWoundEntry> Entries = new();

    // Server-local mutation identity for continuations across public healing callbacks.
    internal ulong Revision;

    [DataField]
    public ExternalBleedTier ExternalBleeding
    {
        get => _externalBleeding;
        set
        {
            _externalBleeding = value;
            ScheduleChanged?.Invoke(this);
        }
    }

    private ExternalBleedTier _externalBleeding;

    [DataField, AutoPausedField]
    public TimeSpan ExternalBleedSuppressedUntil;

    [DataField, AutoPausedField]
    public TimeSpan NextExternalBleedTick
    {
        get => _nextExternalBleedTick;
        set
        {
            _nextExternalBleedTick = value;
            ScheduleChanged?.Invoke(this);
        }
    }

    private TimeSpan _nextExternalBleedTick;

    [DataField, AutoPausedField]
    public TimeSpan NextHealTick
    {
        get => _nextHealTick;
        set
        {
            _nextHealTick = value;
            ScheduleChanged?.Invoke(this);
        }
    }

    private TimeSpan _nextHealTick;
}
