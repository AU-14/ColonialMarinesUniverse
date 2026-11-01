namespace Content.Server.CMU14.Radio;

/// <summary>
/// Put on a radio source for the length of a send to make it an automated, always-clear transmission:
/// no comms tower, relay range, jamming or garbling applies. Deaf listeners still can't hear it.
/// </summary>
[RegisterComponent]
public sealed partial class CMUClearRadioTransmissionComponent : Component;
