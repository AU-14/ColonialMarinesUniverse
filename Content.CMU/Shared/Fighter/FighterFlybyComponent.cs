using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Fighter;

/// <summary>Presentation on the battlefield; flight and collision remain route based.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FighterFlybyComponent : Component
{
    [DataField, AutoNetworkedField] public float Height;
    [DataField, AutoNetworkedField] public bool Crashing;
}

/// <summary>A fixed release point for flares and airbursts viewed from the ground.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FighterAirBurstComponent : Component;
