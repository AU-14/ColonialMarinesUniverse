using Content.Server._RMC14.Telephone;
using Content.Server.Station.Systems;
using Content.Shared._RMC14.Telephone;
using Content.Shared.CMU14.Radio;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared.Storage;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.UnitTesting;

namespace Content.IntegrationTests.CMU14.Radio;

// the AN/PRC-117G is a phone on the RMC exchange that shares its one handset with the net. these
// pin the parts that are easy to break: the exchange and the set agreeing on which handset is which,
// the side coming off the fill card, a call placed and answered set to set, the net refusing to key
// while a call holds the handset, and the pack carrying the same bag space as the old AN/PRC-55
[TestFixture]
public sealed class ANPRCPhoneTest
{
    private static readonly EntProtoId FilledPack = "ANPRC117GRadioFilled";

    private static readonly ProtoId<JobPrototype> Rifleman = "AU14JobGOVFORSquadRifleman";

    private const string BackSlot = "back";

    [Test]
    public async Task SetToSetCallRingsAnswersAndHangsUp()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        EntityUid caller = default, callerPack = default, callee = default, calleePack = default;

        await server.WaitAssertion(() =>
        {
            (caller, callerPack) = SpawnOperator(server, testMap.GridCoords, FilledPack);
            (callee, calleePack) = SpawnOperator(server, testMap.GridCoords, FilledPack);
        });

        // let the phone tick stamp both sets with their fill card's side
        await pair.RunTicksSync(90);

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var ui = server.System<SharedUserInterfaceSystem>();
            var telephone = server.System<RMCTelephoneSystem>();
            var hands = server.System<SharedHandsSystem>();

            // open the phone book from the panel, then dial the other set
            ui.OpenUi(callerPack, ANPRCRadioUI.Key, caller);
            ui.RaiseUiMessage(callerPack, ANPRCRadioUI.Key, new ANPRCOpenPhoneMsg { Actor = caller });
            Assert.That(ui.IsUiOpen(callerPack, RMCTelephoneUiKey.Key), Is.True, "the phone book opens");

            ui.RaiseUiMessage(callerPack, RMCTelephoneUiKey.Key,
                new RMCTelephoneCallBuiMsg(entities.GetNetEntity(calleePack)) { Actor = caller });

            var callerRadio = entities.GetComponent<ANPRCRadioComponent>(callerPack);
            var calleeRadio = entities.GetComponent<ANPRCRadioComponent>(calleePack);

            Assert.Multiple(() =>
            {
                Assert.That(telephone.AU14InCall(callerPack), Is.True, "the caller is dialing");
                Assert.That(telephone.AU14IsRinging(calleePack), Is.True, "the other set rings");
                Assert.That(hands.IsHolding(caller, callerRadio.Handset), Is.True,
                    "dialing puts the set's own handset in the caller's hand");
                Assert.That(entities.HasComponent<ANPRCHandsetUserComponent>(caller), Is.True,
                    "and wires the caller up as the handset user");
            });

            // answer from the callee's own panel
            ui.OpenUi(calleePack, ANPRCRadioUI.Key, callee);
            ui.RaiseUiMessage(calleePack, ANPRCRadioUI.Key, new ANPRCOpenPhoneMsg { Actor = callee });

            Assert.Multiple(() =>
            {
                Assert.That(telephone.AU14IsRinging(calleePack), Is.False, "answered");
                Assert.That(hands.IsHolding(callee, calleeRadio.Handset), Is.True,
                    "answering takes the callee's handset off the cradle");
                Assert.That(telephone.AU14TryGetOtherPhone(callerPack, out var farPhone), Is.True);
                Assert.That(farPhone, Is.EqualTo(calleeRadio.Handset), "the line runs handset to handset");
            });

            // the caller hangs up by putting the handset back
            var useInHand = new UseInHandEvent(caller);
            entities.EventBus.RaiseLocalEvent(callerRadio.Handset!.Value, useInHand);

            Assert.That(hands.IsHolding(caller, callerRadio.Handset), Is.False, "hanging up puts the handset back");
        });

        // the exchange drops its call state deferred, and the far end only notices on the phone tick
        await pair.RunTicksSync(90);

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var telephone = server.System<RMCTelephoneSystem>();

            Assert.Multiple(() =>
            {
                Assert.That(telephone.AU14InCall(callerPack), Is.False, "hanging up ends the caller's side");
                Assert.That(telephone.AU14InCall(calleePack), Is.False,
                    "the far end notices the line is dead and hangs up on its own");
            });

            entities.DeleteEntity(caller);
            entities.DeleteEntity(callee);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CallOverNoLinkIsRefused()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        EntityUid caller = default, callerPack = default, callee = default, calleePack = default;

        await server.WaitAssertion(() =>
        {
            (caller, callerPack) = SpawnOperator(server, testMap.GridCoords, FilledPack);

            // far outside direct range, and nothing on the test map relays for either of them
            var far = new EntityCoordinates(testMap.MapUid, 500, 500);
            (callee, calleePack) = SpawnOperator(server, far, FilledPack);
        });

        await pair.RunTicksSync(90);

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var ui = server.System<SharedUserInterfaceSystem>();

            ui.OpenUi(callerPack, RMCTelephoneUiKey.Key, caller);
            ui.RaiseUiMessage(callerPack, RMCTelephoneUiKey.Key,
                new RMCTelephoneCallBuiMsg(entities.GetNetEntity(calleePack)) { Actor = caller });
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var telephone = server.System<RMCTelephoneSystem>();

            var hands = server.System<SharedHandsSystem>();
            var callerHandset = entities.GetComponent<ANPRCRadioComponent>(callerPack).Handset;

            Assert.Multiple(() =>
            {
                Assert.That(telephone.AU14InCall(callerPack), Is.False, "the call is torn down at once");
                Assert.That(telephone.AU14InCall(calleePack), Is.False, "the far set never keeps ringing");
                Assert.That(hands.IsHolding(caller, callerHandset), Is.False,
                    "the caller is not left holding a handset with no call on it");
            });

            entities.DeleteEntity(caller);
            entities.DeleteEntity(callee);
        });

        await pair.CleanReturnAsync();
    }

    private static (EntityUid Wearer, EntityUid Pack) SpawnOperator(
        RobustIntegrationTest.ServerIntegrationInstance server,
        EntityCoordinates coords,
        EntProtoId packId)
    {
        var entities = server.EntMan;
        var spawning = server.System<StationSpawningSystem>();
        var inventory = server.System<InventorySystem>();

        var wearer = spawning.SpawnPlayerMob(coords, Rifleman, new HumanoidCharacterProfile(), station: null);
        var pack = entities.SpawnEntity(packId, coords);

        if (inventory.TryGetSlotEntity(wearer, BackSlot, out var oldBack))
            entities.DeleteEntity(oldBack.Value);

        entities.EnsureComponent<ANPRCRadioUserComponent>(wearer);
        Assert.That(inventory.TryEquip(wearer, pack, BackSlot, force: true), Is.True);

        return (wearer, pack);
    }
}
