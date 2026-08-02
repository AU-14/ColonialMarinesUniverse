using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Round.Objectives.Component;

[RegisterComponent, NetworkedComponent]
public sealed partial class ObjectivesConsoleComponent : Robust.Shared.GameObjects.Component
{
    [DataField(required: true)]
    public string Faction { get; private set; } = string.Empty;
}
