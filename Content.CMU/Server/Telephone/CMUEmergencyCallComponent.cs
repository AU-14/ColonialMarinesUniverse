using Content.Shared.CMU14.Telephone;

namespace Content.Server.CMU14.Telephone;

/// <summary>
/// A rotary phone currently on a call with 911 dispatch.
/// </summary>
[RegisterComponent]
public sealed partial class CMUEmergencyCallComponent : Component
{
    [DataField]
    public CMUEmergencyService Service;

    [DataField]
    public EntityUid Caller;

    /// <summary>
    /// 0: location question, 1: details question, 2: dispatch confirmation, 3: finished.
    /// </summary>
    [DataField]
    public int Stage;

    [DataField]
    public TimeSpan? NextPromptAt;

    [DataField]
    public bool AwaitingAnswer;

    [DataField]
    public string Location = string.Empty;

    [DataField]
    public string Details = string.Empty;
}
