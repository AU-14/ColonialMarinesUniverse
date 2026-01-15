using System.Runtime.Serialization;
using Robust.Shared.Prototypes;

namespace Content.Shared.AU14.Threats;


[Prototype]
public sealed partial class PartySpawnPrototype : IPrototype
{

    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField("gruntsToSpawn", required: false)]
    public Dictionary<string, int> GruntsToSpawn { get; private set; } = new Dictionary<string, int>();

    [DataField("leadersToSpawn", required: true)]
    public Dictionary<string, int> LeadersToSpawn { get; private set; } = new Dictionary<string, int>();

    [DataField("entsToSpsawn", required: false)]
    public Dictionary<string, int> entitiestospawn { get; private set; } = new Dictionary<string, int>();


    [DataField("spawnTogether", required: false),]
    public bool SpawnTogether { get; private set; } =  true;

    [DataField("scalewithpop", required: false)]
    public bool ScalewithPop { get; private set; } = false;
// does nothing yet
    [DataField("Markers", required: false)]
    public Dictionary<ThreatMarkerType, string> Markers { get; private set; } = new Dictionary<ThreatMarkerType, string>();
    // threatmarkertype, custommarkerid. if blank use generic

}
