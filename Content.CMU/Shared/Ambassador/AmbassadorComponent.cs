namespace Content.Shared.CMU14.Ambassador;

/// <summary>
/// Marks a foreign envoy. They ride along with a side for IFF, sentries and the tacmap
/// but they're not in its chain of command, so command tablet / ARES announcements skip them.
/// </summary>
[RegisterComponent]
public sealed partial class AmbassadorComponent : Component;
