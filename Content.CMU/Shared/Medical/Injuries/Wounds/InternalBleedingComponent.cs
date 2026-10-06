using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Medical.Injuries.Wounds;

/// <summary>
///     Multiple sources are NOT additive: the highest single rate wins. The
///     <see cref="Source"/> tag carries the contributing reason for
///     diagnostics only; recompute is source-agnostic so a fracture clearing
///     while a damaged organ is still in the part keeps the bleed alive.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(SharedCMUWoundsSystem))]
public sealed partial class InternalBleedingComponent : Component
{
    internal Action<InternalBleedingComponent>? ScheduleChanged;
    internal EntityUid ScheduledOwner;
    [DataField, AutoNetworkedField]
    public float BloodlossPerSecond = 0.5f;

    /// <summary>
    ///     Diagnostic tag — e.g. <c>fracture:Compound</c> or
    ///     <c>organ:Heart</c>. Surfaced by the X-ray scanner on the readout.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string Source = "unknown";

    [DataField, AutoPausedField]
    public TimeSpan NextBleedTick
    {
        get => _nextBleedTick;
        set
        {
            _nextBleedTick = value;
            ScheduleChanged?.Invoke(this);
        }
    }

    private TimeSpan _nextBleedTick;
}

[ByRefEvent]
public readonly record struct InternalBleedingChangedEvent(EntityUid Body, EntityUid Part, bool Removed);
