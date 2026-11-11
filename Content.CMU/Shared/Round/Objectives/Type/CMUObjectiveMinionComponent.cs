namespace Content.Shared.CMU14.Round.Objectives.Type;

// throwaway spawn that kill objectives with ignoreMinions skip. xenos with contributesToVictory: false
// (lesser drones, swarm lessers, huggers) already count as minions, this is for the ones that don't
// carry that flag, like neomorph poppers
[RegisterComponent]
public sealed partial class CMUObjectiveMinionComponent : Robust.Shared.GameObjects.Component;
