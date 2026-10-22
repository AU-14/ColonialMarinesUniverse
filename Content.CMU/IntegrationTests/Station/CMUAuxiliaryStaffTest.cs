using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Vendors;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.CMU14.Roles;
using Content.Shared.Inventory;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Storage;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Station;

[TestFixture]
public sealed class CMUAuxiliaryStaffTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    /// <summary>Picking mess technician retitles the ID and grants the kitchen, and squads use the same title.</summary>
    [Test]
    public async Task MessTechnicianGetsTitleAndKitchenAccess()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var protos = Server.ResolveDependency<IPrototypeManager>();
            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var card = SEntMan.SpawnEntity("AU14IDCardGOVFORCombatCorrespondent", map.GridCoords);
            Assert.That(SEntMan.System<InventorySystem>().TryEquip(user, card, "id", force: true), Is.True);

            var messTech = protos.Index<LoadoutPrototype>("CMUAuxiliaryStaffMessTechnician");
            foreach (var effect in messTech.Effects)
            {
                effect.ApplyToEntity(user, SEntMan, protos);
            }

            Assert.That(SEntMan.GetComponent<IdCardComponent>(card).LocalizedJobTitle, Is.EqualTo("Mess Technician"));
            Assert.That(SEntMan.System<SharedAccessSystem>().TryGetTags(card), Does.Contain(new ProtoId<AccessLevelPrototype>("CMUAccessKitchen")));
            Assert.That(SEntMan.GetComponent<RMCVendorRoleOverrideComponent>(user).GiveSquadRoleName?.Id, Is.EqualTo("cmu-auxiliary-staff-mess-technician"));
        });
    }

    /// <summary>A chaplain gets their own faith's holy book in their kit, and the Bible if their faith uses it.</summary>
    [TestCase("CMUIslam", "CMUHolyBookQuran")]
    [TestCase("CMUJudaism", "CMUHolyBookTanakh")]
    [TestCase("CMUCatholic", "RMCBible")]
    public async Task ChaplainGetsTheirFaithsBook(string religion, string book)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var kit = SEntMan.SpawnEntity("CMUKitAuxiliaryStaffChaplain", map.GridCoords);
            Assert.That(SEntMan.System<Content.Shared.Hands.EntitySystems.SharedHandsSystem>().TryPickupAnyHand(user, kit), Is.True);
            var chaplain = SEntMan.EnsureComponent<Content.Shared.CMU14.Religion.CMUChaplainComponent>(user);

            SEntMan.System<Content.Server.CMU14.Religion.CMUReligionSystem>().GiveHolyBook((user, chaplain), religion);

            var contents = SEntMan.GetComponent<StorageComponent>(kit).Container.ContainedEntities
                .Select(e => SEntMan.GetComponent<MetaDataComponent>(e).EntityPrototype!.ID)
                .ToList();
            Assert.That(contents, Does.Contain(book));
        });
    }

    [TestCase("CMUKitAuxiliaryStaffCombatCorrespondent", "RMCCameraBroadcasting", "RMCCorrespondentMicrophone")]
    [TestCase("CMUKitAuxiliaryStaffChaplain", "RMCHelmetGarbChaplainPatch")]
    [TestCase("CMUKitAuxiliaryStaffMessTechnician", "RMCChefApron", "ClothingHeadHatChef")]
    public async Task KitsHoldTheirGear(string kit, params string[] items)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var spawned = SEntMan.SpawnEntity(kit, map.GridCoords);
            var contents = SEntMan.GetComponent<StorageComponent>(spawned).Container.ContainedEntities
                .Select(e => SEntMan.GetComponent<MetaDataComponent>(e).EntityPrototype!.ID)
                .ToList();
            Assert.That(contents, Is.EquivalentTo(items));
        });
    }
}
