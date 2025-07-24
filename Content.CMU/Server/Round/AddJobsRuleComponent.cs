using System.Collections.Generic;
using Content.Server.GameTicking.Rules.Components;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.List;

namespace Content.Server.AU14.Round
{

    [RegisterComponent]

    public sealed partial class AddJobsRuleComponent : Component
    {
        [DataField("jobs")]
        public Dictionary<ProtoId<JobPrototype>, int>? Jobs { get; set; }


    }

}
