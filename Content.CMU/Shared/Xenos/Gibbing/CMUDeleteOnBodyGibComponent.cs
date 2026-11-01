namespace Content.Shared.CMU14.Xenos.Gibbing;

// put on a body part that should vanish with the body instead of flying out as a giblet.
// nubody gave xenos a real RMCHeadXeno root part and GibBody drops every part, so every
// gibbed xeno (75% of dead huggers) left a xeno head on the floor
[RegisterComponent]
public sealed partial class CMUDeleteOnBodyGibComponent : Component;
