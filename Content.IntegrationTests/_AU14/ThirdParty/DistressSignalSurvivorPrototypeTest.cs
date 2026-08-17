using System.Collections.Generic;
using System.Linq;
using Content.Server._CMU14.Threats;
using Content.Shared._CMU14.Threats;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._AU14.ThirdParty;

[TestFixture]
public sealed class DistressSignalSurvivorPrototypeTest
{
    private static readonly HashSet<string> ExpectedSurvivorParties = new(StringComparer.OrdinalIgnoreCase)
    {
        "AU14IntelThirdPartyMarksmanGOVFOR",
        "AU14IntelThirdPartySniperGOVFOR",
        "CLFCellReinforcementsIntel",
        "CLFCellReinforcementsIntelMachineGunner",
        "CLFSurvsBig",
        "CLFSurvsMedium",
        "CLFSurvsSmall",
        "FORECON",
        "FORECONAlt",
        "FORECONAlt2",
        "FORECONFirstRecon",
        "FORECONKillTeam",
        "IPIE",
        "IPIESynth",
        "LACNSniperteam",
        "LACNSniperteamIntel",
        "NSPAInvestigationParty",
        "NSPAInvestigationPartyAlt",
        "NSPAInvestigationPartyArmored",
        "PAPInvestigationParty",
        "PAPInvestigationPartyAlt",
        "UPPGROM",
        "UPPGROMAlt",
        "UPPGROMAlt2",
        "UPPTDLostTeam",
        "USArmy",
        "USArmyAlt",
        "USArmyAlt2",
        "USArmyArmored",
        "USArmyTank",
        "VAIPO",
        "VAISO",
        "VAISP",
        "WEYUSurvLarge",
        "WEYUSurvMedium",
        "WEYUSurvSmall",
        "ProdigySurvLarge",
        "ProdigySurvMedium",
        "ProdigySurvSmall",
        "WYHT",
        "WYPMCParty",
        "WYPMCPartyAlt",
    };

    [Test]
    public async Task SurvivorPartiesAndAnnouncementsAreValid()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var localization = server.ResolveDependency<ILocalizationManager>();
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var survivorParties = prototypes
                .EnumeratePrototypes<ThirdPartyPrototype>()
                .Where(party => party.AnnounceAsSurvivors)
                .ToDictionary(party => party.ID, StringComparer.OrdinalIgnoreCase);

            Assert.That(survivorParties.Keys, Is.EquivalentTo(ExpectedSurvivorParties));
            foreach (var (id, party) in survivorParties)
            {
                Assert.That(
                    prototypes.TryIndex(party.PartySpawn, out PartySpawnPrototype spawn),
                    Is.True,
                    $"Survivor party '{id}' references missing party spawn '{party.PartySpawn}'.");
                Assert.That(
                    ThreatVoteSelection.CalculateBodyCount(spawn, 100).Total,
                    Is.GreaterThan(0),
                    $"Survivor party '{id}' must spawn at least one player body.");
                if (party.RoundStart)
                {
                    Assert.That(
                        spawn.Scaling,
                        Is.Empty,
                        $"Round-start survivor party '{id}' uses population scaling, which would invalidate its pre-round count.");
                }
            }

            Assert.That(localization.HasString("cmu-distress-signal-survivors-spawning"), Is.True);
            Assert.That(localization.HasString("cmu-distress-signal-no-survivors"), Is.True);
            Assert.That(
                localization.GetString("cmu-distress-signal-survivors-spawning", ("count", 1)),
                Does.Contain("1 survivor"));
            Assert.That(
                localization.GetString("cmu-distress-signal-survivors-spawning", ("count", 2)),
                Does.Contain("2 survivors"));
        });

        await pair.CleanReturnAsync();
    }
}
