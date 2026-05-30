using Content.Shared.DeviceLinking;
using Content.Shared.Inventory;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.Explosion.Components
{
    /// <summary>
    /// Sends a trigger when signal is received.
    /// </summary>
    [RegisterComponent]
    public sealed partial class TimerStartOnSignalComponent : Component
    {
        [DataField("port", customTypeSerializer: typeof(PrototypeIdSerializer<SinkPortPrototype>))]
        public string Port = "Timer";

        /// <summary>
        ///     If set, the timer can only be started while this entity is equipped in one of these slots.
        /// </summary>
        [DataField]
        public SlotFlags RequiredWornSlots = SlotFlags.NONE;
    }
}
