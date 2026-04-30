using Content.Shared._RMC14.Medical.Surgery.Tools;
using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.Items;

[RegisterComponent, NetworkedComponent]
public sealed partial class CMUBoneGraftComponent : Component, ICMSurgeryToolComponent
{
    public string ToolName => "a bone graft";
}
