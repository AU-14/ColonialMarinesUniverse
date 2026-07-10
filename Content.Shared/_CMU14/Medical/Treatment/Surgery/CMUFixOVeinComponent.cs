using Content.Shared._RMC14.Medical.Surgery.Tools;
using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.Treatment.Surgery;

[RegisterComponent, NetworkedComponent]
public sealed partial class CMUFixOVeinComponent : Component, ICMSurgeryToolComponent
{
    public string ToolName => "a Fix-O-Vein";
}
