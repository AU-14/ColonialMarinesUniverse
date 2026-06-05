using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Medical.Surgery.Tools;

[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedCMSurgerySystem))]
public sealed partial class CMCircularSawComponent : Component, ICMSurgeryToolComponent
{
    public string ToolName => "a circular saw";
}
