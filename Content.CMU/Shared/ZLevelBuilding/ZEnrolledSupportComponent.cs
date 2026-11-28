namespace Content.Shared.CMU14.ZLevelBuilding;

/// <summary>
/// Marks a structure that a non-player builder (an admin tool, the Grand Machine, a scripted event) enrolled in the
/// structural support graph through <c>ZLevelSupportSystem.Enroll</c>. It is never <c>PlayerBuilt</c>, yet it is a
/// built structure and not mapper-authored content, so <c>ZLevelSupportSystem.IsAuthoredCell</c> treats its cell
/// as non-authored and it stays collapsible on an authored level.
/// </summary>
[RegisterComponent]
public sealed partial class ZEnrolledSupportComponent : Component;
