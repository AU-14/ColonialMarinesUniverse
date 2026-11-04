using System.Numerics;
using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.VoiceChat;

/// <summary>
/// How a voice frame reached the listener, which decides how the client plays it.
/// </summary>
public enum CMUVoiceRoute : byte
{
    /// <summary>Heard in the world from the speaker, or through a floor opening.</summary>
    Direct,

    /// <summary>Over a headset channel: in the ear, through the radio filter.</summary>
    Radio,

    /// <summary>A live announcement from a console: in the ear, through the PA filter.</summary>
    Announcement,

    /// <summary>Over a connected telephone call: in the ear, through the phone line filter.</summary>
    Telephone,
}

public sealed class CMUMsgVoiceFrame : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Core;
    public override NetDeliveryMethod DeliveryMethod => NetDeliveryMethod.Unreliable;

    public NetEntity Source;
    public ushort Speaker;
    public ushort Sequence;
    public byte Flags;
    public byte[] Payload = Array.Empty<byte>();

    /// <summary>
    /// Set when the speaker is on an adjacent z-level: the voice is played from the opening
    /// at <see cref="RelayPosition"/> on the listener's map instead of from <see cref="Source"/>.
    /// </summary>
    public bool Relayed;

    public Vector2 RelayPosition;

    /// <summary>
    /// Distance in tiles the voice already travelled from the speaker to the opening.
    /// </summary>
    public float RelayDistance;

    public CMUVoiceRoute Route;

    /// <summary>
    /// Distance in tiles this frame carries in the world. Whispering and shouting change it per frame.
    /// </summary>
    public float Range;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        Source = buffer.ReadNetEntity();
        Speaker = buffer.ReadUInt16();
        Sequence = buffer.ReadUInt16();
        Flags = buffer.ReadByte();
        var length = buffer.ReadVariableInt32();
        Payload = length is > 0 and <= CMUVoiceCodec.FrameBytes ? buffer.ReadBytes(length) : Array.Empty<byte>();
        Route = (CMUVoiceRoute) buffer.ReadByte();
        Range = buffer.ReadFloat();
        Relayed = buffer.ReadBoolean();
        if (!Relayed)
            return;

        RelayPosition = buffer.ReadVector2();
        RelayDistance = buffer.ReadFloat();
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(Source);
        buffer.Write(Speaker);
        buffer.Write(Sequence);
        buffer.Write(Flags);
        buffer.WriteVariableInt32(Payload.Length);
        buffer.Write(Payload);
        buffer.Write((byte) Route);
        buffer.Write(Range);
        buffer.Write(Relayed);
        if (!Relayed)
            return;

        buffer.Write(RelayPosition);
        buffer.Write(RelayDistance);
    }
}

public sealed class CMUMsgVoiceLinkRequest : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
    }
}

public sealed class CMUMsgVoiceLink : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public string Url = string.Empty;
    public string Token = string.Empty;
    public int StatusPort;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        Url = buffer.ReadString();
        Token = buffer.ReadString();
        StatusPort = buffer.ReadInt32();
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(Url);
        buffer.Write(Token);
        buffer.Write(StatusPort);
    }
}

public sealed class CMUMsgVoiceSettings : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public bool HearSelf;
    public bool Receive = true;

    /// <summary>
    /// Radio channel IDs the player muted for voice in their headset window.
    /// </summary>
    public List<string> MutedChannels = new();

    private const int MaxMutedChannels = 64;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        HearSelf = buffer.ReadBoolean();
        Receive = buffer.ReadBoolean();

        var count = Math.Min(buffer.ReadVariableInt32(), MaxMutedChannels);
        MutedChannels = new List<string>(count);
        for (var i = 0; i < count; i++)
        {
            MutedChannels.Add(buffer.ReadString());
        }
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(HearSelf);
        buffer.Write(Receive);

        var count = Math.Min(MutedChannels.Count, MaxMutedChannels);
        buffer.WriteVariableInt32(count);
        for (var i = 0; i < count; i++)
        {
            buffer.Write(MutedChannels[i]);
        }
    }
}

/// <summary>
/// Tells the speaker their radio push-to-talk went live or was released, so their client plays the squelch clicks.
/// </summary>
public sealed class CMUMsgVoiceRadioKey : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public bool Transmitting;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        Transmitting = buffer.ReadBoolean();
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(Transmitting);
    }
}

/// <summary>
/// A talk key went down or up on the client. Sent directly rather than through entity input
/// so push-to-talk keeps working whatever the player is attached to.
/// </summary>
public sealed class CMUMsgVoicePushToTalk : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public bool Pressed;
    public bool Radio;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        Pressed = buffer.ReadBoolean();
        Radio = buffer.ReadBoolean();
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(Pressed);
        buffer.Write(Radio);
    }
}

/// <summary>
/// What a speaker's own talking currently looks like, for the "You" row of the speaker list.
/// </summary>
[Flags]
public enum CMUVoiceSelfFlags : byte
{
    None = 0,
    Radio = 1 << 0,
    Announcement = 1 << 1,
    Shout = 1 << 2,
    Whisper = 1 << 3,
    Blocked = 1 << 4,
    Telephone = 1 << 5,
}

/// <summary>
/// Sent to a speaker for each frame they transmit: their speaker id, mic level and how they are being heard.
/// </summary>
public sealed class CMUMsgVoiceSelf : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Core;
    public override NetDeliveryMethod DeliveryMethod => NetDeliveryMethod.Unreliable;

    public ushort Speaker;
    public byte Level;
    public CMUVoiceSelfFlags Flags;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        Speaker = buffer.ReadUInt16();
        Level = buffer.ReadByte();
        Flags = (CMUVoiceSelfFlags) buffer.ReadByte();
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(Speaker);
        buffer.Write(Level);
        buffer.Write((byte) Flags);
    }
}

/// <summary>
/// Names a speaker id for listeners, once per transmission, so the speaker list can label who is talking.
/// </summary>
public sealed class CMUMsgVoiceSpeakerInfo : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public ushort Speaker;
    public string Name = string.Empty;

    /// <summary>
    /// The radio channel ID the speaker is transmitting on, or empty when not on the radio.
    /// </summary>
    public string Channel = string.Empty;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        Speaker = buffer.ReadUInt16();
        Name = buffer.ReadString();
        Channel = buffer.ReadString();
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(Speaker);
        buffer.Write(Name);
        buffer.Write(Channel);
    }
}
