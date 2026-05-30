using Content.Server.Power.NodeGroups;

namespace Content.Server.Power.Components
{
    [RegisterComponent]
    [ComponentProtoName("PowerProvider")]
    public sealed partial class ApcPowerProviderComponent : BaseApcNetComponent
    {
        [ViewVariables] public List<ApcPowerReceiverComponent> LinkedReceivers { get; } = new();

        public void AddReceiver(ApcPowerReceiverComponent receiver)
        {
            LinkedReceivers.Add(receiver);
            receiver.NetworkLoad.LinkedNetwork = default;

            Net?.QueueNetworkReconnect();
        }

        public void RemoveReceiver(ApcPowerReceiverComponent receiver)
        {
            LinkedReceivers.Remove(receiver);
            receiver.NetworkLoad.LinkedNetwork = default;

            Net?.QueueNetworkReconnect();
        }

        protected override void AddSelfToNet(EntityUid uid, IApcNet apcNet)
        {
            apcNet.AddPowerProvider(uid, this);
        }

        protected override void RemoveSelfFromNet(EntityUid uid, IApcNet apcNet)
        {
            apcNet.RemovePowerProvider(uid, this);
        }
    }
}
