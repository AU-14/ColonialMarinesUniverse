namespace Content.Shared._CMU14.Yautja;

/// <summary>
///     Marker components used by the shared surgery tool resolver. Dedicated
///     markers keep the three Medicomp stages from accepting one another's
///     tools.
/// </summary>
[RegisterComponent]
public sealed partial class CMUYautjaMedicompStabilizerToolComponent : Component;

[RegisterComponent]
public sealed partial class CMUYautjaMedicompHealingGunToolComponent : Component;

[RegisterComponent]
public sealed partial class CMUYautjaMedicompClampToolComponent : Component;

[RegisterComponent]
public sealed partial class CMUYautjaMedicompStabilizedComponent : Component;

[RegisterComponent]
public sealed partial class CMUYautjaMedicompTreatedComponent : Component;

[RegisterComponent]
public sealed partial class CMUYautjaMedicompSurgeryConditionComponent : Component;

[RegisterComponent]
public sealed partial class CMUYautjaMedicompStabilizeStepComponent : Component;

[RegisterComponent]
public sealed partial class CMUYautjaMedicompHealingGunStepComponent : Component;

[RegisterComponent]
public sealed partial class CMUYautjaMedicompClampStepComponent : Component;
