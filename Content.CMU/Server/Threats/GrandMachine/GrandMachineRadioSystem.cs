using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Threats.GrandMachine;

/// <summary>
/// The Grand Machine's radio channel, shared by Early Warning Joes, their thralls and hijacked Working Joes.
/// Same shape as the accessory headsets: the channel is granted onto whatever radio components the body has, and
/// on revoke a component is dropped only once it carries no channels at all, so a Working Joe keeps its own
/// intrinsic radio. Callers track whether they granted the channel; this system holds no state.
/// </summary>
public sealed partial class GrandMachineRadioSystem : EntitySystem
{
    /// <summary>The machine channel (EWJoe.yml).</summary>
    public static readonly ProtoId<RadioChannelPrototype> Channel = "CMUGrandMachine";

    /// <summary>Grants the Grand Machine channel.</summary>
    public void GrantChannel(EntityUid uid)
    {
        GrantChannel(uid, Channel);
    }

    /// <summary>Revokes the Grand Machine channel.</summary>
    public void RevokeChannel(EntityUid uid)
    {
        RevokeChannel(uid, Channel);
    }

    /// <summary>Whether the body can both hear and speak on <see cref="Channel"/>.</summary>
    public bool HasChannel(EntityUid uid)
    {
        return HasChannel(uid, Channel);
    }

    public void GrantChannel(EntityUid uid, ProtoId<RadioChannelPrototype> channel)
    {
        EnsureComp<ActiveRadioComponent>(uid).Channels.Add(channel);
        EnsureComp<IntrinsicRadioReceiverComponent>(uid);
        EnsureComp<IntrinsicRadioTransmitterComponent>(uid).Channels.Add(channel);
    }

    public void RevokeChannel(EntityUid uid, ProtoId<RadioChannelPrototype> channel)
    {
        var anyChannels = false;
        if (TryComp(uid, out ActiveRadioComponent? active))
        {
            active.Channels.Remove(channel);
            anyChannels = active.Channels.Count > 0;
            if (!anyChannels)
                RemCompDeferred<ActiveRadioComponent>(uid);
        }

        if (TryComp(uid, out IntrinsicRadioTransmitterComponent? transmitter))
        {
            transmitter.Channels.Remove(channel);
            if (transmitter.Channels.Count == 0)
                RemCompDeferred<IntrinsicRadioTransmitterComponent>(uid);
        }

        // the receiver is only useful with an active radio; a headset re-adds it when worn
        if (!anyChannels)
            RemCompDeferred<IntrinsicRadioReceiverComponent>(uid);
    }

    public bool HasChannel(EntityUid uid, ProtoId<RadioChannelPrototype> channel)
    {
        return TryComp(uid, out ActiveRadioComponent? active) && active.Channels.Contains(channel)
            && TryComp(uid, out IntrinsicRadioTransmitterComponent? transmitter) && transmitter.Channels.Contains(channel);
    }
}
