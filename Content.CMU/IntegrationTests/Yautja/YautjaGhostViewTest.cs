using Content.Client.Verbs;
using Content.IntegrationTests.Fixtures;
using Content.Shared.CMU14.Yautja;
using Content.Shared.IdentityManagement;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
[TestOf(typeof(YautjaVisibilitySystem))]
[TestOf(typeof(VerbSystem))]
public sealed class YautjaGhostViewTest : GameTest
{
    [Test]
    public async Task GhostSeesYautjaNameButPlayersDont()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var pred = SEntMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var ghost = SEntMan.SpawnEntity("MobObserver", map.GridCoords);
            var human = SEntMan.SpawnEntity("MobHuman", map.GridCoords);

            var trueName = SEntMan.GetComponent<MetaDataComponent>(pred).EntityName;
            var unknown = Loc.GetString(SEntMan.GetComponent<YautjaComponent>(pred).IdentityName);

            Assert.Multiple(() =>
            {
                Assert.That(Identity.Name(pred, SEntMan, ghost).Name, Does.Contain(trueName));
                Assert.That(Identity.Name(pred, SEntMan, human).Name, Is.EqualTo(unknown));
            });

            SEntMan.DeleteEntity(pred);
            SEntMan.DeleteEntity(ghost);
            SEntMan.DeleteEntity(human);
        });
    }

    [Test]
    public async Task OnlyGhostsGetCloakedYautjaInRightClickMenu()
    {
        await Client.WaitAssertion(() =>
        {
            var verbs = CEntMan.System<VerbSystem>();
            var pred = CEntMan.Spawn("CMUMobYautja");
            var ghost = CEntMan.Spawn("MobObserver");
            var human = CEntMan.Spawn("MobHuman");

            Assert.Multiple(() =>
            {
                Assert.That(verbs.CMUShowHiddenInMenu(ghost, pred), Is.True);
                Assert.That(verbs.CMUShowHiddenInMenu(human, pred), Is.False);
                Assert.That(verbs.CMUShowHiddenInMenu(pred, pred), Is.False);
                Assert.That(verbs.CMUShowHiddenInMenu(ghost, human), Is.False);
            });

            CEntMan.DeleteEntity(pred);
            CEntMan.DeleteEntity(ghost);
            CEntMan.DeleteEntity(human);
        });
    }
}
