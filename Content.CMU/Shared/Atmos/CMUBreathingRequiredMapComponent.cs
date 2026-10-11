namespace Content.Shared.CMU14.Atmos;

/// <summary>
/// Put on a map entity to make humans breathe while they're on it, or anywhere else in its z-network. Breathing is
/// otherwise disabled for humans, so this is how a planet with hostile air (like Bosenmori's nitrogen atmosphere)
/// becomes dangerous without internals. Leaving the map turns breathing back off.
/// </summary>
[RegisterComponent]
public sealed partial class CMUBreathingRequiredMapComponent : Component;
