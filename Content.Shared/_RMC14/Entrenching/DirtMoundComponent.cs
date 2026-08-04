using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Entrenching;

/// <summary>
///     Marker for structures built by <see cref="BarricadeSystem"/>'s dirt mound building that can be
///     dismantled back into nothing with an entrenching tool.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(BarricadeSystem))]
public sealed partial class DirtMoundComponent : Component;
