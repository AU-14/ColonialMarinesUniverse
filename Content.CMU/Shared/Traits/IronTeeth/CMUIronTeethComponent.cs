using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Traits.IronTeeth;

/// <summary>
///     Whatever this person holds in their teeth stays there when they go down.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUIronTeethComponent : Component;
