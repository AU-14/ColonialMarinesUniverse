// Content.Shared/CMU14/ColonyEconomy/SubmissionStorageComponent.cs
using Content.Shared.Whitelist;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.ColonyEconomy;

// a point buys every commodity in its categories plus the ones listed by hand.
// prices live on the commodities so the same item pays the same everywhere
[RegisterComponent, NetworkedComponent]
public sealed partial class SubmissionStorageComponent : Component
{
    [DataField, ViewVariables]
    public List<ProtoId<CMUCommodityCategoryPrototype>> Categories = new();

    [DataField, ViewVariables]
    public List<ProtoId<CMUCommodityPrototype>> Commodities = new();

    // turned away even when a commodity matches. thrown items skip the disposal unit's own
    // lists, so this is checked in the insert veto instead
    [DataField]
    public EntityWhitelist? Blacklist;
}
