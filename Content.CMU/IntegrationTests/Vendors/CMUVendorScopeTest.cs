using System.Linq;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server.CMU14.Vendors;
using Content.Server.GameTicking;
using Content.Shared._RMC14.NightVision;
using Content.Shared._RMC14.Scoping;
using Content.Shared._RMC14.Vendors;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Vendors;

[TestFixture]
public sealed class CMUVendorScopeTest : InteractionTest
{
    private const string TestRule = "CMUVendorScopeTestRule";
    private const string TestVendor = "CMUVendorScopeTestVendor";

    [TestPrototypes]
    private const string Prototypes = """
- type: entity
  id: CMUVendorScopeTestRule
  parent: BaseGameRule
  components:
  - type: InsurgencyRule

- type: entity
  id: CMUVendorScopeTestVendor
  parent: AU14USCMsquadleaderequipmentvendor
  components:
  - type: CMAutomatedVendor
    jobs: []
""";

    [TestCase(false, 0)]
    [TestCase(false, 1)]
    [TestCase(true, 0)]
    [TestCase(true, 1)]
    public async Task SquadLeaderGetsOneScopeAcrossVendors(bool insurgency, int firstChoice)
    {
        EntityUid rule = default;
        if (insurgency)
        {
            await Server.WaitAssertion(() =>
            {
                var ticker = Server.System<GameTicker>();
                Assert.That(ticker.StartGameRule(TestRule, out rule), Is.True);
            });
        }

        try
        {
            await SpawnTarget(TestVendor);
            await Activate();
            Assert.That(IsUiOpen(CMAutomatedVendorUI.Key), Is.True);
            await SendBui(CMAutomatedVendorUI.Key, new CMVendorVendBuiMsg(0, firstChoice, []));

            await Server.WaitAssertion(() =>
            {
                var held = HandSys.GetActiveItem((SPlayer, Hands));
                Assert.That(held, Is.Not.Null);
                var expected = firstChoice == 0 ? "RMCAttachmentS42xTelescopicMiniscope" : "CMUAttachmentNightVisionScope";
                Assert.That(SEntMan.GetComponent<MetaDataComponent>(held!.Value).EntityPrototype!.ID, Is.EqualTo(expected));
                Assert.That(SEntMan.GetComponent<CMVendorUserComponent>(SPlayer).Choices["CMUSquadLeaderScope"], Is.EqualTo(1));
            });

            await DeleteHeldEntity();
            await SendBui(CMAutomatedVendorUI.Key, new CMVendorVendBuiMsg(0, 1 - firstChoice, []));
            Assert.That(HandSys.GetActiveItem((SPlayer, Hands)), Is.Null);

            await SpawnTarget(TestVendor);
            await Activate();
            await SendBui(CMAutomatedVendorUI.Key, new CMVendorVendBuiMsg(0, firstChoice, []));
            Assert.That(HandSys.GetActiveItem((SPlayer, Hands)), Is.Null, "Changing vendors must not reset the scope allowance.");
        }
        finally
        {
            if (insurgency)
                await Server.WaitPost(() => Server.System<GameTicker>().EndGameRule(rule));
        }
    }

    [Test]
    public async Task AllSquadLeaderVendorsShareTheSameScopeAllowance()
    {
        await Server.WaitAssertion(() =>
        {
            var count = 0;
            foreach (var prototype in SProtoMan.EnumeratePrototypes<EntityPrototype>())
            {
                if (prototype.Abstract
                    || !prototype.TryComp<CMAutomatedVendorComponent>(out var vendor, Factory)
                    || !vendor.Jobs.Any(job => job.Id is "CMSquadLeader" or "AU14JobGOVFORSquadSergeant"))
                    continue;

                var scopes = vendor.Sections.Single(section => section.Choices?.Id == "CMUSquadLeaderScope");
                Assert.That(scopes.Choices!.Value.Amount, Is.EqualTo(1), prototype.ID);
                Assert.That(scopes.Entries.Select(entry => entry.Id.Id),
                    Is.EquivalentTo(new[] { "RMCAttachmentS42xTelescopicMiniscope", "CMUAttachmentNightVisionScope" }),
                    prototype.ID);
                Assert.That(scopes.Entries.All(entry => entry.Spawn == 1 && entry.Choices == null), Is.True, prototype.ID);
                count++;
            }

            Assert.That(count, Is.GreaterThanOrEqualTo(11));
        });
    }

    [Test]
    public async Task InsurgencyHidesOnlyRequisitionsNightVisionScopesAndRestoresStock()
    {
        var originals = new Dictionary<EntityUid, (string Id, int? Amount)[]>();
        var withoutScopes = new Dictionary<EntityUid, string[]>();
        EntityUid representative = default;
        EntityUid clf = default;
        EntityUid clfObjective = default;
        EntityUid leader = default;
        EntityUid rule = default;
        NetEntity representativeNet = default;

        (string Id, int? Amount)[] Stock(EntityUid uid) =>
            SEntMan.GetComponent<CMAutomatedVendorComponent>(uid).Sections
                .SelectMany(section => section.Entries).Select(entry => (entry.Id.Id, entry.Amount)).ToArray();

        bool IsNightVisionScope(string id)
        {
            var prototype = SProtoMan.Index<EntityPrototype>(id);
            return prototype.TryComp<ScopeComponent>(out var scope, Factory)
                   && scope.Attachment
                   && prototype.TryComp<NightVisionItemComponent>(out _, Factory);
        }

        await Server.WaitAssertion(() =>
        {
            foreach (var prototype in SProtoMan.EnumeratePrototypes<EntityPrototype>())
            {
                if (prototype.Abstract || !prototype.Components.ContainsKey("CMUInsurgencyRequisitions"))
                    continue;

                var coordinates = prototype.ID == "AU14USCMRequisitionVendor"
                    ? new EntityCoordinates(MapData.MapUid, 4, 4)
                    : new EntityCoordinates(MapData.MapUid, 100 + originals.Count * 4, 100);
                var vendor = SEntMan.SpawnEntity(prototype.ID, coordinates);
                originals.Add(vendor, Stock(vendor));
                withoutScopes.Add(vendor, Stock(vendor).Where(entry => !IsNightVisionScope(entry.Id)).Select(entry => entry.Id).ToArray());
                if (prototype.ID == "AU14USCMRequisitionVendor")
                    representative = vendor;
            }

            Assert.That(originals.Count, Is.GreaterThanOrEqualTo(10));
            Assert.That(representative, Is.Not.EqualTo(EntityUid.Invalid));
            representativeNet = SEntMan.GetNetEntity(representative);
            clf = SEntMan.SpawnEntity("AU14CLFWeaponsVendor", new EntityCoordinates(MapData.MapUid, 200, 100));
            clfObjective = SEntMan.SpawnEntity("AU14CLFObjectiveWeaponsVendor", new EntityCoordinates(MapData.MapUid, 204, 100));
            leader = SEntMan.SpawnEntity("AU14USCMsquadleaderequipmentvendor", new EntityCoordinates(MapData.MapUid, 208, 100));
        });

        var clfStock = Stock(clf);
        var objectiveStock = Stock(clfObjective);
        var leaderStock = Stock(leader);
        try
        {
            await Server.WaitAssertion(() =>
            {
                Assert.That(Server.System<GameTicker>().StartGameRule(TestRule, out rule), Is.True);
                foreach (var (vendor, expected) in withoutScopes)
                    Assert.That(Stock(vendor).Select(entry => entry.Id), Is.EqualTo(expected));
                Assert.That(Stock(representative).Select(entry => entry.Id),
                    Does.Contain("RMCAttachmentS42xTelescopicMiniscope"), "Regular scopes must remain available.");

                Assert.That(Stock(clf), Is.EqualTo(clfStock));
                Assert.That(Stock(clfObjective), Is.EqualTo(objectiveStock));
                Assert.That(Stock(leader), Is.EqualTo(leaderStock));

                var late = SEntMan.SpawnEntity("AU14USCMRequisitionVendor", new EntityCoordinates(MapData.MapUid, 212, 100));
                Assert.That(Stock(late).Any(entry => IsNightVisionScope(entry.Id)), Is.False, "Late-spawned req must also hide night-vision scopes.");
            });
            await RunTicks(5);
            await Client.WaitAssertion(() =>
            {
                var vendor = CEntMan.GetComponent<CMAutomatedVendorComponent>(CEntMan.GetEntity(representativeNet));
                Assert.That(vendor.Sections.SelectMany(section => section.Entries).Select(entry => entry.Id.Id),
                    Is.EqualTo(withoutScopes[representative]));
            });
        }
        finally
        {
            await Server.WaitPost(() => Server.System<GameTicker>().EndGameRule(rule));
        }

        await Server.WaitAssertion(() =>
        {
            foreach (var (vendor, expected) in originals)
            {
                Assert.That(Stock(vendor), Is.EqualTo(expected), "Mode ending must restore original order and stock.");
                Assert.That(SEntMan.GetComponent<CMUInsurgencyRequisitionsComponent>(vendor).HiddenScopes, Is.Empty);
            }
        });
        await RunTicks(5);
        await Client.WaitAssertion(() =>
        {
            var vendor = CEntMan.GetComponent<CMAutomatedVendorComponent>(CEntMan.GetEntity(representativeNet));
            Assert.That(vendor.Sections.SelectMany(section => section.Entries).Select(entry => (entry.Id.Id, entry.Amount)),
                Is.EqualTo(originals[representative]));
        });
    }
}
