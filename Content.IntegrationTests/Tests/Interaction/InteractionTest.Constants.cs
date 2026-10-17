using Content.Shared.Stacks;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Interaction;

// This partial class contains various constant prototype IDs common to interaction tests.
// Should make it easier to mass-change hard coded strings if prototypes get renamed.
public abstract partial class InteractionTest
{
    // Tiles
    // CMU14: CM floor protos replace the upstream floor tiles
    // protected const string Floor = "FloorSteel";
    // protected const string FloorItem = "FloorTileItemSteel";
    protected const string Floor = "CMFloorSteel";
    protected const string FloorItem = "CMTileItemSteel";
    protected const string Plating = "Plating";
    protected const string PlatingRCD = "PlatingRCD";
    protected const string Lattice = "Lattice";
    protected const string PlatingSnow = "PlatingSnow";

    // Structures
    protected const string Airlock = "Airlock";

    // Tools/steps
    protected const string Wrench = "Wrench";
    protected const string Screw = "Screwdriver";
    protected const string Weld = "WelderExperimental";
    protected const string Pry = "Crowbar";
    protected const string Cut = "Wirecutter";

    // Materials/stacks
    protected const string Steel = "Steel";
    protected const string Glass = "Glass";
    protected const string RGlass = "ReinforcedGlass";
    protected const string Plastic = "Plastic";
    protected const string Cable = "Cable";
    protected const string Rod = "MetalRod";
    // CMU14: fork sheets. CMU construction graphs consume these, not the upstream stacks above.
    protected const string CMSteel = "CMSteel";
    protected const string CMGlass = "CMGlass";
    protected const string CMRodMetal = "CMRodMetal";
    protected const string CMFloorSteel = "CMFloorSteel";
    protected const string CMPlating = "CMFloorPlating";
    protected const string CMTileItemSteel = "CMTileItemSteel";
    protected const string CMSheetMetal1 = "CMSheetMetal1";
    protected const string CMRodMetal10 = "CMRodMetal10";
    protected const string CMGlassReinforced = "CMGlassReinforced";

    // Parts
    protected const string Manipulator1 = "MicroManipulatorStockPart";
    protected const string Battery1 = "PowerCellSmall";
    protected const string Battery4 = "PowerCellHyper";

    // Inflatables & Needle used to pop them
    protected static readonly EntProtoId InflatableWall = "InflatableWall";
    protected static readonly EntProtoId Needle = "WeaponMeleeNeedle";
    protected static readonly ProtoId<StackPrototype> InflatableWallStack = "InflatableWall";
}
