using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Radio;

/// <summary>
/// Opts a handheld radio into the on / listen-only / off interaction cycle.
/// State remains in its networked microphone and speaker components.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMURadioListenOnlyComponent : Component;
