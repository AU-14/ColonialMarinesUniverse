using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Forensics.Toolmarks;

/// <summary>
///     The unique wear on a tool's working edge. It's left as marks on whatever the tool pries or welds, and a forensic
///     scanner can match those marks back to the tool.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUToolSignatureComponent : Component
{
    [DataField]
    public string Signature = string.Empty;
}
