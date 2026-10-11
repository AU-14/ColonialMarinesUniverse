using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Xenonids.Egg;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared.Ghost.Components;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class CMUForsakenEggTest : GameTest
{
    [Test]
    public async Task GhostCanHatchForsakenSpecimenWithoutHumanTrigger()
    {
        var map = await Pair.CreateTestMap();
        var previous = ServerSession!.AttachedEntity;
        try
        {
            await Server.WaitAssertion(() =>
            {
                var hive = SEntMan.SpawnEntity("CMUHunterShipForsakenHive", map.GridCoords);
                var egg = SEntMan.SpawnEntity("CMUHunterShipObjEffectAlienEggForsakenEggGrowingSouth", map.GridCoords);
                Server.System<SharedXenoHiveSystem>().SetHive(egg, hive);
                var ghost = SEntMan.SpawnEntity("MobObserver", map.GridCoords);
                Server.PlayerMan.SetAttachedEntity(ServerSession, ghost);
#pragma warning disable RA0002 // Arrange a mature egg and a ghost past the respawn cooldown.
                var eggComp = SEntMan.GetComponent<XenoEggComponent>(egg);
                eggComp.State = XenoEggState.Grown;
                SEntMan.GetComponent<GhostComponent>(ghost).TimeOfDeath = TimeSpan.FromMinutes(-10);
#pragma warning restore RA0002
                SEntMan.EventBus.RaiseLocalEvent(egg, new XenoParasiteGhostBuiMsg
                {
                    Actor = ghost,
                    UiKey = XenoParasiteGhostUI.Key,
                });
                Assert.That(eggComp.SpawnedCreature, Is.Not.Null,
                    "Forsaken specimen eggs must support the same ghost hatch flow as Alpha eggs");
                Assert.That(ServerSession.AttachedEntity, Is.EqualTo(eggComp.SpawnedCreature));
                Assert.That(Server.System<SharedXenoHiveSystem>().FromSameHive(egg, eggComp.SpawnedCreature!.Value), Is.True);
            });
        }
        finally
        {
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(ServerSession, previous));
        }
    }
}
