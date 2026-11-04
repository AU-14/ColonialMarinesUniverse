using Robust.Shared.GameObjects;

namespace Content.Shared.CMU14.Medical.Anatomy.Organs.Events;

/// <summary>
/// An organ's health component has started or is stopping. Consumers re-read the
/// current component and its lifecycle; anatomical relationships are unchanged.
/// </summary>
[ByRefEvent]
public readonly record struct OrganHealthLifecycleChangedEvent(EntityUid Organ, EntityUid? Body);
