namespace Content.Shared.CMU14.Threats.GrandMachine;

/// <summary>
/// Lore marker for entities corrupted by the Grand Machine (the Early Warning Joe and River-Melded
/// thralls). Deliberately distinct from the future GrandMachineComponent, which will drive the Grand
/// Machine threat's kill rule, turret friend lists and victory exclusions; touched entities must not
/// count as threat units. Not networked: it would reveal the antag to every client.
/// </summary>
[RegisterComponent]
public sealed partial class GrandMachineTouchedComponent : Component;
