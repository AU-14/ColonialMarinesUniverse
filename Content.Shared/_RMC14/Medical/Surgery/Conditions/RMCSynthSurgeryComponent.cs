using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Medical.Surgery.Conditions;

/// <summary>
///     Marker on a surgery prototype declaring it as the synth-only flow.
///     Synth bodies hide non-marked surgeries; non-synth bodies hide
///     marked ones.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedCMSurgerySystem))]
public sealed partial class RMCSynthSurgeryComponent : Component;
