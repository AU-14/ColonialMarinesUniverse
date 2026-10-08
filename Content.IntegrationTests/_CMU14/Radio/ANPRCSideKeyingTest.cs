using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared._RMC14.Vendors;
using Content.Shared.CMU14;
using Content.Shared.CMU14.Callsigns;
using Content.Shared.CMU14.Radio;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Radio;

// every platoon flies either side off one catalog and vendor set, so the comms stock they sell is
// side-neutral and keys itself. a set or card keyed to the wrong side is a dead radio, or worse, the
// enemy's key in a friendly pocket
[TestFixture]
public sealed class ANPRCSideKeyingTest
{
    private const string IssuedRadio = "ANPRC117GRadioIssued";
    private const string IssuedCard = "ANPRCFillCardIssued";

    [Test]
    public async Task IssuedRadioTakesTheShipsSide()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.EntMan;

        await server.WaitAssertion(() =>
        {
            entMan.EnsureComponent<ShipFactionComponent>(map.Grid).Faction = "opfor";

            var radio = entMan.SpawnEntity(IssuedRadio, map.GridCoords);
            var anprc = entMan.GetComponent<ANPRCRadioComponent>(radio);

            Assert.That(anprc.OperatorFaction, Is.EqualTo("opfor"));
            Assert.That(anprc.StandardNets.Select(net => net.Channel.Id), Does.Contain("radioOpforCommand"));
            Assert.That(entMan.GetComponent<RTORelayComponent>(radio).BridgedChannels.Select(c => c.Id),
                Is.EquivalentTo(new[] { "radioOpforAlpha", "radioOpforBravo", "radioOpforCharlie" }));
            Assert.That(entMan.GetComponent<AU14CallsignConsoleComponent>(radio).Faction, Is.EqualTo("opfor"));

            var slot = server.System<ItemSlotsSystem>().GetItemOrNull(radio, "fill_card");
            Assert.That(slot, Is.Not.Null, "the issued set ships with a card");
            var card = entMan.GetComponent<ANPRCFillCardComponent>(slot!.Value);
            Assert.That(card.Faction, Is.EqualTo("opfor"));
            Assert.That(card.Designation, Is.EqualTo("KY-58"));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RequisitionedCardTakesThePickersSide()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.EntMan;

        await server.WaitAssertion(() =>
        {
            // requisitions build crate contents in nullspace, so nothing is keyed on arrival
            var card = entMan.SpawnEntity(IssuedCard, MapCoordinates.Nullspace);
            Assert.That(entMan.GetComponent<ANPRCFillCardComponent>(card).Faction, Is.Empty);

            var marine = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            entMan.EnsureComponent<MarineComponent>(marine).Faction = "govfor";

            entMan.System<SharedTransformSystem>().SetCoordinates(card, map.GridCoords);
            Assert.That(server.System<SharedHandsSystem>().TryPickupAnyHand(marine, card));

            var keyed = entMan.GetComponent<ANPRCFillCardComponent>(card);
            Assert.That(keyed.Faction, Is.EqualTo("govfor"));
            Assert.That(keyed.Designation, Is.EqualTo("KY-99A"));
            Assert.That(entMan.GetComponent<MetaDataComponent>(card).EntityName, Is.EqualTo("GOVFOR COMSEC fill card"));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task UnkeyedCardTakesTheSideOfTheSetItIsLoadedInto()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var radio = entMan.SpawnEntity("ANPRC117GRadioOPFOR", map.GridCoords);
            var card = entMan.SpawnEntity(IssuedCard, MapCoordinates.Nullspace);

            var slots = server.System<ItemSlotsSystem>();
            Assert.That(slots.TryGetSlot(radio, "fill_card", out var slot));
            Assert.That(slots.TryInsert(radio, slot!, card, null));

            Assert.That(entMan.GetComponent<ANPRCFillCardComponent>(card).Faction, Is.EqualTo("opfor"));
        });

        await pair.CleanReturnAsync();
    }

    // the ten platoons that fly GOVFOR or OPFOR all have to be able to buy a key and a set
    [Test]
    public async Task EveryPlatoonSellsSideNeutralComms()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var protoMan = server.ProtoMan;

        var catalogs = new[]
        {
            "USCMCargoCatalog", "LACNCargoCatalog", "UPPCargoCatalog", "WYPMCCargoCatalog", "CMBCIUCargoCatalog",
            "HAZOPSCargoCatalog", "ProdigyCargoCatalog", "VAIPOCargoCatalog", "RMCCargoCatalog",
        };
        var vendors = new[]
        {
            "AU14USCMcommandequipmentvendor", "AU14LACNcommandequipmentvendor", "AU14UPPcommandvendor",
            "AU14WYdirectorequipmentvendor", "AU14CMBCIUCommandVendor", "AU14HAZOPScommandequipmentvendor",
            "AU14prodigycommandequipmentvendor", "AU14VAIPOcommandequipmentvendor", "AU14RMCcommandequipmentvendor",
            "RuMCBEARcommandvendor",
        };

        await server.WaitAssertion(() =>
        {
            var factory = server.ResolveDependency<IComponentFactory>();

            Assert.Multiple(() =>
            {
                foreach (var id in catalogs)
                {
                    Assert.That(protoMan.Index<EntityPrototype>(id)
                        .TryGetComponent(out RequisitionsComputerComponent? asrs, factory), id);
                    var crates = asrs!.Categories.SelectMany(c => c.Entries).Select(e => e.Crate.Id).ToList();

                    Assert.That(crates, Does.Contain("ANPRCFillCardResupplyCrate"), id);
                    Assert.That(crates, Does.Contain("ANPRCRadioResupplyCrate"), id);
                    Assert.That(crates.Where(c => c.EndsWith("GOVFOR") || c.EndsWith("OPFOR")), Is.Empty, id);
                }

                foreach (var id in vendors)
                {
                    Assert.That(protoMan.Index<EntityPrototype>(id)
                        .TryGetComponent(out CMAutomatedVendorComponent? vendor, factory), id);
                    var stock = vendor!.Sections.SelectMany(s => s.Entries).Select(e => e.Id.Id).ToList();

                    Assert.That(stock, Does.Contain(IssuedRadio), id);
                    Assert.That(stock, Does.Contain(IssuedCard), id);
                    Assert.That(stock, Does.Not.Contain("ANPRC117GRadioFilled").And.Not.Contain("ANPRC117GRadioOPFORFilled"), id);
                }
            });
        });

        await pair.CleanReturnAsync();
    }

    // the command vendors weren't the only leak, CIU's req rack still sold govfor-filled sets
    [Test]
    public async Task NoVendorSellsSideBakedComsec()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var protoMan = server.ProtoMan;
        var baked = new[]
        {
            "ANPRC117GRadioFilled", "ANPRC117GRadioOPFORFilled", "ANPRCFillCardGOVFOR", "ANPRCFillCardOPFOR",
        };

        await server.WaitAssertion(() =>
        {
            var factory = server.ResolveDependency<IComponentFactory>();

            Assert.Multiple(() =>
            {
                foreach (var proto in protoMan.EnumeratePrototypes<EntityPrototype>())
                {
                    if (proto.Abstract || !proto.TryGetComponent(out CMAutomatedVendorComponent? vendor, factory))
                        continue;

                    var stock = vendor!.Sections.SelectMany(s => s.Entries).Select(e => e.Id.Id).ToList();
                    Assert.That(stock.Intersect(baked), Is.Empty, proto.ID);
                }
            });
        });

        await pair.CleanReturnAsync();
    }
}
