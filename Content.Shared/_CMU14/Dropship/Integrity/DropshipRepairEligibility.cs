using System;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Dropship.Integrity;

[Serializable, NetSerializable]
public enum DropshipFlightState : byte
{
    Landed,
    Hovering,
    ChangingAltitude,
    Ftl,
    Crashing,
    Wrecked,
}

public static class DropshipRepairEligibility
{
    public static DropshipFlightState ResolveState(
        bool hovering,
        bool changingAltitude,
        bool ftlActive,
        bool crashing,
        bool wrecked)
    {
        if (wrecked)
            return DropshipFlightState.Wrecked;
        if (crashing)
            return DropshipFlightState.Crashing;
        if (ftlActive)
            return DropshipFlightState.Ftl;
        if (changingAltitude)
            return DropshipFlightState.ChangingAltitude;
        return hovering ? DropshipFlightState.Hovering : DropshipFlightState.Landed;
    }

    public static bool CanRepair(DropshipFlightState state)
    {
        return state == DropshipFlightState.Landed;
    }
}
