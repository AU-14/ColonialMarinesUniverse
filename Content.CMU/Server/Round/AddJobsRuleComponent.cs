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

        [DataField("addToShip")]
        public bool AddToShip { get; set; } = false;

        /// <summary>
        /// If set, designates which ship(s) to add jobs to: "govfor", "opfor", or null/empty for all.
        /// </summary>
        [DataField("shipFaction")]
        public string? ShipFaction { get; set; } = null;

    }

}
