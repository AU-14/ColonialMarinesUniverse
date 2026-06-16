using Content.Shared.Actions;

namespace Content.Shared._CMU14.Xenonids.Bull;

public sealed partial class CMUXenoBullChargeActionEvent : InstantActionEvent
{
    [DataField]
    public CMUXenoBullChargeMode Mode = CMUXenoBullChargeMode.Plow;
}
