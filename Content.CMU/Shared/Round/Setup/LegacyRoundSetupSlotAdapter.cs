using Content.Shared.AU14.util;

namespace Content.Shared.CMU.Round;

/// <summary>
/// Converts the legacy mixed-purpose marker vocabulary into force-neutral setup purposes during migration.
/// </summary>
public static class LegacyRoundSetupSlotAdapter
{
    /// <summary>
    /// Converts a known semantic marker class. Ambiguous fleet classes and numbered placeholders remain on the legacy fallback.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when a marker value is unknown so additions cannot silently bypass explicit classification.
    /// </exception>
    public static bool TryConvert(PlatoonMarkerClass legacy, out RoundSetupSlot slot)
    {
        RoundSetupSlot? resolved = legacy switch
        {
            PlatoonMarkerClass.Corpsman => RoundSetupSlot.CorpsmanVendor,
            PlatoonMarkerClass.Clothing => RoundSetupSlot.ClothingVendor,
            PlatoonMarkerClass.ShipsideUniform => RoundSetupSlot.ShipsideUniformVendor,
            PlatoonMarkerClass.Weapons => RoundSetupSlot.WeaponsVendor,
            PlatoonMarkerClass.SWeapons => RoundSetupSlot.SpecialWeaponsVendor,
            PlatoonMarkerClass.ObjectivesConsole => RoundSetupSlot.ObjectivesConsole,
            PlatoonMarkerClass.ReturnPointGeneric => RoundSetupSlot.ObjectiveReturnPoint,
            PlatoonMarkerClass.DropshipDestination => RoundSetupSlot.DropshipDestination,
            PlatoonMarkerClass.RequisitionsLift => RoundSetupSlot.RequisitionsLift,
            PlatoonMarkerClass.RequisitionsConsole => RoundSetupSlot.RequisitionsConsole,
            PlatoonMarkerClass.Arifleman => RoundSetupSlot.AutomaticRiflemanVendor,
            PlatoonMarkerClass.Rifleman => RoundSetupSlot.RiflemanVendor,
            PlatoonMarkerClass.Dcc => RoundSetupSlot.DropshipCrewChiefVendor,
            PlatoonMarkerClass.OperationsOfficer => RoundSetupSlot.OperationsOfficerVendor,
            PlatoonMarkerClass.Rto => RoundSetupSlot.RadioTelephoneOperatorVendor,
            PlatoonMarkerClass.JuniorOfficer => RoundSetupSlot.JuniorOfficerVendor,
            PlatoonMarkerClass.MilitaryPolice => RoundSetupSlot.MilitaryPoliceVendor,
            PlatoonMarkerClass.MilitaryDoctor => RoundSetupSlot.MilitaryDoctorVendor,
            PlatoonMarkerClass.SectionSergeant => RoundSetupSlot.SectionSergeantVendor,
            PlatoonMarkerClass.SquadSergeant => RoundSetupSlot.SquadSergeantVendor,
            PlatoonMarkerClass.Pilot => RoundSetupSlot.PilotVendor,
            PlatoonMarkerClass.combattech => RoundSetupSlot.CombatTechnicianVendor,
            PlatoonMarkerClass.LockedFTLDoor => RoundSetupSlot.LockedFtlDoor,
            PlatoonMarkerClass.LockedFTLGlassDoor => RoundSetupSlot.LockedFtlGlassDoor,
            PlatoonMarkerClass.LockedCommandDoor => RoundSetupSlot.LockedCommandDoor,
            PlatoonMarkerClass.LockedSecurityDoor => RoundSetupSlot.LockedSecurityDoor,
            PlatoonMarkerClass.LockedSecurityDoorGlass => RoundSetupSlot.LockedSecurityGlassDoor,
            PlatoonMarkerClass.LockedGlassDoor => RoundSetupSlot.LockedGlassDoor,
            PlatoonMarkerClass.LockedCommandGlassDoor => RoundSetupSlot.LockedCommandGlassDoor,
            PlatoonMarkerClass.LockedNormalDoor => RoundSetupSlot.LockedNormalDoor,
            PlatoonMarkerClass.LockedEngineeringDoor => RoundSetupSlot.LockedEngineeringDoor,
            PlatoonMarkerClass.LockedEngineeringGlassDoor => RoundSetupSlot.LockedEngineeringGlassDoor,
            PlatoonMarkerClass.LockedMedicalDoor => RoundSetupSlot.LockedMedicalDoor,
            PlatoonMarkerClass.LockedMedicalGlassDoor => RoundSetupSlot.LockedMedicalGlassDoor,
            PlatoonMarkerClass.OverwatchConsole => RoundSetupSlot.OverwatchConsole,
            PlatoonMarkerClass.IntelComputer => RoundSetupSlot.IntelligenceComputer,
            PlatoonMarkerClass.TechTree => RoundSetupSlot.TechnologyTreeConsole,
            PlatoonMarkerClass.GroundsideOps or
                PlatoonMarkerClass.GroundsideOpsGovfor or
                PlatoonMarkerClass.GroundsideOpsOpfor => RoundSetupSlot.GroundsideOperationsConsole,
            PlatoonMarkerClass.TacticalMap => RoundSetupSlot.TacticalMap,
            PlatoonMarkerClass.ReqVend => RoundSetupSlot.RequisitionsVendor,
            PlatoonMarkerClass.VehicleCrew => RoundSetupSlot.VehicleCrewVendor,
            PlatoonMarkerClass.Analyzer => RoundSetupSlot.Analyzer,
            PlatoonMarkerClass.AICore => RoundSetupSlot.AiCore,
            PlatoonMarkerClass.AllianceConsoleGovfor or
                PlatoonMarkerClass.AllianceConsoleOpfor => RoundSetupSlot.AllianceConsole,
            PlatoonMarkerClass.OrbitalCannonGovfor or
                PlatoonMarkerClass.OrbitalCannonOpfor => RoundSetupSlot.OrbitalCannon,
            PlatoonMarkerClass.WithdrawConsoleGovfor or
                PlatoonMarkerClass.WithdrawConsoleOpfor or
                PlatoonMarkerClass.WithdrawConsoleColony => RoundSetupSlot.WithdrawalConsole,
            PlatoonMarkerClass.CommandTabletGovfor or
                PlatoonMarkerClass.CommandTabletOpfor => RoundSetupSlot.CommandTablet,
            PlatoonMarkerClass.LockedDoubleNormalDoor => RoundSetupSlot.LockedDoubleNormalDoor,
            PlatoonMarkerClass.LockedDoubleGlassDoor => RoundSetupSlot.LockedDoubleGlassDoor,
            PlatoonMarkerClass.LockedDoubleCommandDoor => RoundSetupSlot.LockedDoubleCommandDoor,
            PlatoonMarkerClass.LockedDoubleCommandGlassDoor => RoundSetupSlot.LockedDoubleCommandGlassDoor,
            PlatoonMarkerClass.LockedDoubleSecurityDoor => RoundSetupSlot.LockedDoubleSecurityDoor,
            PlatoonMarkerClass.LockedDoubleSecurityGlassDoor => RoundSetupSlot.LockedDoubleSecurityGlassDoor,
            PlatoonMarkerClass.LockedDoubleMedicalDoor => RoundSetupSlot.LockedDoubleMedicalDoor,
            PlatoonMarkerClass.LockedDoubleMedicalGlassDoor => RoundSetupSlot.LockedDoubleMedicalGlassDoor,
            PlatoonMarkerClass.LockedDoubleEngineeringDoor => RoundSetupSlot.LockedDoubleEngineeringDoor,
            PlatoonMarkerClass.LockedDoubleEngineeringGlassDoor => RoundSetupSlot.LockedDoubleEngineeringGlassDoor,
            PlatoonMarkerClass.LockedLogisticsDoor => RoundSetupSlot.LockedLogisticsDoor,
            PlatoonMarkerClass.LockedLogisticsGlassDoor => RoundSetupSlot.LockedLogisticsGlassDoor,
            PlatoonMarkerClass.LockedDoubleLogisticsDoor => RoundSetupSlot.LockedDoubleLogisticsDoor,
            PlatoonMarkerClass.LockedDoubleLogisticsGlassDoor => RoundSetupSlot.LockedDoubleLogisticsGlassDoor,
            PlatoonMarkerClass.LaptopCallsign => RoundSetupSlot.CallsignLaptop,
            PlatoonMarkerClass.CommsArrayShip => RoundSetupSlot.ShipCommunicationsArray,
            PlatoonMarkerClass.RosterConsole => RoundSetupSlot.RosterConsole,
            // These fleet classes have conflicting active prototype-ID and class-based meanings.
            PlatoonMarkerClass.DSWeapons or
                PlatoonMarkerClass.DSPilot or
                PlatoonMarkerClass.FighterDestination => null,
            // Historical compatibility values only; no mapper prototypes or setup behavior use them.
            PlatoonMarkerClass.ExtraVendor1 or
                PlatoonMarkerClass.ExtraVendor2 or
                PlatoonMarkerClass.ExtraVendor3 or
                PlatoonMarkerClass.ExtraVendor4 or
                PlatoonMarkerClass.Deco1 or
                PlatoonMarkerClass.Deco2 or
                PlatoonMarkerClass.Deco3 or
                PlatoonMarkerClass.Deco4 or
                PlatoonMarkerClass.Deco5 or
                PlatoonMarkerClass.Deco6 => null,
            _ => throw new ArgumentOutOfRangeException(nameof(legacy), legacy, "Unknown legacy platoon marker class."),
        };

        if (resolved is not { } semantic)
        {
            slot = default;
            return false;
        }

        slot = semantic;
        return true;
    }
}
