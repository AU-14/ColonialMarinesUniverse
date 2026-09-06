using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Dropship.Integrity;

[Serializable, NetSerializable]
public enum DropshipMalfunction : byte
{
    WeaponShort,
    PropulsionFault,
    ManeuveringThrusterFault,
    SensorArrayFault,
}

public static class DropshipMalfunctionData
{
    public static string GetAlertName(DropshipMalfunction malfunction)
    {
        return malfunction switch
        {
            DropshipMalfunction.WeaponShort => Loc.GetString("cmu-gunship-malfunction-weapon-short"),
            DropshipMalfunction.PropulsionFault => Loc.GetString("cmu-gunship-malfunction-propulsion"),
            DropshipMalfunction.ManeuveringThrusterFault => Loc.GetString("cmu-gunship-malfunction-thruster"),
            DropshipMalfunction.SensorArrayFault => Loc.GetString("cmu-gunship-malfunction-sensor"),
            _ => Loc.GetString("cmu-gunship-malfunction-unknown"),
        };
    }
}

[Serializable, NetSerializable]
public sealed partial class DropshipMalfunctionRepairDoAfterEvent : DoAfterEvent
{
    [DataField(required: true)]
    public DropshipMalfunction Malfunction;

    [DataField]
    public int Step;

    public DropshipMalfunctionRepairDoAfterEvent()
    {
    }

    public DropshipMalfunctionRepairDoAfterEvent(DropshipMalfunction malfunction, int step)
    {
        Malfunction = malfunction;
        Step = step;
    }

    public override DoAfterEvent Clone()
    {
        return new DropshipMalfunctionRepairDoAfterEvent(Malfunction, Step);
    }
}
