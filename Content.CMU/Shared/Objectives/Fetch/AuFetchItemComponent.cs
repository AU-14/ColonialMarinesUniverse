using Content.Shared.AU14.Objectives.Fetch;
using Robust.Shared.GameStates;

namespace Content.Shared.AU14.Objectives;

[RegisterComponent, NetworkedComponent]
public sealed partial class AuFetchItemComponent : Component
{
    public bool Fetched = false;

    public FetchObjectiveComponent FetchObjective;
}
