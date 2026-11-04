using System.Linq;
using Content.Server.Station.Systems;
using Content.Shared._RMC14.Marines.Squads;
using Content.Shared.CMU14.Radio;
using Content.Shared.Inventory;
using Content.Shared.Preferences;
using Content.Shared.Radio;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Radio;

// the panel's one-press setup is what gets a new operator on the air. it has to load the
// wearer's own squad net and the pack's issued nets, select the squad net, and never touch a
// memory the operator already tuned - otherwise it would be making their decisions for them
[TestFixture]
public sealed class ANPRCQuickSetupTest
{
    private static readonly EntProtoId Pack = "ANPRC117GRadio";
    private static readonly EntProtoId Squad = "SquadGovfor";
    private static readonly ProtoId<JobPrototype> Rifleman = "AU14JobGOVFORSquadRifleman";

    private static readonly ProtoId<RadioChannelPrototype> SquadNet = "radioGovforAlpha";
    private static readonly ProtoId<RadioChannelPrototype> CommandNet = "radioGovforCommand";
    private static readonly ProtoId<RadioChannelPrototype> Tuned = "radioGovforJTAC";

    private const string BackSlot = "back";

    [Test]
    public async Task QuickSetupLoadsSquadAndCommandWithoutOverwriting()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var spawning = server.System<StationSpawningSystem>();
            var inventory = server.System<InventorySystem>();
            var squads = server.System<SquadSystem>();
            var ui = server.System<SharedUserInterfaceSystem>();

            var wearer = spawning.SpawnPlayerMob(testMap.GridCoords, Rifleman, new HumanoidCharacterProfile(), station: null);
            var pack = entities.SpawnEntity(Pack, testMap.GridCoords);

            if (inventory.TryGetSlotEntity(wearer, BackSlot, out var oldBack))
                entities.DeleteEntity(oldBack.Value);

            Assert.That(squads.TryEnsureSquad(Squad, out var squad), Is.True);
            squads.AssignSquad(wearer, (squad.Owner, squad.Comp), null);

            entities.EnsureComponent<ANPRCRadioUserComponent>(wearer);
            Assert.That(inventory.TryEquip(wearer, pack, BackSlot, force: true), Is.True);

            var radio = entities.GetComponent<ANPRCRadioComponent>(pack);

            // a memory the operator tuned by hand, plus the set switched off
            radio.SlotLabels[0] = "MINE";
            radio.Presets[0] = Tuned;
            radio.ActiveSlot = -1;
            radio.Enabled = false;

            ui.OpenUi(pack, ANPRCRadioUI.Key, wearer);
            ui.RaiseUiMessage(pack, ANPRCRadioUI.Key, new ANPRCQuickSetupMsg { Actor = wearer });

            Assert.Multiple(() =>
            {
                Assert.That(radio.Enabled, Is.True, "quick setup switches the set on");
                Assert.That(radio.Presets[0], Is.EqualTo(Tuned), "a tuned memory is never overwritten");
                Assert.That(radio.Presets.Values, Does.Contain(SquadNet), "the wearer's squad net is loaded");
                Assert.That(radio.Presets.Values, Does.Contain(CommandNet), "the issued command net is loaded");
                Assert.That(radio.Presets[radio.ActiveSlot], Is.EqualTo(SquadNet), "the squad net is selected to talk on");
            });

            var anchor = entities.GetComponent<ANPRCRelayAnchorComponent>(pack);
            Assert.That(anchor.Channels, Does.Contain(SquadNet), "the squad net is now relayed");

            // a second press has nothing left to do and must not duplicate anything
            var count = radio.Presets.Count;
            ui.RaiseUiMessage(pack, ANPRCRadioUI.Key, new ANPRCQuickSetupMsg { Actor = wearer });
            Assert.That(radio.Presets.Count, Is.EqualTo(count));

            entities.DeleteEntity(wearer);
            entities.DeleteEntity(pack);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RenameKeepsTheMemorysNet()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var spawning = server.System<StationSpawningSystem>();
            var inventory = server.System<InventorySystem>();
            var ui = server.System<SharedUserInterfaceSystem>();

            var wearer = spawning.SpawnPlayerMob(testMap.GridCoords, Rifleman, new HumanoidCharacterProfile(), station: null);
            var pack = entities.SpawnEntity(Pack, testMap.GridCoords);

            if (inventory.TryGetSlotEntity(wearer, BackSlot, out var oldBack))
                entities.DeleteEntity(oldBack.Value);

            Assert.That(inventory.TryEquip(wearer, pack, BackSlot, force: true), Is.True);

            var radio = entities.GetComponent<ANPRCRadioComponent>(pack);
            radio.SlotLabels[2] = "OLD";
            radio.Presets[2] = CommandNet;

            ui.OpenUi(pack, ANPRCRadioUI.Key, wearer);
            ui.RaiseUiMessage(pack, ANPRCRadioUI.Key, new ANPRCRenameSlotMsg(2, "boss net") { Actor = wearer });

            Assert.Multiple(() =>
            {
                Assert.That(radio.SlotLabels[2], Is.EqualTo("BOSS NET"));
                Assert.That(radio.Presets[2], Is.EqualTo(CommandNet), "renaming used to delete and re-add, emptying the memory");
                Assert.That(radio.SlotLabels.Count, Is.EqualTo(1));
            });

            entities.DeleteEntity(wearer);
            entities.DeleteEntity(pack);
        });

        await pair.CleanReturnAsync();
    }
}
