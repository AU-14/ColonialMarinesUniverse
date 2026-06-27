using Robust.Shared.GameStates;
using System;
using System.Collections.Generic;
using System.Text;

namespace Content.Shared._AU14.Chemistry.Research;

[RegisterComponent, AutoGenerateComponentState, NetworkedComponent, AutoGenerateComponentPause]
public sealed partial class XRFScannerComponent : Component
{
    [AutoNetworkedField]
    public bool Processing = false;

    [AutoNetworkedField]
    public NetEntity LastUser = NetEntity.Invalid;

    [AutoNetworkedField]
    public int State = 0;

    //how long it takes to process a sample
    [DataField, AutoNetworkedField]
    public TimeSpan Inefficiency = TimeSpan.FromSeconds(6);
}
