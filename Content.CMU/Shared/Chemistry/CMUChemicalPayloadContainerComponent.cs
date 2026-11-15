using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Chemistry;

/// <summary>
/// Marks a container a chemical payload will take: vials, beakers, large beakers and high-capacity beakers.
/// Enforced by <see cref="CMUChemicalPayloadSystem"/>.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUChemicalPayloadContainerComponent : Component;
