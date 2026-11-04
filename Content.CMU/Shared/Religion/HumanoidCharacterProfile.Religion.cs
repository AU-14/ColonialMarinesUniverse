using Content.Shared.CMU14.Religion;
using Robust.Shared.Prototypes;

namespace Content.Shared.Preferences;

// CMU14: the character's religion, shown on records and needed to pray.
public sealed partial class HumanoidCharacterProfile
{
    [DataField]
    public ProtoId<CMUReligionPrototype> Religion { get; private set; } = CMUReligionPrototype.Atheist;

    public HumanoidCharacterProfile WithReligion(ProtoId<CMUReligionPrototype> religion)
    {
        return new(this) { Religion = religion };
    }
}
