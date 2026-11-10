using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Crafting;
using Content.Shared._RMC14.Xenonids.Weeds;
using Content.Shared.CMU14.Xenonids.Weeds;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Roles;
using Content.Shared.Stacks;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.BugReports;

[TestFixture]
public sealed class NovemberTenRegressionTest : GameTest
{
    // the UNISC lead enforcer was the only team leader without orders
    [Test]
    public async Task UniscLeadEnforcerCanGiveOrders()
    {
        await Server.WaitAssertion(() =>
        {
            var job = SProtoMan.Index<JobPrototype>("AU14JobUNISCEnforcerLeader");
            Assert.That(job.RoundComponents.ContainsKey("MarineOrders"), Is.True);
        });
    }

    // every ambassador merc came out named "Nameless mercenary"
    [Test]
    public async Task AmbassadorMercenariesGetTheirOwnNames()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var first = SEntMan.SpawnEntity("RUMCJobTWEMerc", map.GridCoords);
            var second = SEntMan.SpawnEntity("RUMCJobUPPMercLeader", map.GridCoords);
            Assert.That(SEntMan.GetComponent<MetaDataComponent>(first).EntityName, Is.Not.EqualTo("Nameless mercenary"));
            Assert.That(SEntMan.GetComponent<MetaDataComponent>(second).EntityName, Is.Not.EqualTo("Nameless mercenary"));
        });
    }

    // pathogen weeds grew the xeno wall weed, their own mycelium wall weed was never used
    [Test]
    public async Task PathogenWeedsPickTheirOwnWallWeed()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var wall = SEntMan.SpawnEntity("CMWallMetal", map.GridCoords);
            var pathogen = SEntMan.SpawnEntity("CMU14XenoMyceliumWeeds", map.GridCoords.Offset(new System.Numerics.Vector2(1, 0)));
            var xeno = SEntMan.SpawnEntity("XenoWeeds", map.GridCoords.Offset(new System.Numerics.Vector2(-1, 0)));
            var weedable = SEntMan.GetComponent<XenoWeedableComponent>(wall);
            Assert.That(SEntMan.HasComponent<CMUWallWeedsSpawnComponent>(pathogen), Is.True);

            var ev = new AfterEntityWeedingEvent(pathogen, wall);
            SEntMan.EventBus.RaiseLocalEvent(pathogen, ref ev);
            Assert.That(weedable.Spawn?.Id, Is.EqualTo("CMU14XenoMyceliumWeedsWall"));

            var back = new AfterEntityWeedingEvent(xeno, wall);
            SEntMan.EventBus.RaiseLocalEvent(xeno, ref back);
            Assert.That(weedable.Spawn?.Id, Is.EqualTo("XenoWeedsWall"), "ordinary weeds put the xeno wall weed back");
        });
    }

    // sand was the one recipe behind a construction skill, the menu hid it from everyone without it
    [Test]
    public async Task SandRecipeListsWithoutConstructionSkill()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var powder = SEntMan.SpawnEntity("RUMCStonePowderItem", map.GridCoords);
            SEntMan.System<SharedStackSystem>().SetCount(powder, 4);
            Assert.That(SEntMan.System<SharedHandsSystem>().TryPickupAnyHand(user, powder), Is.True);

            var recipes = SEntMan.System<CraftingSystem>().GetAvailableRecipes(user).Select(r => r.ID).ToList();
            Assert.That(recipes, Does.Contain("RUMCCraftSand"));
        });
    }
}
