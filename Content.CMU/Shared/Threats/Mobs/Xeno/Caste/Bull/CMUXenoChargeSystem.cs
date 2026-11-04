// ReSharper disable CheckNamespace

namespace Content.Shared._RMC14.Xenonids.Charge;

public sealed partial class XenoChargeSystem
{
    public void CMUResetToggleCharging(Entity<ActiveXenoToggleChargingComponent> xeno, bool resetInput = true)
    {
        ResetCharging(xeno, resetInput);
    }

    public void CMUEndToggleCharging(Entity<ActiveXenoToggleChargingComponent> xeno, bool resetInput = true)
    {
        ResetCharging(xeno, resetInput);
        RemCompDeferred<ActiveXenoToggleChargingComponent>(xeno.Owner);
    }
}
