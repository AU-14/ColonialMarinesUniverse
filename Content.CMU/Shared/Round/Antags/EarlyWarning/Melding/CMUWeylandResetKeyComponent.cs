using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Round.Antags.EarlyWarning.Melding;

/// <summary>
/// The Weyland-tier reset key: the only reset key that frees a River-Melded thrall (D4). The marker
/// is what the cure checks, because every reset key (and the Working Joe reboot verb, which zaps with a
/// temporary Seegson key) runs the same defibrillator path.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUWeylandResetKeyComponent : Component;
