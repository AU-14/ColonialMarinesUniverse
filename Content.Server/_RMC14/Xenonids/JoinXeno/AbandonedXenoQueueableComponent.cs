using Content.Server._RMC14.Xenonids;

namespace Content.Server._RMC14.Xenonids.JoinXeno;

[RegisterComponent]
[Access(typeof(XenoRoleSystem), typeof(LarvaQueueSystem))]
public sealed partial class AbandonedXenoQueueableComponent : Component;
