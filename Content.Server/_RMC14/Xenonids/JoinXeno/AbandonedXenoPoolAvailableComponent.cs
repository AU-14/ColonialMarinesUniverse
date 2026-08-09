using Content.Server._RMC14.Xenonids;

namespace Content.Server._RMC14.Xenonids.JoinXeno;

[RegisterComponent]
[Access(typeof(XenoRoleSystem), typeof(LarvaPoolSystem))]
public sealed partial class AbandonedXenoPoolAvailableComponent : Component;
