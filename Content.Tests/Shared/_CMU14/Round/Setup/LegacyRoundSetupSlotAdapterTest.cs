#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Content.Shared.AU14.util;
using Content.Shared.CMU.Round;
using NUnit.Framework;

namespace Content.Tests.Shared._CMU14.Round.Setup;

[TestFixture]
public sealed class LegacyRoundSetupSlotAdapterTest
{
    private static readonly IReadOnlyDictionary<PlatoonMarkerClass, RoundSetupSlot> ExpectedMappings =
        new Dictionary<PlatoonMarkerClass, RoundSetupSlot>
        {
            [PlatoonMarkerClass.Corpsman] = RoundSetupSlot.CorpsmanVendor,
            [PlatoonMarkerClass.Clothing] = RoundSetupSlot.ClothingVendor,
            [PlatoonMarkerClass.ShipsideUniform] = RoundSetupSlot.ShipsideUniformVendor,
            [PlatoonMarkerClass.Weapons] = RoundSetupSlot.WeaponsVendor,
            [PlatoonMarkerClass.SWeapons] = RoundSetupSlot.SpecialWeaponsVendor,
            [PlatoonMarkerClass.ObjectivesConsole] = RoundSetupSlot.ObjectivesConsole,
            [PlatoonMarkerClass.ReturnPointGeneric] = RoundSetupSlot.ObjectiveReturnPoint,
            [PlatoonMarkerClass.DropshipDestination] = RoundSetupSlot.DropshipDestination,
            [PlatoonMarkerClass.RequisitionsLift] = RoundSetupSlot.RequisitionsLift,
            [PlatoonMarkerClass.RequisitionsConsole] = RoundSetupSlot.RequisitionsConsole,
            [PlatoonMarkerClass.Arifleman] = RoundSetupSlot.AutomaticRiflemanVendor,
            [PlatoonMarkerClass.Rifleman] = RoundSetupSlot.RiflemanVendor,
            [PlatoonMarkerClass.Dcc] = RoundSetupSlot.DropshipCrewChiefVendor,
            [PlatoonMarkerClass.OperationsOfficer] = RoundSetupSlot.OperationsOfficerVendor,
            [PlatoonMarkerClass.Rto] = RoundSetupSlot.RadioTelephoneOperatorVendor,
            [PlatoonMarkerClass.JuniorOfficer] = RoundSetupSlot.JuniorOfficerVendor,
            [PlatoonMarkerClass.MilitaryPolice] = RoundSetupSlot.MilitaryPoliceVendor,
            [PlatoonMarkerClass.MilitaryDoctor] = RoundSetupSlot.MilitaryDoctorVendor,
            [PlatoonMarkerClass.SectionSergeant] = RoundSetupSlot.SectionSergeantVendor,
            [PlatoonMarkerClass.SquadSergeant] = RoundSetupSlot.SquadSergeantVendor,
            [PlatoonMarkerClass.Pilot] = RoundSetupSlot.PilotVendor,
            [PlatoonMarkerClass.combattech] = RoundSetupSlot.CombatTechnicianVendor,
            [PlatoonMarkerClass.LockedFTLDoor] = RoundSetupSlot.LockedFtlDoor,
            [PlatoonMarkerClass.LockedFTLGlassDoor] = RoundSetupSlot.LockedFtlGlassDoor,
            [PlatoonMarkerClass.LockedCommandDoor] = RoundSetupSlot.LockedCommandDoor,
            [PlatoonMarkerClass.LockedSecurityDoor] = RoundSetupSlot.LockedSecurityDoor,
            [PlatoonMarkerClass.LockedSecurityDoorGlass] = RoundSetupSlot.LockedSecurityGlassDoor,
            [PlatoonMarkerClass.LockedGlassDoor] = RoundSetupSlot.LockedGlassDoor,
            [PlatoonMarkerClass.LockedCommandGlassDoor] = RoundSetupSlot.LockedCommandGlassDoor,
            [PlatoonMarkerClass.LockedNormalDoor] = RoundSetupSlot.LockedNormalDoor,
            [PlatoonMarkerClass.LockedEngineeringDoor] = RoundSetupSlot.LockedEngineeringDoor,
            [PlatoonMarkerClass.LockedEngineeringGlassDoor] = RoundSetupSlot.LockedEngineeringGlassDoor,
            [PlatoonMarkerClass.LockedMedicalDoor] = RoundSetupSlot.LockedMedicalDoor,
            [PlatoonMarkerClass.LockedMedicalGlassDoor] = RoundSetupSlot.LockedMedicalGlassDoor,
            [PlatoonMarkerClass.OverwatchConsole] = RoundSetupSlot.OverwatchConsole,
            [PlatoonMarkerClass.IntelComputer] = RoundSetupSlot.IntelligenceComputer,
            [PlatoonMarkerClass.TechTree] = RoundSetupSlot.TechnologyTreeConsole,
            [PlatoonMarkerClass.GroundsideOps] = RoundSetupSlot.GroundsideOperationsConsole,
            [PlatoonMarkerClass.TacticalMap] = RoundSetupSlot.TacticalMap,
            [PlatoonMarkerClass.ReqVend] = RoundSetupSlot.RequisitionsVendor,
            [PlatoonMarkerClass.VehicleCrew] = RoundSetupSlot.VehicleCrewVendor,
            [PlatoonMarkerClass.Analyzer] = RoundSetupSlot.Analyzer,
            [PlatoonMarkerClass.AICore] = RoundSetupSlot.AiCore,
            [PlatoonMarkerClass.AllianceConsoleGovfor] = RoundSetupSlot.AllianceConsole,
            [PlatoonMarkerClass.AllianceConsoleOpfor] = RoundSetupSlot.AllianceConsole,
            [PlatoonMarkerClass.OrbitalCannonGovfor] = RoundSetupSlot.OrbitalCannon,
            [PlatoonMarkerClass.OrbitalCannonOpfor] = RoundSetupSlot.OrbitalCannon,
            [PlatoonMarkerClass.WithdrawConsoleGovfor] = RoundSetupSlot.WithdrawalConsole,
            [PlatoonMarkerClass.WithdrawConsoleOpfor] = RoundSetupSlot.WithdrawalConsole,
            [PlatoonMarkerClass.WithdrawConsoleColony] = RoundSetupSlot.WithdrawalConsole,
            [PlatoonMarkerClass.CommandTabletGovfor] = RoundSetupSlot.CommandTablet,
            [PlatoonMarkerClass.CommandTabletOpfor] = RoundSetupSlot.CommandTablet,
            [PlatoonMarkerClass.GroundsideOpsGovfor] = RoundSetupSlot.GroundsideOperationsConsole,
            [PlatoonMarkerClass.GroundsideOpsOpfor] = RoundSetupSlot.GroundsideOperationsConsole,
            [PlatoonMarkerClass.LockedDoubleNormalDoor] = RoundSetupSlot.LockedDoubleNormalDoor,
            [PlatoonMarkerClass.LockedDoubleGlassDoor] = RoundSetupSlot.LockedDoubleGlassDoor,
            [PlatoonMarkerClass.LockedDoubleCommandDoor] = RoundSetupSlot.LockedDoubleCommandDoor,
            [PlatoonMarkerClass.LockedDoubleCommandGlassDoor] = RoundSetupSlot.LockedDoubleCommandGlassDoor,
            [PlatoonMarkerClass.LockedDoubleSecurityDoor] = RoundSetupSlot.LockedDoubleSecurityDoor,
            [PlatoonMarkerClass.LockedDoubleSecurityGlassDoor] = RoundSetupSlot.LockedDoubleSecurityGlassDoor,
            [PlatoonMarkerClass.LockedDoubleMedicalDoor] = RoundSetupSlot.LockedDoubleMedicalDoor,
            [PlatoonMarkerClass.LockedDoubleMedicalGlassDoor] = RoundSetupSlot.LockedDoubleMedicalGlassDoor,
            [PlatoonMarkerClass.LockedDoubleEngineeringDoor] = RoundSetupSlot.LockedDoubleEngineeringDoor,
            [PlatoonMarkerClass.LockedDoubleEngineeringGlassDoor] = RoundSetupSlot.LockedDoubleEngineeringGlassDoor,
            [PlatoonMarkerClass.LockedLogisticsDoor] = RoundSetupSlot.LockedLogisticsDoor,
            [PlatoonMarkerClass.LockedLogisticsGlassDoor] = RoundSetupSlot.LockedLogisticsGlassDoor,
            [PlatoonMarkerClass.LockedDoubleLogisticsDoor] = RoundSetupSlot.LockedDoubleLogisticsDoor,
            [PlatoonMarkerClass.LockedDoubleLogisticsGlassDoor] = RoundSetupSlot.LockedDoubleLogisticsGlassDoor,
            [PlatoonMarkerClass.LaptopCallsign] = RoundSetupSlot.CallsignLaptop,
            [PlatoonMarkerClass.CommsArrayShip] = RoundSetupSlot.ShipCommunicationsArray,
            [PlatoonMarkerClass.RosterConsole] = RoundSetupSlot.RosterConsole,
        };

    private static readonly HashSet<PlatoonMarkerClass> ExpectedUnsupportedLegacyClasses =
    [
        PlatoonMarkerClass.DSWeapons,
        PlatoonMarkerClass.DSPilot,
        PlatoonMarkerClass.FighterDestination,
        PlatoonMarkerClass.ExtraVendor1,
        PlatoonMarkerClass.ExtraVendor2,
        PlatoonMarkerClass.ExtraVendor3,
        PlatoonMarkerClass.ExtraVendor4,
        PlatoonMarkerClass.Deco1,
        PlatoonMarkerClass.Deco2,
        PlatoonMarkerClass.Deco3,
        PlatoonMarkerClass.Deco4,
        PlatoonMarkerClass.Deco5,
        PlatoonMarkerClass.Deco6,
    ];

    [Test]
    public void ExplicitlyClassifiesEveryLegacyMarkerClass()
    {
        var classified = ExpectedMappings.Keys
            .Concat(ExpectedUnsupportedLegacyClasses)
            .ToArray();
        Assert.That(classified, Is.EquivalentTo(Enum.GetValues<PlatoonMarkerClass>()));

        foreach (var (legacy, expected) in ExpectedMappings)
        {
            Assert.That(LegacyRoundSetupSlotAdapter.TryConvert(legacy, out var actual), Is.True, legacy.ToString());
            Assert.That(actual, Is.EqualTo(expected), legacy.ToString());
        }

        foreach (var legacy in ExpectedUnsupportedLegacyClasses)
        {
            Assert.That(LegacyRoundSetupSlotAdapter.TryConvert(legacy, out var slot), Is.False, legacy.ToString());
            Assert.That(slot, Is.EqualTo(RoundSetupSlot.None), legacy.ToString());
        }
    }

    [Test]
    public void RejectsUnknownLegacyMarkerValue()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LegacyRoundSetupSlotAdapter.TryConvert((PlatoonMarkerClass) int.MaxValue, out _));
    }
}
