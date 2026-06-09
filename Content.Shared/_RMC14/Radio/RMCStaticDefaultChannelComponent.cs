using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Radio;

/// <summary>
/// Prevents encryption key changes from replacing this headset's configured default channel.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class RMCStaticDefaultChannelComponent : Component;
