using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.util;
[Prototype]
public sealed partial class LorePrimerPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

        [DataField("KnowledgeLevelsGovforThreat", required: false)]
        public Dictionary<string, int>? KnowledgeLevels { get; private set; }
        // randomly select a level
        [DataField("planetText", required: false)]
        public string? PlanetText { get; private set; }


        [DataField("PlatoonInfo", required: false)]
        public string? PlatoonInfo { get; private set; }



        [DataField("threattext", required: false)]
        public string? ThreatText { get; private set; }

        /// <summary>
        /// CMU14 FLOW-611: the chance (0-1) that this round's marine knowledge line is drawn from one of
        /// <see cref="DecoyPrimers"/> instead, so the marines' intel points at the wrong threat.
        /// </summary>
        [DataField("decoyChance")]
        public float DecoyChance { get; private set; }

        /// <summary>
        /// CMU14 FLOW-611: primers whose knowledge levels may stand in for this one's.
        /// </summary>
        [DataField("decoyPrimers")]
        public List<ProtoId<LorePrimerPrototype>>? DecoyPrimers { get; private set; }


}

