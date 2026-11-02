// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Server.Power.Components;

namespace Content.Server.Power.EntitySystems;

// The part of the extension cables for the cables run by hand: a receiver is given the provider its cable goes to, wherever it stands, and is
// given back to the nearest one when the cable is cut.
public sealed partial class ExtensionCableSystem
{
    public bool IsConnectedDirect(Entity<ExtensionCableReceiverComponent> receiver, EntityUid provider)
    {
        return receiver.Comp.Provider is { } current && current.Owner == provider;
    }

    public void ConnectDirect(Entity<ExtensionCableReceiverComponent> receiver, Entity<ExtensionCableProviderComponent> provider)
    {
        if (!receiver.Comp.Connectable || !provider.Comp.Connectable)
            return;

        if (receiver.Comp.Provider is { } current)
        {
            if (current.Owner == provider.Owner)
                return;

            RaiseLocalEvent(receiver, new ProviderDisconnectedEvent(current), broadcast: false);
            RaiseLocalEvent(current, new ReceiverDisconnectedEvent(receiver), broadcast: false);
            current.Comp.LinkedReceivers.Remove(receiver);
        }

        receiver.Comp.Provider = provider;
        provider.Comp.LinkedReceivers.Add(receiver);
        RaiseLocalEvent(receiver, new ProviderConnectedEvent(provider), broadcast: false);
        RaiseLocalEvent(provider, new ReceiverConnectedEvent(receiver), broadcast: false);
    }

    // The cable is cut: the receiver lets its provider go and takes the nearest one in its reach, if there is.
    public void DisconnectDirect(Entity<ExtensionCableReceiverComponent> receiver)
    {
        if (receiver.Comp.Provider is { } current)
        {
            RaiseLocalEvent(receiver, new ProviderDisconnectedEvent(current), broadcast: false);
            RaiseLocalEvent(current, new ReceiverDisconnectedEvent(receiver), broadcast: false);
            current.Comp.LinkedReceivers.Remove(receiver);
            receiver.Comp.Provider = null;
        }

        TryFindAndSetProvider(receiver);
    }
}
