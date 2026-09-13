using Robust.Shared.GameObjects;

namespace Content.Shared.CMU14.Medical.Core;

[ByRefEvent]
public readonly record struct CMUInternalsChangedEvent(
    bool Working,
    EntityUid? GasTank);
