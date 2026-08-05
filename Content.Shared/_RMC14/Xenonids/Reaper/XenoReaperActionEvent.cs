using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Xenonids.Reaper;

public sealed partial class XenoFleshHarvestActionEvent : EntityTargetActionEvent;

[Serializable, NetSerializable]
public sealed partial class XenoFleshHarvestDoAfterEvent : SimpleDoAfterEvent;

public sealed partial class XenoRaptureActionEvent : EntityTargetActionEvent;

public sealed partial class XenoFleshBloomActionEvent : WorldTargetActionEvent;

public sealed partial class XenoReaperRedGasActionEvent : WorldTargetActionEvent;

[Serializable, NetSerializable]
public sealed partial class XenoReaperRedGasDoAfterEvent : SimpleDoAfterEvent
{
    [DataField]
    public NetCoordinates[] Path = Array.Empty<NetCoordinates>();

    [DataField]
    public int Step;

    public XenoReaperRedGasDoAfterEvent(NetCoordinates[] path, int step)
    {
        Path = path;
        Step = step;
    }
}

[Serializable, NetSerializable]
public sealed partial class XenoFleshBloomDoAfterEvent : SimpleDoAfterEvent
{
    [DataField]
    public NetCoordinates Coordinates;

    public XenoFleshBloomDoAfterEvent(NetCoordinates coordinates)
    {
        Coordinates = coordinates;
    }
}

public sealed partial class XenoCarrionMantleActionEvent : EntityTargetActionEvent;
