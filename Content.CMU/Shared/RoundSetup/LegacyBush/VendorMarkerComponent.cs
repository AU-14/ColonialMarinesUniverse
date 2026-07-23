namespace Content.Shared._CMU14.RoundSetup.LegacyBush;

using Robust.Shared.Prototypes;

/// <summary>
/// Marks a USS Bush map location where the legacy platoon setup placed a ship entity.
/// The current compatibility resolver uses <see cref="Replacement"/> to materialize the
/// default UNMC equivalent without restoring the removed AU platoon framework.
/// </summary>
[RegisterComponent]
public sealed partial class VendorMarkerComponent : Component
{
    [DataField]
    public bool Govfor;

    [DataField]
    public bool Opfor;

    [DataField("dropship")]
    public bool Dropship;

    [DataField]
    public bool Ship;

    [DataField]
    public LegacyBushMarkerClass Class;

    [DataField]
    public EntProtoId? Replacement;

    [DataField]
    public bool PreserveName;
}

public enum LegacyBushMarkerClass
{
    Corpsman,
    Clothing,
    Weapons,
    SWeapons,
    ObjectivesConsole,
    ReturnPointGeneric,
    DropshipDestination,
    RequisitionsLift,
    RequisitionsConsole,
    RequisitionsVendor,
    AutomaticRifleman,
    Rifleman,
    DropshipCrewChief,
    OperationsOfficer,
    RadioTelephoneOperator,
    JuniorOfficer,
    MilitaryPolice,
    MilitaryDoctor,
    SectionSergeant,
    Pilot,
    CombatTechnician,
    LockedCommandDoor,
    LockedSecurityDoor,
    LockedSecurityDoorGlass,
    LockedGlassDoor,
    LockedCommandGlassDoor,
    LockedEngineeringDoor,
    LockedEngineeringGlassDoor,
    LockedMedicalGlassDoor,
    OverwatchConsole,
    TechTree,
    Analyzer,
    AllianceConsoleGovfor,
    WithdrawConsoleGovfor,
    CommandTabletGovfor,
}
