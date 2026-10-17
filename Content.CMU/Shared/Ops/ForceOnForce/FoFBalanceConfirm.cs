using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Ops.ForceOnForce;

/// <summary>
/// Sent from server to client when a Force on Force late join would place the player on the
/// opposite side of their pick. The client asks for confirmation before the join is retried.
/// </summary>
[Serializable, NetSerializable]
public sealed class FoFBalanceConfirmEvent(int govfor, int opfor, int maxGap) : EntityEventArgs
{
    public readonly int Govfor = govfor;
    public readonly int Opfor = opfor;
    public readonly int MaxGap = maxGap;
}

/// <summary>
/// Sent from client to server to accept being placed on the opposite side.
/// </summary>
public sealed class FoFBalanceConfirmMessage : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer) { }
    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer) { }
}
