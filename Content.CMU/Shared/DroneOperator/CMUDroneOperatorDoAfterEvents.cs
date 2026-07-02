using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.DroneOperator;

[Serializable, NetSerializable]
public sealed partial class CMUDroneFrameOpenPortsDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class CMUDroneFrameInstallPartDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class CMUDroneFrameClampPartDoAfterEvent : DoAfterEvent
{
    [DataField]
    public CMUDroneAssemblyPartSlot Part;

    private CMUDroneFrameClampPartDoAfterEvent()
    {
    }

    public CMUDroneFrameClampPartDoAfterEvent(CMUDroneAssemblyPartSlot part)
    {
        Part = part;
    }

    public override DoAfterEvent Clone()
    {
        return new CMUDroneFrameClampPartDoAfterEvent(Part);
    }

    public override bool IsDuplicate(DoAfterEvent other)
    {
        return other is CMUDroneFrameClampPartDoAfterEvent clamp && clamp.Part == Part;
    }
}

[Serializable, NetSerializable]
public sealed partial class CMUDroneFrameWeldPartDoAfterEvent : DoAfterEvent
{
    [DataField]
    public CMUDroneAssemblyPartSlot Part;

    private CMUDroneFrameWeldPartDoAfterEvent()
    {
    }

    public CMUDroneFrameWeldPartDoAfterEvent(CMUDroneAssemblyPartSlot part)
    {
        Part = part;
    }

    public override DoAfterEvent Clone()
    {
        return new CMUDroneFrameWeldPartDoAfterEvent(Part);
    }

    public override bool IsDuplicate(DoAfterEvent other)
    {
        return other is CMUDroneFrameWeldPartDoAfterEvent weld && weld.Part == Part;
    }
}

[Serializable, NetSerializable]
public sealed partial class CMUDroneFrameActivateDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class CMUDroneModuleInstallDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class CMUDroneModuleUninstallDoAfterEvent : SimpleDoAfterEvent;
