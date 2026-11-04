using Robust.Shared.Serialization;

namespace Content.Shared.Sandbox
{
    public abstract partial class SharedSandboxSystem : EntitySystem
    {
        [Serializable, NetSerializable]
        protected sealed partial class MsgSandboxStatus : EntityEventArgs
        {
            public bool SandboxAllowed { get; set; }
        }

        [Serializable, NetSerializable]
        protected sealed partial class MsgSandboxRespawn : EntityEventArgs {}

        [Serializable, NetSerializable]
        protected sealed partial class MsgSandboxGiveAccess : EntityEventArgs {}

        [Serializable, NetSerializable]
        protected sealed partial class MsgSandboxGiveAghost : EntityEventArgs {}

        [Serializable, NetSerializable]
        protected sealed partial class MsgSandboxSuicide : EntityEventArgs {}

        [Serializable, NetSerializable]
        protected sealed partial class MsgSandboxThermalVision : EntityEventArgs {}
    }
}
