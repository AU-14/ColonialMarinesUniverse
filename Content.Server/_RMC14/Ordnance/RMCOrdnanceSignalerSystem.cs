using Content.Server.DeviceNetwork.Systems;
using Content.Shared._RMC14.Ordnance.Signaler;
using Content.Shared.DeviceLinking;
using Content.Shared.DeviceNetwork;
using Content.Shared.DeviceNetwork.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Timing;
using Content.Shared.UserInterface;

namespace Content.Server._RMC14.Ordnance.Signaler;

public sealed class RMCOrdnanceSignalerSystem : EntitySystem
{
    [Dependency] private readonly DeviceNetworkSystem _deviceNetwork = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly UseDelaySystem _useDelay = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<RMCOrdnanceSignalerComponent, UseInHandEvent>(OnUseInHand);

        Subs.BuiEvents<RMCOrdnanceSignalerComponent>(OrdnanceSignalerUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnUiOpened);
            subs.Event<SelectOrdnanceSignalerFrequencyMessage>(OnFrequencyChange);
        });
    }

    private void OnUiOpened(Entity<RMCOrdnanceSignalerComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private void OnFrequencyChange(Entity<RMCOrdnanceSignalerComponent> ent, ref SelectOrdnanceSignalerFrequencyMessage args)
    {
        if (!TryComp<DeviceNetworkComponent>(ent, out var net))
            return;

        if (args.Frequency != 0)
        {
            _deviceNetwork.SetReceiveFrequency(ent, args.Frequency, net);
            _deviceNetwork.SetTransmitFrequency(ent, args.Frequency, net);
        }

        UpdateUi(ent, net);
    }

    private void OnUseInHand(Entity<RMCOrdnanceSignalerComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || !TryComp<DeviceNetworkComponent>(ent, out var net))
            return;

        if (TryComp<UseDelayComponent>(ent, out var useDelay) &&
            !_useDelay.TryResetDelay((ent, useDelay), true))
        {
            return;
        }

        var payload = new NetworkPayload
        {
            [SharedDeviceLinkSystem.InvokedPort] = "Trigger"
        };

        _deviceNetwork.QueuePacket(ent, null, payload, device: net);
        args.Handled = true;
    }

    private void UpdateUi(Entity<RMCOrdnanceSignalerComponent> ent, DeviceNetworkComponent? net = null)
    {
        if (!Resolve(ent, ref net, false))
            return;

        _ui.SetUiState(ent.Owner, OrdnanceSignalerUiKey.Key, new OrdnanceSignalerBoundUIState((int)(net.ReceiveFrequency ?? 0)));
    }
}
