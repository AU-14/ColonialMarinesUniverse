namespace Content.Shared._AU14.Radio;

// command authority over COMSEC: only holders may order a recrypto that
// supersedes every older fill card of their faction
[RegisterComponent]
public sealed partial class ANPRCCryptoAuthorityComponent : Component;
