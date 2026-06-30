using Content.Shared._RMC14.Language.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Language.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class RemoveLanguageComponent : Component
{
    [DataField]
    public HashSet<ProtoId<LanguagePrototype>> Languages = [];

    [DataField]
    public bool RemoveSpoken = true;

    [DataField]
    public bool RemoveUnderstood = true;
}