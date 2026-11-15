namespace Content.Server.CMU14.Threats;

/// <summary>Prevents ghost-role registration while a force body awaits its reserved assignment.</summary>
[RegisterComponent]
public sealed partial class PendingForceRoleComponent : Component;
