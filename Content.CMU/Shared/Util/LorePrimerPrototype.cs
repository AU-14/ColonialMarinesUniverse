using Robust.Shared.Prototypes;

namespace Content.Shared.AU14.util;
[Prototype("lorePrimer")]
public sealed class LorePrimerPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

        [DataField("KnowledgeLevelsGovforThreat", required: false)]
        public  Dictionary<string, int> KnowledgeLevels { get; private set; } = new();
        // randomly select a level
        [DataField("planetText", required: false)]
        public  string PlanetText { get; private set; } = default!;


        [DataField("PlatoonInfo", required: false)]
        public  string PlatoonInfo { get; private set; } = default!;



        [DataField("threattext", required: false)]
        public  string ThreatText { get; private set; } = default!;


}
