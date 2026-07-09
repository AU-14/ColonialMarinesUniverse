using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using System;
using System.Collections.Generic;
using System.Text;

namespace Content.Shared._AU14.Chemistry.Research;

[RegisterComponent, AutoGenerateComponentState, NetworkedComponent]
public sealed partial class XRFScannerComponent : Component
{
    [AutoNetworkedField]
    public bool Processing = false;

    [AutoNetworkedField]
    public NetEntity LastUser = NetEntity.Invalid;

    [AutoNetworkedField]
    public int Sample = 0;

    //how long it takes to process a sample
    [DataField, AutoNetworkedField]
    public TimeSpan Inefficiency = TimeSpan.FromSeconds(10);
}
[Serializable, NetSerializable]
public enum XRFScannerVisuals
{
    State,
}

[Serializable, NetSerializable]
public enum XRFScannerState
{
    Scanner,
    Sample,
    Processing,
    Finished,
    Error,
    Failed,
}
