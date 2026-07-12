using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._AU14.Insurgency.Sapper;

/// <summary>
///     The Sapper's Workbench structure. Recipes use CM/RMC material prototype ids and display counts,
///     which the server converts to MaterialStorage's raw material units.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SapperWorkbenchComponent : Component
{
    // ---------------------------------------------------------------------
    // Weapon slot (force attach/detach, see SapperWorkbenchSystem).
    // ---------------------------------------------------------------------

    /// <summary>Container id the worked-on weapon sits in.</summary>
    [DataField]
    public string WeaponContainer = "workbench_weapon";

    /// <summary>
    ///     Recipes shown in the custom workbench crafting tab. Sheet costs are display counts;
    ///     item ingredients are consumed from loose items placed on or next to the bench.
    ///     Every craftable also demands one electronics item (any circuit board whatsoever).
    /// </summary>
    [DataField]
    public List<SapperWorkbenchRecipe> Recipes = new()
    {
        new("AU14SapperTripwireTrap", "Tripwire trap", 3f, new() { { "CMSteel", 6 }, { "RMCWood", 3 } },
            new() { Cable(10), AnyElectronics() }),
        new("AU14SapperShotgunTrap", "Shotgun trap", 4f, new() { { "CMSteel", 9 }, { "RMCWood", 6 } },
            new() { Buckshot(), AnyElectronics() }),
        new("AU14SapperIED", "Buried IED", 6f, new() { { "CMSteel", 12 }, { "CMPlasteel", 6 }, { "RMCPlastic", 3 } },
            new() { Ied(), AnyElectronics() }),
        new("AU14SapperSnareTrap", "Snare trap", 3f, new() { { "RMCWood", 9 }, { "RMCPlastic", 3 } },
            new() { Cable(10), AnyElectronics(), Handcuffs() }),
        new("AU14SapperAudioTrap", "Audio trap", 3f, new() { { "CMSteel", 6 }, { "RMCWood", 3 }, { "RMCPlastic", 3 } },
            new() { Cable(10), AnyElectronics() }),
        new("AU14CLFSpyCamera", "CLF spy camera", 4f, new() { { "CMSteel", 6 }, { "RMCPlastic", 6 } },
            new() { AnyElectronics(), PowerCell() }),
        new("AU14CLFCameraMonitor", "CLF camera monitor", 5f, new() { { "CMSteel", 9 }, { "RMCPlastic", 9 } },
            new() { AnyElectronics(), PowerCell() }),
        new("AU14CLFCameraRelay", "CLF camera relay", 5f, new() { { "CMSteel", 9 }, { "RMCPlastic", 6 } },
            new() { AnyElectronics(), PowerCell() }),
        // The siphon rig moved into the recipe list (was: apply a cable coil to the bench).
        new("AU14SapperSiphonRig", "Siphon rig", 6f, new() { { "CMSteel", 9 }, { "RMCPlastic", 6 } },
            new() { Cable(30), AnyElectronics(), PowerCell() }),
        // The "Switch" illegal fire-selector chip. Deliberately plasteel-hungry: it turns any rifle
        // into a bullet hose, so the cost is the whole balance lever.
        new("AU14AttachmentSwitch", "Switch (auto-sear chip)", 8f, new() { { "CMPlasteel", 40 } },
            new() { AnyElectronics() }),
    };

    // Ingredient shorthands so the recipe table above stays readable. IconPrototype is a
    // representative entity drawn in the recipe rows and the Loose Ingredients Help panel.
    private static SapperWorkbenchItemRequirement Cable(int count) =>
        new() { AnyCable = true, Count = count, Name = "any cable coil", IconPrototype = "RMCCableCoil" };

    private static SapperWorkbenchItemRequirement AnyElectronics() =>
        new() { AnyElectronics = true, Name = "any electronics", IconPrototype = "DoorElectronics" };

    private static SapperWorkbenchItemRequirement PowerCell() =>
        new() { AnyPowerCell = true, Name = "power cell", IconPrototype = "PowerCellMedium" };

    private static SapperWorkbenchItemRequirement Buckshot() =>
        new() { Prototype = "CMShellShotgunBuckshot", Count = 5, Name = "buckshot shells", IconPrototype = "CMShellShotgunBuckshot" };

    private static SapperWorkbenchItemRequirement Ied() =>
        new() { Prototype = "AU14IED", Name = "IED", IconPrototype = "AU14IED" };

    // Any cuffs at all: the Handcuffs tag covers handcuffs, zipties, cablecuffs, and the like.
    private static SapperWorkbenchItemRequirement Handcuffs() =>
        new() { Tag = "Handcuffs", Name = "any handcuffs", IconPrototype = "Handcuffs" };
}

/// <summary>
///     One loose-item ingredient of a workbench recipe. Exactly one of Prototype / StackType /
///     AnyElectronics should be set. Count is in shells for ammo handfuls, stack units for stacks,
///     and whole entities otherwise.
/// </summary>
[DataDefinition]
[Serializable, NetSerializable]
public sealed partial class SapperWorkbenchItemRequirement
{
    /// <summary>Exact entity prototype id to match.</summary>
    [DataField]
    public string? Prototype;

    /// <summary>Stack type to match, any stack size.</summary>
    [DataField]
    public string? StackType;

    /// <summary>Tag to match (e.g. CableCoil accepts every kind of cable coil).</summary>
    [DataField]
    public string? Tag;

    /// <summary>Matches ANY electronics item: machine boards, computer boards, door electronics.</summary>
    [DataField]
    public bool AnyElectronics;

    /// <summary>Matches any power cell.</summary>
    [DataField]
    public bool AnyPowerCell;

    /// <summary>
    ///     Matches ANY kind of cable coil: the CableCoil tag, or any stack whose type contains
    ///     "Cable" (Cable, CableApc/MV/HV, RMCCable...).
    /// </summary>
    [DataField]
    public bool AnyCable;

    /// <summary>Representative entity drawn next to the name in the recipe UI and the help panel.</summary>
    [DataField]
    public string? IconPrototype;

    /// <summary>How many units are needed (see class remarks for what a unit is).</summary>
    [DataField]
    public int Count = 1;

    /// <summary>Name shown in the recipe's cost line.</summary>
    [DataField(required: true)]
    public string Name = string.Empty;
}

[DataDefinition]
[Serializable, NetSerializable]
public sealed partial class SapperWorkbenchRecipe
{
    [DataField(required: true)]
    public EntProtoId Prototype;

    [DataField(required: true)]
    public string Name = string.Empty;

    [DataField]
    public float BuildTime = 3f;

    [DataField]
    public Dictionary<string, int> Materials = new();

    /// <summary>Loose-item ingredients consumed from items placed on or next to the bench.</summary>
    [DataField]
    public List<SapperWorkbenchItemRequirement> Items = new();

    public SapperWorkbenchRecipe()
    {
    }

    public SapperWorkbenchRecipe(EntProtoId prototype, string name, float buildTime, Dictionary<string, int> materials, List<SapperWorkbenchItemRequirement>? items = null)
    {
        Prototype = prototype;
        Name = name;
        BuildTime = buildTime;
        Materials = materials;
        Items = items ?? new();
    }
}

/// <summary>
///     A one-use kit item that a sapper unfolds into a Sapper's Workbench where they stand.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SapperWorkbenchKitComponent : Component
{
    /// <summary>The structure the kit unfolds into.</summary>
    [DataField]
    public EntProtoId WorkbenchPrototype = "AU14SapperWorkbench";

    /// <summary>How long unfolding takes.</summary>
    [DataField]
    public float DeployTime = 4f;
}

/// <summary>DoAfter for unfolding the workbench kit.</summary>
[Serializable, NetSerializable]
public sealed partial class SapperWorkbenchDeployDoAfterEvent : SimpleDoAfterEvent
{
}

/// <summary>DoAfter for fabricating one workbench recipe.</summary>
[Serializable, NetSerializable]
public sealed partial class SapperWorkbenchCraftDoAfterEvent : DoAfterEvent
{
    [DataField]
    public int RecipeIndex;

    private SapperWorkbenchCraftDoAfterEvent()
    {
    }

    public SapperWorkbenchCraftDoAfterEvent(int recipeIndex)
    {
        RecipeIndex = recipeIndex;
    }

    public override DoAfterEvent Clone() => this;
}
