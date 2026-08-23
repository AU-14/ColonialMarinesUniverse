using Content.Shared._CMU14.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._AU14.Chemistry.Research;

/// <summary>
/// Admin-only console for issuing generated chemical contracts and materializing their reagent.
/// Access is enforced by the server system; the prototype remains visible in the admin spawn menu.
/// </summary>
[RegisterComponent]
public sealed partial class AdminChemicalContractConsoleComponent : Component
{
    [DataField]
    // Keep the explicit List initializer: a collection expression emits CollectionsMarshal.SetCount,
    // which is rejected by Robust's content sandbox during module loading.
    public List<ProtoId<ReagentPropertyPrototype>> AvailableProperties = new()
    {
        "Antitoxic",
        "Anticorrosive",
        "Neogenetic",
        "Repairing",
        "Hemogenic",
        "Yautjahemogenic",
        "Hemostatic",
        "Nervestimulating",
        "Musclestimulating",
        "Painkilling",
        "Hepatopeutic",
        "Nephropeutic",
        "Pneumopeutic",
        "Oculopeutic",
        "Cardiopeutic",
        "Neuropeutic",
        "Bonemending",
        "Fluxing",
        "Neurocryogenic",
        "Antiparasitic",
        "Electrogenetic",
        "Defibrillating",
        "Hyperdensificating",
        "Neuroshielding",
        "Antiaddictive",
    };

    [DataField]
    public FixedPoint2 OutputAmount = 30;

    [ViewVariables(VVAccess.ReadOnly)]
    public Dictionary<string, int> SelectedProperties = [];

    [ViewVariables(VVAccess.ReadOnly)]
    public string Status = string.Empty;
}

/// <summary>
/// Marks a contract printed by an admin chemical contract console as safe to materialize.
/// </summary>
[RegisterComponent]
public sealed partial class AdminChemicalContractPaperComponent : Component;
