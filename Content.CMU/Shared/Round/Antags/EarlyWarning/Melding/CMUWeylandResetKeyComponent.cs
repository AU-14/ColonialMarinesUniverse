using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Round.Antags.EarlyWarning.Melding;

/// <summary>
/// The Weyland-tier reset key, issued to platoon command. Any reset key now frees a River-Melded thrall
/// (EWJoeMeldingSystem checks SynthRepairer); this marker identifies the issued model for loadouts and tests.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUWeylandResetKeyComponent : Component;
