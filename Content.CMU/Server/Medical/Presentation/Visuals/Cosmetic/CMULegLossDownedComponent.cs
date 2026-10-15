namespace Content.Server.CMU14.Medical.Presentation.Visuals.Cosmetic;

// this mob lost the legs to stand on, so getting them back should stand it up again.
// part-added also fires for every limb at spawn, and that must not stand up a mob laid out on purpose
[RegisterComponent, Access(typeof(CMUSeveranceCosmeticSystem))]
public sealed partial class CMULegLossDownedComponent : Component;
