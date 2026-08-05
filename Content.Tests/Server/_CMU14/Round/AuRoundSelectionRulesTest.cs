using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Content.Server.AU14.Round;
using Content.Shared._RMC14.Rules;
using Content.Shared.AU14.util;
using NUnit.Framework;

namespace Content.Tests.Server._CMU14.Round;

[TestFixture]
public sealed class AuRoundSelectionRulesTest
{
    [Test]
    public void PlanetVoteCarryoverIsSeparatedByPreset()
    {
        var planets = new List<RMCPlanetMapPrototypeComponent>
        {
            new() { MapId = "FirstMap", VoteName = "First Planet" },
            new() { MapId = "SecondMap", VoteName = "Second Planet" },
        };

        var colonyFall = AuRoundSelectionRules.BuildPlanetVoteOptions(
            "ColonyFall",
            planets,
            TimeSpan.FromSeconds(30));
        var insurgency = AuRoundSelectionRules.BuildPlanetVoteOptions(
            "Insurgency",
            planets,
            TimeSpan.FromSeconds(30));
        var distressSignal = AuRoundSelectionRules.BuildPlanetVoteOptions(
            "DistressSignal",
            planets,
            TimeSpan.FromSeconds(30));

        Assert.Multiple(() =>
        {
            Assert.That(colonyFall.CarryoverKey,
                Is.EqualTo("au14-planet:ColonyFall:FirstMap,SecondMap"));
            Assert.That(insurgency.CarryoverKey,
                Is.EqualTo("au14-planet:Insurgency:FirstMap,SecondMap"));
            Assert.That(distressSignal.CarryoverKey,
                Is.EqualTo("au14-planet:DistressSignal:FirstMap,SecondMap"));
            Assert.That(new[]
                {
                    colonyFall.CarryoverKey,
                    insurgency.CarryoverKey,
                    distressSignal.CarryoverKey,
                },
                Is.Unique);
        });
    }

    [Test]
    public void PlatoonVoteCarryoverIsSeparatedByPresetPlanetAndFaction()
    {
        var platoons = new List<PlatoonPrototype>
        {
            CreatePlatoon("USCM", "United States Colonial Marines"),
            CreatePlatoon("RMC", "Royal Marines Commandos"),
        };

        var insurgency = AuRoundSelectionRules.BuildPlatoonVoteOptions(
            "Govfor",
            "Insurgency",
            "AUPlanetStableGarrison",
            platoons,
            TimeSpan.FromSeconds(30));
        var distressSignal = AuRoundSelectionRules.BuildPlatoonVoteOptions(
            "Govfor",
            "DistressSignal",
            "AUPlanetStableGarrison",
            platoons,
            TimeSpan.FromSeconds(30));
        var otherPlanet = AuRoundSelectionRules.BuildPlatoonVoteOptions(
            "Govfor",
            "Insurgency",
            "CMUPlanetLament",
            platoons,
            TimeSpan.FromSeconds(30));
        var opfor = AuRoundSelectionRules.BuildPlatoonVoteOptions(
            "Opfor",
            "Insurgency",
            "AUPlanetStableGarrison",
            platoons,
            TimeSpan.FromSeconds(30));

        Assert.Multiple(() =>
        {
            Assert.That(insurgency.Title, Is.EqualTo("Govfor Vote"));
            Assert.That(insurgency.CarryoverKey,
                Is.EqualTo("au14-platoon:govfor:Insurgency:AUPlanetStableGarrison:RMC,USCM"));
            Assert.That(new[]
                {
                    insurgency.CarryoverKey,
                    distressSignal.CarryoverKey,
                    otherPlanet.CarryoverKey,
                    opfor.CarryoverKey,
                },
                Is.Unique);
            Assert.That(insurgency.Options.Select(option => option.text),
                Is.EqualTo(new[] { "United States Colonial Marines", "Royal Marines Commandos" }));
        });
    }

    [Test]
    public void ShipVoteCarryoverIsSeparatedBySelectedPlatoon()
    {
        var uscm = CreatePlatoon("USCM", "United States Colonial Marines");
        var rmc = CreatePlatoon("RMC", "Royal Marines Commandos");
        var ships = new List<string> { "ShipB", "ShipA" };

        var uscmVote = AuRoundSelectionRules.BuildShipVoteOptions(
            "Govfor",
            "DistressSignal",
            "AUPlanetTrijent",
            uscm,
            ships,
            TimeSpan.FromSeconds(30));
        var rmcVote = AuRoundSelectionRules.BuildShipVoteOptions(
            "Govfor",
            "DistressSignal",
            "AUPlanetTrijent",
            rmc,
            ships,
            TimeSpan.FromSeconds(30));

        Assert.Multiple(() =>
        {
            Assert.That(uscmVote.CarryoverKey,
                Is.EqualTo("au14-ship:govfor:USCM:DistressSignal:AUPlanetTrijent:ShipA,ShipB"));
            Assert.That(rmcVote.CarryoverKey, Is.Not.EqualTo(uscmVote.CarryoverKey));
        });
    }

    private static PlatoonPrototype CreatePlatoon(string id, string name)
    {
        var platoon = (PlatoonPrototype) RuntimeHelpers.GetUninitializedObject(typeof(PlatoonPrototype));
        SetBackingField(platoon, nameof(PlatoonPrototype.ID), id);
        SetBackingField(platoon, nameof(PlatoonPrototype.Name), name);
        return platoon;
    }

    private static void SetBackingField<T>(PlatoonPrototype platoon, string property, T value)
    {
        typeof(PlatoonPrototype)
            .GetField($"<{property}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(platoon, value);
    }
}
