using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Tests.Helpers;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Inventory;
using Content.Shared.Throwing;
using Robust.Shared.Localization;

namespace Content.IntegrationTests.CMU14.Yautja;

public sealed class CMUYautjaIdentityTest : GameTest
{
    [Test]
    public async Task HandlingHuntingPouchKeepsPublicIdentityStable()
    {
        var map = await Pair.CreateTestMap();
        EntityUid hunter = default;
        EntityUid pouch = default;
        await Server.WaitPost(() => hunter = SSpawnAtPosition("CMUMobYautjaMilitaryCasteSoldier", map.GridCoords));
        await Pair.RunTicksSync(1);

        await Server.WaitAssertion(() =>
        {
            SEntMan.EnsureComponent<TestListenerComponent>(hunter);
            Assert.That(Server.System<InventorySystem>().TryUnequip(hunter, "belt", out var removed), Is.True);
            pouch = removed!.Value;
            Assert.That(SComp<MetaDataComponent>(pouch).EntityPrototype!.ID, Is.EqualTo("CMUYautjaHuntingPouch"));
            Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(hunter, pouch), Is.True);
        });
        await Pair.RunTicksSync(1);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.System<SharedHandsSystem>().TryDrop(hunter, pouch), Is.True);
            Assert.That(Server.System<ThrowingSystem>().TryThrow(pouch, Vector2.UnitX, user: hunter), Is.True);
        });
        await Pair.RunTicksSync(1);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(hunter, pouch), Is.True);
            Assert.That(Server.System<InventorySystem>().TryEquip(hunter, pouch, "belt"), Is.True);
        });
        await Pair.RunTicksSync(1);

        await Server.WaitAssertion(() =>
        {
            var alias = Server.ResolveDependency<ILocalizationManager>().GetString("cmu-yautja-identity-unknown");
            Assert.That(Identity.Name(hunter, SEntMan).Name, Is.EqualTo(alias));
            Assert.That(Identity.Name(hunter, SEntMan, hunter).Name, Is.EqualTo(SComp<MetaDataComponent>(hunter).EntityName));
            Assert.That(Server.System<IdentityChangeListener>().GetEvents(hunter), Is.Empty);
        });
    }

    public sealed partial class IdentityChangeListener : TestListenerSystem<IdentityChangedEvent>;
}
