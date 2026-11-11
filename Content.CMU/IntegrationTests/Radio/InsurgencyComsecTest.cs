using System.Linq;
using Content.Server.CMU14.Insurgency;
using Content.Server.CMU14.Radio;
using Content.Server.GameTicking.Presets;
using Content.Server.Station.Systems;
using Content.Shared._RMC14.Vendors;
using Content.Shared.CMU14.Insurgency;
using Content.Shared.CMU14.Radio;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Inventory;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Radio;

// insurgency comms: the cell can recrypto and get its sets back on the new key, every INSFOR
// faction can buy fill cards, and the mode runs without radio DF
[TestFixture]
public sealed class InsurgencyComsecTest
{
    private static readonly EntProtoId ClfPack = "ANPRC117GRadioCLFFilled";
    private static readonly EntProtoId ClfCard = "ANPRCFillCardCLF";
    private static readonly ProtoId<JobPrototype> CellLeader = "AU14JobCLFCellLeader";
    private static readonly ProtoId<JobPrototype> RadioOperator = "AU14JobCLFRadioOperator";

    [Test]
    public async Task CellLeaderRecryptosAndTheCellComesBackOnTheNewKey()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var ui = server.System<SharedUserInterfaceSystem>();
            var crypto = server.System<ANPRCCryptoSystem>();

            var (leader, leaderPack) = SpawnOperator(server, testMap.GridCoords, CellLeader);
            var (rto, rtoPack) = SpawnOperator(server, testMap.GridCoords, RadioOperator);

            // the job grants it at round join, SpawnPlayerMob doesn't apply roundComponents
            entities.EnsureComponent<ANPRCCryptoAuthorityComponent>(leader);

            Assert.That(crypto.GetFillFaction(leaderPack), Is.EqualTo("clf"), "the cell's sets ship filled");
            Assert.That(crypto.IsFillStale(rtoPack), Is.False);

            // an operator can't supersede the cell's key on their own
            ui.OpenUi(rtoPack, ANPRCRadioUI.Key, rto);
            ui.RaiseUiMessage(rtoPack, ANPRCRadioUI.Key, new ANPRCCryptoRecryptoMsg { Actor = rto });
            Assert.That(crypto.IsFillStale(leaderPack), Is.False, "no authority, no recrypto");

            ui.OpenUi(leaderPack, ANPRCRadioUI.Key, leader);
            ui.RaiseUiMessage(leaderPack, ANPRCRadioUI.Key, new ANPRCCryptoRecryptoMsg { Actor = leader });

            Assert.Multiple(() =>
            {
                Assert.That(crypto.IsFillStale(leaderPack), Is.False, "the ordering set holds the new key");
                Assert.That(crypto.IsFillStale(rtoPack), Is.True, "every other cell card is superseded");
            });

            // a card fresh off the rack is cut on the current key
            var fresh = entities.SpawnEntity(ClfCard, testMap.GridCoords);
            var freshCard = entities.GetComponent<ANPRCFillCardComponent>(fresh);
            var leaderCard = entities.GetComponent<ANPRCFillCardComponent>(
                entities.GetComponent<ItemSlotsComponent>(leaderPack).Slots[ANPRCCryptoSystem.FillSlotId].Item!.Value);
            Assert.That(freshCard.Generation, Is.EqualTo(leaderCard.Generation), "a new card is current");

            // and the sets still out there get the key pushed to them
            ui.RaiseUiMessage(leaderPack, ANPRCRadioUI.Key, new ANPRCOtarMsg { Actor = leader });
            Assert.That(crypto.IsFillStale(rtoPack), Is.False, "OTAR brought the operator's set current");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EveryFactionCanBuyFillCards()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var apply = server.System<InsurgencyFactionApplySystem>();
            var reference = server.ProtoMan.Index<EntityPrototype>("AU14CLFObjectiveWeaponsVendor")
                .Components.Values.Select(c => c.Component).OfType<CMAutomatedVendorComponent>().Single();
            var price = reference.Sections.SelectMany(s => s.Entries).Single(e => e.Id == ClfCard).Points;

            // a host-authored faction with a points arsenal and no comms gear at all
            var custom = new FactionDefinition();
            custom.CellKit.VendorDefinitions.Add(new FactionVendorDefinition
            {
                Name = "arsenal",
                BaseModel = "AU14CLFObjectiveWeaponsVendor",
                UsesIntelPoints = true,
                Sections =
                {
                    new CMVendorSection
                    {
                        Name = "Rifles",
                        Entries = { new CMVendorEntry { Id = "RMCWeaponBoltActionRifle", Amount = 3, Points = 2 } },
                    },
                },
            });

            apply.ApplyFaction(custom);

            var cards = apply.GetActiveFaction()!.CellKit.VendorDefinitions[0].Sections
                .SelectMany(s => s.Entries)
                .Where(e => e.Id == ClfCard)
                .ToList();

            Assert.That(cards, Has.Count.EqualTo(1), "the points vendor sells fill cards");
            Assert.That(cards[0].Points, Is.EqualTo(price), "at the CLF rack's price");
            Assert.That(custom.CellKit.VendorDefinitions[0].Sections.SelectMany(s => s.Entries).Any(e => e.Id == ClfCard),
                Is.False, "the stored faction is left alone");

            // a faction that already stocks them keeps its own line, no second one
            var stocked = new FactionDefinition();
            stocked.CellKit.VendorDefinitions.Add(new FactionVendorDefinition
            {
                Name = "arsenal",
                BaseModel = "AU14CLFObjectiveWeaponsVendor",
                UsesIntelPoints = true,
                Sections =
                {
                    new CMVendorSection
                    {
                        Name = "Comms",
                        Entries = { new CMVendorEntry { Id = ClfCard, Amount = 9, Points = 1 } },
                    },
                },
            });

            apply.ApplyFaction(stocked);

            var own = apply.GetActiveFaction()!.CellKit.VendorDefinitions[0].Sections
                .SelectMany(s => s.Entries)
                .Where(e => e.Id == ClfCard)
                .ToList();

            Assert.That(own, Has.Count.EqualTo(1));
            Assert.That(own[0].Amount, Is.EqualTo(9));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task InsurgencyRunsWithoutDirectionFinding()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            Assert.That(server.ProtoMan.Index<GamePresetPrototype>("Insurgency").AnprcDirectionFinding, Is.False);
            Assert.That(server.ProtoMan.Index<GamePresetPrototype>("ForceOnForce").AnprcDirectionFinding, Is.True,
                "only the insurgency mode drops DF");
        });

        await pair.CleanReturnAsync();
    }

    private static (EntityUid Wearer, EntityUid Pack) SpawnOperator(
        Robust.UnitTesting.RobustIntegrationTest.ServerIntegrationInstance server,
        EntityCoordinates coords,
        ProtoId<JobPrototype> job)
    {
        var entities = server.EntMan;
        var inventory = server.System<InventorySystem>();

        var wearer = server.System<StationSpawningSystem>()
            .SpawnPlayerMob(coords, job, new HumanoidCharacterProfile(), station: null);
        var pack = entities.SpawnEntity(ClfPack, coords);

        if (inventory.TryGetSlotEntity(wearer, "back", out var oldBack))
            entities.DeleteEntity(oldBack.Value);

        entities.EnsureComponent<ANPRCRadioUserComponent>(wearer);
        Assert.That(inventory.TryEquip(wearer, pack, "back", force: true), Is.True);

        return (wearer, pack);
    }
}
