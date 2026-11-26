using System.Linq;
using System.Text.RegularExpressions;
using Content.Server.CMU14.Round.Antags.ColonyBounty;
using Content.Server.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.CMU14.Round.Antags.ColonyBounty;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.Fax.Components;
using Content.Shared.Preferences.Loadouts;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._CMU14.Round.Antags;

/// <summary>
/// Early Warning Joe rebalance, detection and counterplay. RB-39: the level that gives the Joe its CMB wanted
/// record (level 3) also faxes the marshals a shuffled line-up of five names, the Joe's among four others,
/// once per Joe; the bounty stays. RB-40: the platoon commander always carries the Weyland reset key.
/// </summary>
[TestFixture]
public sealed class EWJoeRebalanceTest
{
    private const string ColonyJoe = "AU14MobWorkingJoeColony";
    private const string Human = "CMMobHuman";
    private const string Fax = "CMFax";

    [Test]
    public async Task LevelThreeFaxesTheMarshalsAFiveNameLineupOnce()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var rule = entities.System<EWJoeRuleSystem>();

            var fax = entities.SpawnEntity(Fax, map.GridCoords);
            var faxComp = entities.GetComponent<FaxMachineComponent>(fax);
            faxComp.Groups.Clear();
            faxComp.Groups.Add(ColonyCmbFax.MarshalBureauFaxGroup);
            faxComp.PrintingQueue.Clear();

            var joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            var comp = entities.EnsureComponent<EWJoeComponent>(joe);

            // more bystanders than the line-up holds, a synthetic among them
            entities.SpawnEntity(ColonyJoe, map.GridCoords);
            for (var i = 0; i < 5; i++)
            {
                entities.SpawnEntity(Human, map.GridCoords);
            }

            rule.ApplyLevel((joe, comp), 2);
            Assert.That(faxComp.PrintingQueue, Is.Empty, "no fax before the wanted level");

            rule.ApplyLevel((joe, comp), 3);
            Assert.That(entities.TryGetComponent(joe, out ColonyBountyComponent? bounty), "level 3 still brings the wanted record");
            Assert.That(bounty!.Bounty, Is.EqualTo(1500), "and keeps the 1500 bounty");
            Assert.That(faxComp.PrintingQueue, Has.Count.EqualTo(1), "one line-up fax to the marshals");

            var content = faxComp.PrintingQueue.Peek().Content;
            var lines = Regex.Matches(content, @"^\s+\d+\. ", RegexOptions.Multiline).Count;
            Assert.Multiple(() =>
            {
                Assert.That(lines, Is.EqualTo(1 + EWJoeRuleSystem.LineupDecoys), "five names");
                Assert.That(content, Does.Contain(entities.GetComponent<MetaDataComponent>(joe).EntityName), "the Joe is among them");
            });

            // a respawn's level drop and the climb back do not fax again
            rule.LowerLevel((joe, comp), 1);
            rule.ApplyLevel((joe, comp), 3);
            Assert.That(faxComp.PrintingQueue, Has.Count.EqualTo(1), "the line-up goes out once per Joe");
            Assert.That(rule.TrySendLineupFax((joe, comp)), Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PlatoonCommanderCarriesTheWeylandResetKey()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var protos = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            // both sides' platoon commander share this loadout (OPFOR uses the GOVFOR job's), in Distress Signal and FoF
            var role = protos.Index<RoleLoadoutPrototype>("JobAU14JobGOVFORPlatCo");
            Assert.That(role.Groups, Does.Contain(new ProtoId<LoadoutGroupPrototype>("CMUForceWeylandResetKeyCommand")));

            var group = protos.Index<LoadoutGroupPrototype>("CMUForceWeylandResetKeyCommand");
            Assert.That(group.MinLimit, Is.EqualTo(1), "always carried");
            Assert.That(group.Loadouts, Has.Count.EqualTo(1));

            var loadout = protos.Index(group.Loadouts[0]);
            Assert.That(loadout.Storage.Values.SelectMany(v => v).Select(p => p.Id), Does.Contain("CMUSynthResetKeyWeyland"),
                "the key that frees a River-Melded thrall");
        });

        await pair.CleanReturnAsync();
    }
}
