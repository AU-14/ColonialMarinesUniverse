using Content.Shared.NPC.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.FactionRoster;

[RegisterComponent]
public sealed partial class FactionRosterConsoleComponent : Component
{
    [DataField(required: true)]
    public ProtoId<NpcFactionPrototype> Faction;
}
