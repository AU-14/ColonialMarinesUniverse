using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Gravity
{
    [RegisterComponent]
    [NetworkedComponent]
    public sealed partial class GravityComponent : Component
    {
        [DataField("gravityShakeSound")]
        public SoundSpecifier GravityShakeSound { get; set; } = new SoundPathSpecifier("/Audio/Effects/alert.ogg");

        [DataField("enabled")]
        public bool Enabled;

        /// <summary>
        /// Inherent gravity ensures GravitySystem won't change Enabled according to the gravity generators attached to this entity.
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("inherent")]
        public bool Inherent;
    }
}
