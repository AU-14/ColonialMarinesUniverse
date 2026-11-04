using Content.Shared.Actions;

namespace Content.Shared.CMU14.Threats.Mobs.Xeno.Caste.Bull;

public sealed partial class CMUXenoBullChargeActionEvent : InstantActionEvent
{
    [DataField]
    public CMUXenoBullChargeMode Mode = CMUXenoBullChargeMode.Plow;
}
